using System.Text;
using System.Text.Json.Nodes;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Phase 2 broker body inspection — pure-function tests for
/// <see cref="DockerBrokerBodyInspector"/>. These cover the rejection rules
/// and the airlock NetworkMode injection that unblocks Testcontainers under
/// airlock + DinD. Bodies are built with JsonNode rather than anonymous types
/// because the test project runs under Native AOT, where reflection-based
/// JsonSerializer is disabled.
/// </summary>
public class DockerBrokerBodyInspectorTests
{
  private static byte[] Bytes(JsonNode node) =>
    Encoding.UTF8.GetBytes(node.ToJsonString());

  private static JsonObject Container(JsonObject? hostConfig = null, JsonObject? networkingConfig = null)
  {
    var root = new JsonObject { ["Image"] = "alpine" };
    if (hostConfig is not null) root["HostConfig"] = hostConfig;
    if (networkingConfig is not null) root["NetworkingConfig"] = networkingConfig;
    return root;
  }

  private static JsonArray ToArray(params string[] items)
  {
    // JsonArray.Add(string) goes through reflection-based JSON conversion
    // which is disabled under AOT. JsonValue.Create<string> is the explicit
    // path that doesn't trip the reflection guard.
    var a = new JsonArray();
    foreach (var i in items) a.Add(JsonValue.Create(i));
    return a;
  }

  /// <summary>
  /// Permissive policy for tests that aren't exercising the image allowlist
  /// itself. The default in production is strict default-deny — empty
  /// allowed_images rejects every spawn. Tests that want to verify other
  /// rules (Privileged, Binds, etc.) need to bypass the image filter to
  /// reach the rule they're actually checking, hence "*".
  /// </summary>
  private static CopilotHere.Commands.DockerBroker.DockerBrokerBodyInspectionConfig PermissiveImages() => new()
  {
    AllowedImages = ["*"]
  };

  private static DockerBrokerBodyInspector.InspectionResult InspectWithAnyImage(byte[] body, string? siblingNetworkName = null) =>
    DockerBrokerBodyInspector.Inspect(body, siblingNetworkName, PermissiveImages());

  // ── Rejections ─────────────────────────────────────────────────────────

