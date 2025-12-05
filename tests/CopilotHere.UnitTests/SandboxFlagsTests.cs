using CopilotHere.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace CopilotHere.UnitTests;

[NotInParallel]
public class SandboxFlagsTests
{
  [Test]
  public async Task Parse_WithEmptyEnvironmentVariable_ReturnsEmptyList()
  {
    // Arrange
    Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);

    try
    {
      // Act
      var result = SandboxFlags.Parse();

      // Assert
      await Assert.That(result).IsEmpty();
    }
    finally
    {
      Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);
    }
  }

  [Test]
  public async Task Parse_WithSingleFlag_ReturnsParsedFlag()
  {
    // Arrange
    Environment.SetEnvironmentVariable("SANDBOX_FLAGS", "--network host");

    try
    {
      // Act
      var result = SandboxFlags.Parse();

      // Assert
      await Assert.That(result).HasCount().EqualTo(2);
      await Assert.That(result[0]).IsEqualTo("--network");
      await Assert.That(result[1]).IsEqualTo("host");
    }
    finally
    {
      Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);
    }
  }

  [Test]
  public async Task Parse_WithMultipleFlags_ReturnsParsedFlags()
  {
    // Arrange
    Environment.SetEnvironmentVariable("SANDBOX_FLAGS", "--network host --env DEBUG=1 --env LOG=trace");

    try
    {
      // Act
      var result = SandboxFlags.Parse();

      // Assert
      await Assert.That(result).HasCount().EqualTo(6);
      await Assert.That(result[0]).IsEqualTo("--network");
      await Assert.That(result[1]).IsEqualTo("host");
      await Assert.That(result[2]).IsEqualTo("--env");
      await Assert.That(result[3]).IsEqualTo("DEBUG=1");
      await Assert.That(result[4]).IsEqualTo("--env");
      await Assert.That(result[5]).IsEqualTo("LOG=trace");
    }
    finally
    {
      Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);
    }
  }

  [Test]
  public async Task Parse_WithQuotedValues_RemovesQuotes()
  {
    // Arrange
    Environment.SetEnvironmentVariable("SANDBOX_FLAGS", "--env MESSAGE=\"hello world\"");

    try
    {
      // Act
      var result = SandboxFlags.Parse();

      // Assert
      await Assert.That(result).HasCount().EqualTo(2);
      await Assert.That(result[0]).IsEqualTo("--env");
      await Assert.That(result[1]).IsEqualTo("MESSAGE=hello world");
    }
    finally
    {
      Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);
    }
  }

  [Test]
  public async Task Parse_WithExtraSpaces_HandlesCorrectly()
  {
    // Arrange
    Environment.SetEnvironmentVariable("SANDBOX_FLAGS", "  --network   host  --env  TEST=1  ");

    try
    {
      // Act
      var result = SandboxFlags.Parse();

      // Assert
      await Assert.That(result).HasCount().EqualTo(4);
      await Assert.That(result[0]).IsEqualTo("--network");
      await Assert.That(result[1]).IsEqualTo("host");
      await Assert.That(result[2]).IsEqualTo("--env");
      await Assert.That(result[3]).IsEqualTo("TEST=1");
    }
    finally
    {
      Environment.SetEnvironmentVariable("SANDBOX_FLAGS", null);
    }
  }

  [Test]
  public async Task ExtractNetwork_WithNetworkFlag_ReturnsNetworkName()
  {
    // Arrange
    var flags = new List<string> { "--network", "my-network", "--env", "TEST=1" };

    // Act
    var result = SandboxFlags.ExtractNetwork(flags);

    // Assert
    await Assert.That(result).IsEqualTo("my-network");
  }

  [Test]
  public async Task ExtractNetwork_WithNetShortFlag_ReturnsNetworkName()
  {
    // Arrange
    var flags = new List<string> { "--net", "custom-net", "--env", "TEST=1" };

    // Act
    var result = SandboxFlags.ExtractNetwork(flags);

    // Assert
    await Assert.That(result).IsEqualTo("custom-net");
  }

  [Test]
  public async Task ExtractNetwork_WithoutNetworkFlag_ReturnsNull()
  {
    // Arrange
    var flags = new List<string> { "--env", "TEST=1", "--memory", "2g" };

    // Act
    var result = SandboxFlags.ExtractNetwork(flags);

    // Assert
    await Assert.That(result).IsNull();
  }

  [Test]
  public async Task FilterNetworkFlags_RemovesNetworkFlags()
  {
    // Arrange
    var flags = new List<string> { "--network", "host", "--env", "TEST=1", "--memory", "2g" };

    // Act
    var result = SandboxFlags.FilterNetworkFlags(flags);

    // Assert
    await Assert.That(result).HasCount().EqualTo(4);
    await Assert.That(result[0]).IsEqualTo("--env");
    await Assert.That(result[1]).IsEqualTo("TEST=1");
    await Assert.That(result[2]).IsEqualTo("--memory");
    await Assert.That(result[3]).IsEqualTo("2g");
  }

  [Test]
  public async Task FilterNetworkFlags_RemovesNetShortFlags()
  {
    // Arrange
    var flags = new List<string> { "--net", "bridge", "--env", "TEST=1" };

    // Act
    var result = SandboxFlags.FilterNetworkFlags(flags);

    // Assert
    await Assert.That(result).HasCount().EqualTo(2);
    await Assert.That(result[0]).IsEqualTo("--env");
    await Assert.That(result[1]).IsEqualTo("TEST=1");
  }

  [Test]
  public async Task FilterNetworkFlags_WithNoNetworkFlags_ReturnsAllFlags()
  {
    // Arrange
    var flags = new List<string> { "--env", "TEST=1", "--memory", "2g" };

    // Act
    var result = SandboxFlags.FilterNetworkFlags(flags);

    // Assert
    await Assert.That(result).HasCount().EqualTo(4);
  }

  [Test]
  public async Task ToComposeYaml_WithEmptyFlags_ReturnsEmpty()
  {
    // Arrange
    var flags = new List<string>();

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).IsEqualTo("");
  }

  [Test]
  public async Task ToComposeYaml_WithEnvFlag_GeneratesEnvironmentYaml()
  {
    // Arrange
    var flags = new List<string> { "--env", "DEBUG=1" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("environment:");
    await Assert.That(result).Contains("- DEBUG=1");
  }

  [Test]
  public async Task ToComposeYaml_WithMultipleEnvFlags_GeneratesEnvironmentYaml()
  {
    // Arrange
    var flags = new List<string> { "--env", "DEBUG=1", "--env", "LOG=trace" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("environment:");
    await Assert.That(result).Contains("- DEBUG=1");
    await Assert.That(result).Contains("- LOG=trace");
  }

  [Test]
  public async Task ToComposeYaml_WithCapAddFlag_GeneratesCapAddYaml()
  {
    // Arrange
    var flags = new List<string> { "--cap-add", "SYS_PTRACE" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("cap_add:");
    await Assert.That(result).Contains("- SYS_PTRACE");
  }

  [Test]
  public async Task ToComposeYaml_WithCapDropFlag_GeneratesCapDropYaml()
  {
    // Arrange
    var flags = new List<string> { "--cap-drop", "NET_RAW" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("cap_drop:");
    await Assert.That(result).Contains("- NET_RAW");
  }

  [Test]
  public async Task ToComposeYaml_WithMemoryFlag_GeneratesMemLimitYaml()
  {
    // Arrange
    var flags = new List<string> { "--memory", "2g" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("mem_limit: 2g");
  }

  [Test]
  public async Task ToComposeYaml_WithCpusFlag_GeneratesCpusYaml()
  {
    // Arrange
    var flags = new List<string> { "--cpus", "1.5" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("cpus: 1.5");
  }

  [Test]
  public async Task ToComposeYaml_WithUlimitFlag_GeneratesUlimitsYaml()
  {
    // Arrange
    var flags = new List<string> { "--ulimit", "nofile=1024" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("ulimits:");
    await Assert.That(result).Contains("nofile: 1024");
  }

  [Test]
  public async Task ToComposeYaml_WithMixedFlags_GeneratesCompleteYaml()
  {
    // Arrange
    var flags = new List<string> 
    { 
      "--env", "DEBUG=1", 
      "--memory", "2g", 
      "--cap-add", "SYS_PTRACE" 
    };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("environment:");
    await Assert.That(result).Contains("- DEBUG=1");
    await Assert.That(result).Contains("mem_limit: 2g");
    await Assert.That(result).Contains("cap_add:");
    await Assert.That(result).Contains("- SYS_PTRACE");
  }

  [Test]
  public async Task ToComposeYaml_WithUnknownFlag_AddsComment()
  {
    // Arrange
    var flags = new List<string> { "--unknown-flag" };

    // Act
    var result = SandboxFlags.ToComposeYaml(flags);

    // Assert
    await Assert.That(result).Contains("# Unsupported flag: --unknown-flag");
  }
}
