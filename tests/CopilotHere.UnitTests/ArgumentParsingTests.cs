namespace CopilotHere.Tests;

public class ArgumentParsingTests
{
    [Test]
    public async Task EmptyArgs_ReturnsDefaultConfig()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ImageTag).IsEqualTo("latest");
        await Assert.That(config.SkipCleanup).IsFalse();
        await Assert.That(config.SkipPull).IsFalse();
    }

    [Test]
    [Arguments("-d")]
    [Arguments("--dotnet")]
    public async Task DotnetFlag_SetsImageTag(string flag)
    {
        // Arrange
        var args = new[] { flag };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ImageTag).IsEqualTo("dotnet");
    }

    [Test]
    public async Task NoCleanupFlag_SetsSkipCleanup()
    {
        // Arrange
        var args = new[] { "--no-cleanup" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.SkipCleanup).IsTrue();
    }

    [Test]
    [Arguments("--no-pull")]
    [Arguments("--skip-pull")]
    public async Task NoPullFlag_SetsSkipPull(string flag)
    {
        // Arrange
        var args = new[] { flag };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.SkipPull).IsTrue();
    }

    [Test]
    public async Task MountFlag_AddsReadOnlyMount()
    {
        // Arrange
        var args = new[] { "--mount", "/path/to/dir" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ReadOnlyMounts).HasCount().EqualTo(1);
        await Assert.That(config.ReadOnlyMounts[0]).IsEqualTo("/path/to/dir");
    }

    [Test]
    public async Task MountRwFlag_AddsReadWriteMount()
    {
        // Arrange
        var args = new[] { "--mount-rw", "/data/rw" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ReadWriteMounts).HasCount().EqualTo(1);
        await Assert.That(config.ReadWriteMounts[0]).IsEqualTo("/data/rw");
    }

    [Test]
    public async Task MultipleMounts_AddsAll()
    {
        // Arrange
        var args = new[] { "--mount", "/path1", "--mount-rw", "/path2", "--mount", "/path3" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ReadOnlyMounts).HasCount().EqualTo(2);
        await Assert.That(config.ReadWriteMounts).HasCount().EqualTo(1);
    }

    [Test]
    public async Task UnknownFlags_PassedThrough()
    {
        // Arrange
        var args = new[] { "-p", "hello world", "--model", "gpt-4" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.PassthroughArgs).HasCount().EqualTo(4);
        await Assert.That(config.PassthroughArgs[0]).IsEqualTo("-p");
        await Assert.That(config.PassthroughArgs[1]).IsEqualTo("hello world");
    }

    [Test]
    public async Task MixedArgs_ParsedCorrectly()
    {
        // Arrange
        var args = new[] { "-d", "--no-pull", "--mount", "/data", "-p", "test prompt" };

        // Act
        var config = ParseArguments(args);

        // Assert
        await Assert.That(config.ImageTag).IsEqualTo("dotnet");
        await Assert.That(config.SkipPull).IsTrue();
        await Assert.That(config.ReadOnlyMounts).HasCount().EqualTo(1);
        await Assert.That(config.PassthroughArgs).Contains("-p");
        await Assert.That(config.PassthroughArgs).Contains("test prompt");
    }

    // Helper method that mirrors the parsing logic from Program.cs
    // TODO: Extract this to a shared class for both app and tests
    private static Models.RunConfig ParseArguments(string[] args)
    {
        var config = new Models.RunConfig();
        var i = 0;

        while (i < args.Length)
        {
            var arg = args[i];

            if (!arg.StartsWith("-"))
            {
                config.PassthroughArgs.Add(arg);
                i++;
                continue;
            }

            switch (arg)
            {
                case "--dotnet":
                case "-d":
                    config.ImageTag = "dotnet";
                    break;
                case "--no-cleanup":
                    config.SkipCleanup = true;
                    break;
                case "--no-pull":
                case "--skip-pull":
                    config.SkipPull = true;
                    break;
                case "--mount":
                    if (i + 1 < args.Length) config.ReadOnlyMounts.Add(args[++i]);
                    break;
                case "--mount-rw":
                    if (i + 1 < args.Length) config.ReadWriteMounts.Add(args[++i]);
                    break;
                default:
                    config.PassthroughArgs.Add(arg);
                    break;
            }
            i++;
        }
        return config;
    }
}
