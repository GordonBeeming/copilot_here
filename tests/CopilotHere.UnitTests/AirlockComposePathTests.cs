using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Regression tests for #105: paths landing in the airlock docker-compose YAML
/// must keep the native Windows drive letter (C:/foo), not the /c/foo form
/// used by docker run -v. The compose YAML is read directly by the daemon,
/// which would otherwise treat /c/... as a Linux path and fail with
/// "mkdir /c: permission denied".
/// </summary>
public class AirlockComposePathTests
{
  [Test]
  public async Task ConvertToComposePath_WindowsDrivePath_KeepsDriveLetter()
  {
    if (!OperatingSystem.IsWindows()) return;

    var result = AirlockRunner.ConvertToComposePath(@"C:\Users\test\foo");

    await Assert.That(result).IsEqualTo("C:/Users/test/foo");
  }

  [Test]
  public async Task ConvertToComposePath_WindowsDifferentDrive_KeepsDriveLetter()
  {
    if (!OperatingSystem.IsWindows()) return;

    var result = AirlockRunner.ConvertToComposePath(@"D:\Projects\app");

    await Assert.That(result).IsEqualTo("D:/Projects/app");
  }

  [Test]
  public async Task ConvertToComposePath_AlreadyForwardSlashes_PassThrough()
  {
    var result = AirlockRunner.ConvertToComposePath("C:/Users/test/foo");

    await Assert.That(result).IsEqualTo("C:/Users/test/foo");
  }

  [Test]
  public async Task ConvertToComposePath_UnixPath_Unchanged()
  {
    var result = AirlockRunner.ConvertToComposePath("/home/user/foo");

    await Assert.That(result).IsEqualTo("/home/user/foo");
  }

  [Test]
  public async Task ConvertToComposePath_EmptyPath_PassThrough()
  {
    var result = AirlockRunner.ConvertToComposePath("");

    await Assert.That(result).IsEqualTo("");
  }

  [Test]
  public async Task ConvertToComposePath_DoesNotProduceLinuxStyleDrive()
  {
    if (!OperatingSystem.IsWindows()) return;

    var result = AirlockRunner.ConvertToComposePath(@"C:\Users\test\.config\copilot_here\tmp\network-123.json");

    // The legacy /c/... form was the cause of #105.
    await Assert.That(result.StartsWith("/c/")).IsFalse();
    await Assert.That(result).StartsWith("C:/");
  }
}
