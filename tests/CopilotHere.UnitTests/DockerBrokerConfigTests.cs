using CopilotHere.Commands.DockerBroker;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class DockerBrokerConfigTests
{
  private string _tempDir = null!;
  private AppPaths _paths = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);

    var globalConfigPath = Path.Combine(_tempDir, "global");
    var localConfigPath = Path.Combine(_tempDir, "local");
    Directory.CreateDirectory(globalConfigPath);
    Directory.CreateDirectory(localConfigPath);

    _paths = new AppPaths
    {
      UserHome = "/home/testuser",
      GlobalConfigPath = globalConfigPath,
      LocalConfigPath = localConfigPath,
      CurrentDirectory = _tempDir,
      ContainerWorkDir = "/work",
      CopilotConfigPath = Path.Combine(_tempDir, ".copilot")
    };
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task LoadDefaultRules_LoadsEmbeddedResource()
  {
    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await Assert.That(defaults).IsNotNull();
    await Assert.That(defaults!.AllowedEndpoints.Count).IsGreaterThan(20);
    await Assert.That(defaults.Mode).IsEqualTo("enforce");
  }

  [Test]
  public async Task LoadEffective_NoFiles_ReturnsEmbeddedDefaults()
  {
    var config = DockerBrokerConfigLoader.LoadEffective(_paths, out var source);
    await Assert.That(source).IsEqualTo(DockerBrokerConfigSource.Default);
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.AllowedEndpoints.Count).IsGreaterThan(0);
  }

  [Test]
  public async Task LoadEffective_LocalOverridesGlobal()
  {
    var globalCfg = new DockerBrokerConfig { Enabled = true, Mode = "monitor", InheritDefaultRules = false };
    DockerBrokerConfigLoader.WriteConfig(_paths.GetGlobalPath("docker-broker.json"), globalCfg);

    var localCfg = new DockerBrokerConfig { Enabled = true, Mode = "enforce", InheritDefaultRules = false };
    DockerBrokerConfigLoader.WriteConfig(_paths.GetLocalPath("docker-broker.json"), localCfg);

    var effective = DockerBrokerConfigLoader.LoadEffective(_paths, out var source);
    await Assert.That(source).IsEqualTo(DockerBrokerConfigSource.Local);
    await Assert.That(effective.Mode).IsEqualTo("enforce");
  }

  [Test]
  public async Task LoadEffective_InheritDefaultRules_MergesEmbeddedRules()
  {
    var localCfg = new DockerBrokerConfig
    {
      Enabled = true,
      Mode = "enforce",
      InheritDefaultRules = true,
      AllowedEndpoints =
      [
        new DockerBrokerEndpoint { Method = "POST", Path = "/my-custom/endpoint" }
      ]
    };
    DockerBrokerConfigLoader.WriteConfig(_paths.GetLocalPath("docker-broker.json"), localCfg);

    var effective = DockerBrokerConfigLoader.LoadEffective(_paths, out _);

    // Custom user rule preserved
    var hasCustom = effective.AllowedEndpoints.Any(e => e.Method == "POST" && e.Path == "/my-custom/endpoint");
    await Assert.That(hasCustom).IsTrue();

    // Default rules merged in
    var hasPing = effective.AllowedEndpoints.Any(e => e.Method == "GET" && e.Path == "/_ping");
    await Assert.That(hasPing).IsTrue();
  }

  [Test]
  public async Task LoadEffective_InheritDisabled_DoesNotMergeDefaults()
  {
    var localCfg = new DockerBrokerConfig
    {
      Enabled = true,
      Mode = "enforce",
      InheritDefaultRules = false,
      AllowedEndpoints =
      [
        new DockerBrokerEndpoint { Method = "POST", Path = "/my-custom/endpoint" }
      ]
    };
    DockerBrokerConfigLoader.WriteConfig(_paths.GetLocalPath("docker-broker.json"), localCfg);

    var effective = DockerBrokerConfigLoader.LoadEffective(_paths, out _);

    await Assert.That(effective.AllowedEndpoints.Count).IsEqualTo(1);
    var hasPing = effective.AllowedEndpoints.Any(e => e.Method == "GET" && e.Path == "/_ping");
    await Assert.That(hasPing).IsFalse();
  }

  [Test]
  public async Task EnableLocal_CreatesFileFromDefaults()
  {
    DockerBrokerConfigLoader.EnableLocal(_paths);
    var localPath = _paths.GetLocalPath("docker-broker.json");
    await Assert.That(File.Exists(localPath)).IsTrue();

    var cfg = DockerBrokerConfigLoader.ReadConfig(localPath);
    await Assert.That(cfg).IsNotNull();
    await Assert.That(cfg!.Enabled).IsTrue();
    await Assert.That(cfg.AllowedEndpoints.Count).IsGreaterThan(0);
  }

  [Test]
  public async Task DisableLocal_FlipsEnabledFalse()
  {
    DockerBrokerConfigLoader.EnableLocal(_paths);
    DockerBrokerConfigLoader.DisableLocal(_paths);
    var localPath = _paths.GetLocalPath("docker-broker.json");
    var cfg = DockerBrokerConfigLoader.ReadConfig(localPath);
    await Assert.That(cfg).IsNotNull();
    await Assert.That(cfg!.Enabled).IsFalse();
  }

  [Test]
  public async Task RoundTrip_PreservesAllFields()
  {
    var written = new DockerBrokerConfig
    {
      Enabled = true,
      EnableLogging = true,
      InheritDefaultRules = false,
      Mode = "monitor",
      AllowedEndpoints =
      [
        new DockerBrokerEndpoint { Method = "GET", Path = "/_ping" },
        new DockerBrokerEndpoint { Method = "POST", Path = "/containers/create" }
      ]
    };

    var path = Path.Combine(_tempDir, "broker.json");
    DockerBrokerConfigLoader.WriteConfig(path, written);
    var read = DockerBrokerConfigLoader.ReadConfig(path);

    await Assert.That(read).IsNotNull();
    await Assert.That(read!.Enabled).IsTrue();
    await Assert.That(read.EnableLogging).IsTrue();
    await Assert.That(read.InheritDefaultRules).IsFalse();
    await Assert.That(read.Mode).IsEqualTo("monitor");
    await Assert.That(read.AllowedEndpoints.Count).IsEqualTo(2);
    await Assert.That(read.AllowedEndpoints[0].Method).IsEqualTo("GET");
    await Assert.That(read.AllowedEndpoints[0].Path).IsEqualTo("/_ping");
  }
}