  [Test]
  public async Task Rejects_Privileged_True()
  {
    var body = Bytes(Container(new JsonObject { ["Privileged"] = true }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());

    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains("Privileged");
  }

  [Test]
  public async Task Allows_Privileged_False_Or_Missing()
  {
    var bodies = new[]
    {
      Bytes(Container(new JsonObject { ["Privileged"] = false })),
      Bytes(Container(new JsonObject())),
      Bytes(Container()),
    };
    foreach (var body in bodies)
    {
      var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
      await Assert.That(result.Allowed).IsTrue();
    }
  }

  [Test]
  [Arguments("NetworkMode")]
  [Arguments("PidMode")]
  [Arguments("IpcMode")]
  [Arguments("UsernsMode")]
  public async Task Rejects_HostMode(string field)
  {
    var body = Bytes(Container(new JsonObject { [field] = "host" }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());

    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains(field);
  }

  [Test]
  [Arguments("/")]
  [Arguments("/etc")]
  [Arguments("/var")]
  [Arguments("/var/run/docker.sock")]
  public async Task Rejects_ForbiddenBinds(string hostPath)
  {
    var body = Bytes(Container(new JsonObject { ["Binds"] = ToArray($"{hostPath}:/mnt/host") }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains("forbidden host path");
  }

  [Test]
  public async Task Allows_SafeBinds()
  {
    var body = Bytes(Container(new JsonObject { ["Binds"] = ToArray("/tmp/work:/work", "/home/user/proj:/proj:ro") }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsTrue();
  }

  [Test]
  [Arguments("SYS_ADMIN")]
  [Arguments("CAP_SYS_ADMIN")]
  [Arguments("SYS_PTRACE")]
  public async Task Rejects_DangerousCapAdd(string capability)
  {
    var body = Bytes(Container(new JsonObject { ["CapAdd"] = ToArray(capability) }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains("deny list");
  }

  [Test]
  public async Task Allows_BenignCapAdd()
  {
    var body = Bytes(Container(new JsonObject { ["CapAdd"] = ToArray("NET_BIND_SERVICE") }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsTrue();
  }

  // ── NetworkMode injection (airlock + DinD unblocker) ───────────────────

  [Test]
  public async Task InjectsNetworkMode_WhenSiblingNetworkProvided_AndModeMissing()
  {
    var body = Bytes(Container(new JsonObject()));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: "myproj_airlock", PermissiveImages());

    await Assert.That(result.Allowed).IsTrue();
    await Assert.That(result.RewrittenBody).IsNotNull();

    var parsed = JsonNode.Parse(result.RewrittenBody!) as JsonObject;
    await Assert.That(parsed!["HostConfig"]!["NetworkMode"]!.GetValue<string>()).IsEqualTo("myproj_airlock");
  }

  [Test]
  [Arguments("default")]
  [Arguments("bridge")]
  public async Task InjectsNetworkMode_OverridesDefaultAndBridge(string original)
  {
    var body = Bytes(Container(new JsonObject { ["NetworkMode"] = original }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: "myproj_airlock", PermissiveImages());

    await Assert.That(result.RewrittenBody).IsNotNull();
    var parsed = JsonNode.Parse(result.RewrittenBody!) as JsonObject;
    await Assert.That(parsed!["HostConfig"]!["NetworkMode"]!.GetValue<string>()).IsEqualTo("myproj_airlock");
  }

  [Test]
  public async Task LeavesNetworkMode_AloneWhenAlreadyExplicit()
  {
    var body = Bytes(Container(new JsonObject { ["NetworkMode"] = "user-defined-net" }));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: "myproj_airlock", PermissiveImages());

    await Assert.That(result.Allowed).IsTrue();
    await Assert.That(result.RewrittenBody).IsNull();
  }

  [Test]
  public async Task ClearsEndpointsConfig_WhenInjecting()
  {
    var body = Encoding.UTF8.GetBytes(@"{
      ""Image"": ""alpine"",
      ""HostConfig"": {},
      ""NetworkingConfig"": {
        ""EndpointsConfig"": {
          ""bridge"": { ""IPAMConfig"": null }
        }
      }
    }");
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: "myproj_airlock", PermissiveImages());

    await Assert.That(result.RewrittenBody).IsNotNull();
    var parsed = JsonNode.Parse(result.RewrittenBody!) as JsonObject;
    var endpoints = parsed!["NetworkingConfig"]!["EndpointsConfig"] as JsonObject;
    await Assert.That(endpoints).IsNotNull();
    await Assert.That(endpoints!.Count).IsEqualTo(0);
  }

  // ── No-op cases ────────────────────────────────────────────────────────

  [Test]
  public async Task UnparseableBody_ForwardsAsIs()
  {
    var body = Encoding.UTF8.GetBytes("not json {");
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsTrue();
    await Assert.That(result.RewrittenBody).IsNull();
  }

  [Test]
  public async Task EmptyBody_ForwardsAsIs()
  {
    var result = DockerBrokerBodyInspector.Inspect(Array.Empty<byte>(), siblingNetworkName: null);
    await Assert.That(result.Allowed).IsTrue();
    await Assert.That(result.RewrittenBody).IsNull();
  }

  [Test]
  public async Task NoSiblingNetwork_AndNoMutationNeeded_ReturnsOriginal()
  {
    var body = Bytes(Container(new JsonObject()));
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, PermissiveImages());
    await Assert.That(result.Allowed).IsTrue();
    await Assert.That(result.RewrittenBody).IsNull();
  }

  // ── Image allowlist ────────────────────────────────────────────────────

  [Test]
  public async Task ImageAllowlist_Empty_RejectsEverything()
  {
    // Strict default-deny: empty allowed_images means no sibling can spawn.
    // Users must explicitly enumerate trusted image patterns.
    var body = Bytes(Container());
    var policy = new CopilotHere.Commands.DockerBroker.DockerBrokerBodyInspectionConfig();
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, policy);
    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains("no trusted images configured");
  }

  [Test]
  public async Task ImageAllowlist_RejectsImageNotInList()
  {
    var body = Bytes(Container());
    var policy = new CopilotHere.Commands.DockerBroker.DockerBrokerBodyInspectionConfig
    {
      AllowedImages = ["mcr.microsoft.com/mssql/server:*"]
    };
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, policy);
    await Assert.That(result.Allowed).IsFalse();
    await Assert.That(result.Reason).Contains("allowed_images");
  }

  [Test]
  public async Task ImageAllowlist_AllowsExactMatch()
  {
    var body = Bytes(Container());
    var policy = new CopilotHere.Commands.DockerBroker.DockerBrokerBodyInspectionConfig
    {
      AllowedImages = ["alpine"]
    };
    var result = DockerBrokerBodyInspector.Inspect(body, siblingNetworkName: null, policy);
    await Assert.That(result.Allowed).IsTrue();
  }

  [Test]
  [Arguments("mcr.microsoft.com/mssql/server:*", "mcr.microsoft.com/mssql/server:2022-latest", true)]
  [Arguments("mcr.microsoft.com/mssql/server:*", "mcr.microsoft.com/mssql/server", false)]
  [Arguments("testcontainers/*", "testcontainers/ryuk:0.14.0", true)]
  [Arguments("alpine:*", "alpine:3.21", true)]
  [Arguments("alpine:*", "alpine", false)]
  [Arguments("*", "anything/at/all:tag", true)]
  [Arguments("registry.example.com/*/web:v*", "registry.example.com/team/web:v3", true)]
  public async Task ImagePatternMatches_Glob(string pattern, string image, bool expected)
  {
    await Assert.That(DockerBrokerBodyInspector.ImagePatternMatches(pattern, image)).IsEqualTo(expected);
  }
}
