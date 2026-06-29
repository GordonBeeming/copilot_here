using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ShellIntegrationTests
{
  private const string MarkerStart = "# >>> copilot_here >>>";
  private const string MarkerEnd = "# <<< copilot_here <<<";
  private const string Token = ".copilot_here.sh";

  private string _tempDir = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_uninstall_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task RemoveBlock_StripsMarkerBlock_PreservesSurroundingContent()
  {
    var profile = Path.Combine(_tempDir, ".zshrc");
    var content =
      "export EDITOR=vim\n" +
      "alias g=git\n" +
      "\n" +
      $"{MarkerStart}\n" +
      "if [ -d \"$HOME/.local/bin\" ]; then\n" +
      "  export PATH=\"$HOME/.local/bin:$PATH\"\n" +
      "fi\n" +
      "source \"$HOME/.copilot_here.sh\"\n" +
      $"{MarkerEnd}\n" +
      "\n" +
      "export FOO=bar\n";
    File.WriteAllText(profile, content);

    var changed = ShellIntegration.RemoveBlock(profile, MarkerStart, MarkerEnd, Token);
    var result = File.ReadAllText(profile);

    await Assert.That(changed).IsTrue();
    await Assert.That(result).Contains("export EDITOR=vim");
    await Assert.That(result).Contains("alias g=git");
    await Assert.That(result).Contains("export FOO=bar");
    await Assert.That(result).DoesNotContain(MarkerStart);
    await Assert.That(result).DoesNotContain(MarkerEnd);
    await Assert.That(result).DoesNotContain(".copilot_here.sh");
  }

  [Test]
  public async Task RemoveBlock_RemovesStraySourcingLine_WithoutMarkers()
  {
    var profile = Path.Combine(_tempDir, ".bashrc");
    File.WriteAllText(profile, "alias ll=\"ls -la\"\nsource \"$HOME/.copilot_here.sh\"\nexport BAZ=1\n");

    var changed = ShellIntegration.RemoveBlock(profile, MarkerStart, MarkerEnd, Token);
    var result = File.ReadAllText(profile);

    await Assert.That(changed).IsTrue();
    await Assert.That(result).Contains("alias ll=\"ls -la\"");
    await Assert.That(result).Contains("export BAZ=1");
    await Assert.That(result).DoesNotContain(".copilot_here.sh");
  }

  [Test]
  public async Task RemoveBlock_MalformedBlock_MissingEndMarker_PreservesUserContent()
  {
    // A start marker with no matching end marker must NOT swallow the rest of the file.
    var profile = Path.Combine(_tempDir, ".zshrc");
    var content =
      "export EDITOR=vim\n" +
      $"{MarkerStart}\n" +
      "source \"$HOME/.copilot_here.sh\"\n" +
      "export FOO=bar\n" +
      "alias g=git\n";
    File.WriteAllText(profile, content);

    var changed = ShellIntegration.RemoveBlock(profile, MarkerStart, MarkerEnd, Token);
    var result = File.ReadAllText(profile);

    await Assert.That(changed).IsTrue();
    await Assert.That(result).Contains("export EDITOR=vim");
    await Assert.That(result).Contains("export FOO=bar");
    await Assert.That(result).Contains("alias g=git");
    await Assert.That(result).DoesNotContain(MarkerStart);
    await Assert.That(result).DoesNotContain(".copilot_here.sh");
  }

  [Test]
  public async Task RemoveBlock_NoMarkersOrStrayLines_LeavesFileUnchanged()
  {
    var profile = Path.Combine(_tempDir, ".zshrc");
    var original = "export EDITOR=vim\nalias g=git\n";
    File.WriteAllText(profile, original);

    var changed = ShellIntegration.RemoveBlock(profile, MarkerStart, MarkerEnd, Token);

    await Assert.That(changed).IsFalse();
    await Assert.That(File.ReadAllText(profile)).IsEqualTo(original);
  }

  [Test]
  public async Task RemoveBlock_MissingFile_ReturnsFalse()
  {
    var profile = Path.Combine(_tempDir, "does-not-exist");

    var changed = ShellIntegration.RemoveBlock(profile, MarkerStart, MarkerEnd, Token);

    await Assert.That(changed).IsFalse();
  }

  [Test]
  public async Task BuildCmdWrapper_UsesArgsSplatForForwarding()
  {
    // Act
    var wrapper = ShellIntegration.BuildCmdWrapper("copilot_yolo", "ignored");

    // Assert
    await Assert.That(wrapper).Contains("copilot_yolo @args");
    await Assert.That(wrapper).Contains(" -- %*");
  }

  [Test]
  public async Task BuildCmdWrapper_UsesArgsSplatForBothPwshAndWindowsPowerShell()
  {
    // Act
    var wrapper = ShellIntegration.BuildCmdWrapper("copilot_here", "ignored");

    // Assert
    var occurrences = wrapper.Split("copilot_here @args").Length - 1;
    await Assert.That(occurrences).IsEqualTo(2);
  }
}
