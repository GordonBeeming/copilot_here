using CopilotHere.Commands.DockerBroker;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class DockerBrokerRouteTests
{
  private static DockerSocketBroker MakeBroker(string mode, params (string Method, string Path)[] allowed)
  {
    var config = new DockerBrokerConfig
    {
      Enabled = true,
      Mode = mode,
      AllowedEndpoints = allowed.Select(t => new DockerBrokerEndpoint { Method = t.Method, Path = t.Path }).ToList()
    };
    return new DockerSocketBroker(config, "/var/run/docker.sock", BrokerListenEndpoint.Unix("/tmp/test-broker.sock"));
  }

  [Test]
  public async Task PathMatches_ExactSegments_Matches()
  {
    await Assert.That(DockerSocketBroker.PathMatches("/containers/json", "/containers/json")).IsTrue();
  }

  [Test]
  public async Task PathMatches_DifferentLengths_DoesNotMatch()
  {
    await Assert.That(DockerSocketBroker.PathMatches("/containers", "/containers/json")).IsFalse();
    await Assert.That(DockerSocketBroker.PathMatches("/containers/json", "/containers")).IsFalse();
  }

  [Test]
  public async Task PathMatches_GlobMatchesSingleSegment()
  {
    await Assert.That(DockerSocketBroker.PathMatches("/containers/*/json", "/containers/abc123/json")).IsTrue();
    await Assert.That(DockerSocketBroker.PathMatches("/containers/*/exec", "/containers/c1/exec")).IsTrue();
  }

  [Test]
  public async Task PathMatches_GlobDoesNotMatchExtraSegments()
  {
    // '*' is single-segment, so /containers/*/json must not match /containers/abc/extra/json
    await Assert.That(DockerSocketBroker.PathMatches("/containers/*/json", "/containers/abc/extra/json")).IsFalse();
  }

  [Test]
  public async Task PathMatches_LiteralSegmentsAreCaseSensitive()
  {
    await Assert.That(DockerSocketBroker.PathMatches("/Containers/json", "/containers/json")).IsFalse();
  }

  [Test]
  public async Task StripVersionPrefix_RemovesV1_43()
  {
    await Assert.That(DockerSocketBroker.StripVersionPrefix("/v1.43/containers/json")).IsEqualTo("/containers/json");
  }

  [Test]
  public async Task StripVersionPrefix_RemovesVersionWithoutMinor()
  {
    await Assert.That(DockerSocketBroker.StripVersionPrefix("/v2/containers/json")).IsEqualTo("/containers/json");
  }

  [Test]
  public async Task StripVersionPrefix_LeavesNonVersionedPaths()
  {
    await Assert.That(DockerSocketBroker.StripVersionPrefix("/containers/json")).IsEqualTo("/containers/json");
    await Assert.That(DockerSocketBroker.StripVersionPrefix("/volumes/abc")).IsEqualTo("/volumes/abc");
  }

  [Test]
  public async Task StripVersionPrefix_LeavesPathsWithVButNoDigits()
  {
    await Assert.That(DockerSocketBroker.StripVersionPrefix("/version")).IsEqualTo("/version");
  }

  [Test]
  public async Task StripQuery_RemovesEverythingAfterQuestionMark()
  {
    await Assert.That(DockerSocketBroker.StripQuery("/containers/json?all=1&size=true")).IsEqualTo("/containers/json");
    await Assert.That(DockerSocketBroker.StripQuery("/containers/json")).IsEqualTo("/containers/json");
  }

  [Test]
  public async Task CheckRule_EnforceMode_AllowsExactMatch()
  {
    var broker = MakeBroker("enforce", ("GET", "/containers/json"));
    var (allowed, _) = broker.CheckRule("GET", "/containers/json");
    await Assert.That(allowed).IsTrue();
  }

  [Test]
  public async Task CheckRule_EnforceMode_AllowsCaseInsensitiveMethod()
  {
    var broker = MakeBroker("enforce", ("get", "/containers/json"));
    var (allowed, _) = broker.CheckRule("GET", "/containers/json");
    await Assert.That(allowed).IsTrue();
  }

  [Test]
  public async Task CheckRule_EnforceMode_AllowsGlob()
  {
    var broker = MakeBroker("enforce", ("POST", "/containers/*/start"));
    var (allowed, _) = broker.CheckRule("POST", "/containers/abc/start");
    await Assert.That(allowed).IsTrue();
  }

  [Test]
  public async Task CheckRule_EnforceMode_DeniesUnmatchedPath()
  {
    var broker = MakeBroker("enforce", ("GET", "/containers/json"));
    var (allowed, reason) = broker.CheckRule("GET", "/swarm/init");
    await Assert.That(allowed).IsFalse();
    await Assert.That(reason).Contains("no rule");
  }

  [Test]
  public async Task CheckRule_EnforceMode_DeniesWrongMethod()
  {
    var broker = MakeBroker("enforce", ("GET", "/containers/json"));
    var (allowed, _) = broker.CheckRule("DELETE", "/containers/json");
    await Assert.That(allowed).IsFalse();
  }

  [Test]
  public async Task CheckRule_MonitorMode_AllowsEverything()
  {
    var broker = MakeBroker("monitor"); // empty allowlist
    var (allowed, reason) = broker.CheckRule("POST", "/swarm/init");
    await Assert.That(allowed).IsTrue();
    await Assert.That(reason).Contains("monitor");
  }

  [Test]
  public async Task DefaultRules_AllowTestcontainersHappyPath()
  {
    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await Assert.That(defaults).IsNotNull();
    var broker = new DockerSocketBroker(defaults!, "/var/run/docker.sock", BrokerListenEndpoint.Unix("/tmp/test.sock"));

    // Things Testcontainers will hit
    await Assert.That(broker.CheckRule("GET", "/_ping").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("GET", "/version").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("GET", "/info").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("POST", "/containers/create").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("POST", "/containers/abc/start").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("DELETE", "/containers/abc").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("POST", "/images/create").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("POST", "/networks/create").Allowed).IsTrue();
    await Assert.That(broker.CheckRule("POST", "/volumes/create").Allowed).IsTrue();
  }

  [Test]
  public async Task DefaultRules_DenyDangerousEndpoints()
  {
    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await Assert.That(defaults).IsNotNull();
    var broker = new DockerSocketBroker(defaults!, "/var/run/docker.sock", BrokerListenEndpoint.Unix("/tmp/test.sock"));

    // Endpoints intentionally not in the allowlist
    await Assert.That(broker.CheckRule("POST", "/swarm/init").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("POST", "/swarm/leave").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/secrets").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/configs").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/nodes").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/services").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/plugins").Allowed).IsFalse();
    await Assert.That(broker.CheckRule("GET", "/events").Allowed).IsFalse();
  }
}
