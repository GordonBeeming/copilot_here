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
  public async Task EnsureBlock_StaleBlock_IsRewritten_PreservingSurroundingContent()
  {
    // An older release wrote profiles with the value of PATH baked in rather than the
    // variable, so a present marker is not proof the block on disk is the right one.
    var profile = Path.Combine(_tempDir, ".bashrc");
    File.WriteAllText(profile,
      "export EDITOR=vim\n" +
      $"{MarkerStart}\n" +
      "export PATH=\"/home/someone/.local/bin:/usr/bin:/bin\"\n" +
      $"{MarkerEnd}\n" +
      "alias g=git\n");

    var block = $"{MarkerStart}\nexport PATH=\"$HOME/.local/bin:$PATH\"\n{MarkerEnd}\n";
    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, block);
    var result = File.ReadAllText(profile);

    await Assert.That(result).Contains("export PATH=\"$HOME/.local/bin:$PATH\"");
    await Assert.That(result).DoesNotContain("/home/someone/.local/bin");
    await Assert.That(result).Contains("export EDITOR=vim");
    await Assert.That(result).Contains("alias g=git");
  }

  [Test]
  public async Task EnsureBlock_CurrentBlock_LeavesFileUnchanged()
  {
    var profile = Path.Combine(_tempDir, ".bashrc");
    var block = $"{MarkerStart}\nexport PATH=\"$HOME/.local/bin:$PATH\"\n{MarkerEnd}\n";
    var original = $"export EDITOR=vim\n{block}alias g=git\n";
    File.WriteAllText(profile, original);

    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, block);

    await Assert.That(File.ReadAllText(profile)).IsEqualTo(original);
  }

  [Test]
  public async Task EnsureBlock_MissingEndMarker_LeavesFileUnchanged()
  {
    // Without an end marker the block's extent is unknowable, so rewriting could
    // swallow whatever the user has below the start marker.
    var profile = Path.Combine(_tempDir, ".bashrc");
    var original = $"export EDITOR=vim\n{MarkerStart}\nexport PATH=\"/frozen:/usr/bin\"\nalias g=git\n";
    File.WriteAllText(profile, original);

    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, $"{MarkerStart}\nfresh\n{MarkerEnd}\n");

    await Assert.That(File.ReadAllText(profile)).IsEqualTo(original);
  }

  [Test]
  public async Task EnsureBlock_NoMarker_AppendsBlock()
  {
    var profile = Path.Combine(_tempDir, ".bashrc");
    File.WriteAllText(profile, "export EDITOR=vim\n");

    var block = $"{MarkerStart}\nexport PATH=\"$HOME/.local/bin:$PATH\"\n{MarkerEnd}\n";
    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, block);
    var result = File.ReadAllText(profile);

    await Assert.That(result).Contains("export EDITOR=vim");
    await Assert.That(result).Contains(block);
  }

  [Test]
  public async Task EnsureBlock_StaleBlock_PreservesUtf8Bom()
  {
    // install.ps1 writes the PowerShell profile as UTF-8 with a BOM so Windows PowerShell
    // 5.1 recognizes the encoding instead of falling back to the legacy code page. A rewrite
    // of the marked region must not silently drop that BOM.
    var profile = Path.Combine(_tempDir, "profile.ps1");
    var bomEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    File.WriteAllText(profile,
      "# café notes\n" +
      $"{MarkerStart}\n" +
      "$env:PATH = \"C:\\old\\bin;$env:PATH\"\n" +
      $"{MarkerEnd}\n",
      bomEncoding);

    var block = $"{MarkerStart}\n$env:PATH = \"$HOME\\.local\\bin;$env:PATH\"\n{MarkerEnd}\n";
    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, block);

    var rewrittenBytes = await File.ReadAllBytesAsync(profile);
    await Assert.That(rewrittenBytes[0]).IsEqualTo((byte)0xEF);
    await Assert.That(rewrittenBytes[1]).IsEqualTo((byte)0xBB);
    await Assert.That(rewrittenBytes[2]).IsEqualTo((byte)0xBF);

    var result = File.ReadAllText(profile);
    await Assert.That(result).Contains("café notes");
    await Assert.That(result).Contains("$HOME\\.local\\bin");
  }

  [Test]
  public async Task EnsureBlock_StaleBlock_NoOriginalBom_WritesWithoutBom()
  {
    // Unix profiles (.bashrc/.zshrc) are never BOM'd on disk; a rewrite must not introduce
    // one, since a leading BOM would corrupt the shell's parsing of the file.
    var profile = Path.Combine(_tempDir, ".bashrc");
    File.WriteAllText(profile,
      "export EDITOR=vim\n" +
      $"{MarkerStart}\n" +
      "export PATH=\"/frozen:/usr/bin\"\n" +
      $"{MarkerEnd}\n");

    var block = $"{MarkerStart}\nexport PATH=\"$HOME/.local/bin:$PATH\"\n{MarkerEnd}\n";
    ShellIntegration.EnsureBlock(profile, MarkerStart, MarkerEnd, block);

    var rewrittenBytes = await File.ReadAllBytesAsync(profile);
    var startsWithBom = rewrittenBytes.Length >= 3
      && rewrittenBytes[0] == 0xEF && rewrittenBytes[1] == 0xBB && rewrittenBytes[2] == 0xBF;
    await Assert.That(startsWithBom).IsFalse();
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
