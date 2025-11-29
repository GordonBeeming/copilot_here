namespace CopilotHere.Tests;

public class RunConfigTests
{
    [Test]
    public async Task DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new Models.RunConfig();

        // Assert
        await Assert.That(config.ImageTag).IsEqualTo("latest");
        await Assert.That(config.SkipCleanup).IsFalse();
        await Assert.That(config.SkipPull).IsFalse();
        await Assert.That(config.ReadOnlyMounts).IsEmpty();
        await Assert.That(config.ReadWriteMounts).IsEmpty();
        await Assert.That(config.PassthroughArgs).IsEmpty();
    }

    [Test]
    public async Task ImageTag_CanBeSet()
    {
        // Arrange
        var config = new Models.RunConfig { ImageTag = "dotnet" };

        // Assert
        await Assert.That(config.ImageTag).IsEqualTo("dotnet");
    }

    [Test]
    public async Task ReadOnlyMounts_CanAddPaths()
    {
        // Arrange
        var config = new Models.RunConfig();

        // Act
        config.ReadOnlyMounts.Add("/path/to/mount");
        config.ReadOnlyMounts.Add("~/another/path");

        // Assert
        await Assert.That(config.ReadOnlyMounts).HasCount().EqualTo(2);
        await Assert.That(config.ReadOnlyMounts[0]).IsEqualTo("/path/to/mount");
        await Assert.That(config.ReadOnlyMounts[1]).IsEqualTo("~/another/path");
    }

    [Test]
    public async Task ReadWriteMounts_CanAddPaths()
    {
        // Arrange
        var config = new Models.RunConfig();

        // Act
        config.ReadWriteMounts.Add("/data/rw");

        // Assert
        await Assert.That(config.ReadWriteMounts).HasCount().EqualTo(1);
        await Assert.That(config.ReadWriteMounts[0]).IsEqualTo("/data/rw");
    }

    [Test]
    public async Task PassthroughArgs_CanAddArgs()
    {
        // Arrange
        var config = new Models.RunConfig();

        // Act
        config.PassthroughArgs.Add("-p");
        config.PassthroughArgs.Add("hello world");
        config.PassthroughArgs.Add("--model");
        config.PassthroughArgs.Add("gpt-4");

        // Assert
        await Assert.That(config.PassthroughArgs).HasCount().EqualTo(4);
    }
}
