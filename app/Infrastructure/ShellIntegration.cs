using System.Net.Http;

namespace CopilotHere.Infrastructure;

public static class ShellIntegration
{
  private const string ReleaseUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest";

  private const string ShMarkerStart = "# >>> copilot_here >>>";
  private const string ShMarkerEnd = "# <<< copilot_here <<<";

  private const string PsMarkerStart = "# >>> copilot_here >>>";
  private const string PsMarkerEnd = "# <<< copilot_here <<<";

  public static bool ArgsContainInstallShells(string[] args)
  {
    return args.Any(a => a.Equals("--install-shells", StringComparison.OrdinalIgnoreCase));
  }

  public static bool ShouldWarn(string[] args)
  {
    if (Console.IsOutputRedirected)
    {
      return false;
    }

    if (ArgsContainInstallShells(args))
    {
      return false;
    }

    // Don’t warn on help.
    if (args.Any(a => a is "--help" or "-h" or "-?" or "--help2"))
    {
      return false;
    }

    return true;
  }

  public static IReadOnlyList<string> GetMissingTargets(string userHome)
  {
    if (OperatingSystem.IsWindows())
    {
      return GetMissingTargetsWindows(userHome);
    }

    return GetMissingTargetsUnix(userHome);
  }

  public static void WarnIfMissing(string appName, string[] args)
  {
    if (!ShouldWarn(args))
    {
      return;
    }

    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var missing = GetMissingTargets(userHome);

    if (missing.Count == 0)
    {
      return;
    }

    Console.Error.WriteLine($"⚠️  Shell integration not installed for: {string.Join(", ", missing)}");
    Console.Error.WriteLine($"   Run: {appName} --install-shells");
    Console.Error.WriteLine();
  }

  public static int InstallAll()
  {
    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    try
    {
      if (OperatingSystem.IsWindows())
      {
        InstallWindows(userHome);
      }
      else
      {
        InstallUnix(userHome);
      }

      Console.WriteLine("✅ Shell integration installed.");
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"❌ Failed to install shell integration: {ex.Message}");
      return 1;
    }
  }

  private static IReadOnlyList<string> GetMissingTargetsUnix(string userHome)
  {
    var missing = new List<string>();

    var shPath = Path.Combine(userHome, ".copilot_here.sh");

    // bash
    var bashProfiles = new[]
    {
      Path.Combine(userHome, ".bashrc"),
      Path.Combine(userHome, ".bash_profile"),
      Path.Combine(userHome, ".profile"),
    };

    // zsh
    var zshProfiles = new[]
    {
      Path.Combine(userHome, ".zshrc"),
      Path.Combine(userHome, ".zprofile"),
    };

    var hasAnyShellInstall = File.Exists(shPath) ||
                            bashProfiles.Any(p => File.Exists(p) && (FileContains(p, ShMarkerStart) || FileContains(p, ".copilot_here.sh"))) ||
                            zshProfiles.Any(p => File.Exists(p) && (FileContains(p, ShMarkerStart) || FileContains(p, ".copilot_here.sh")));

    if (!hasAnyShellInstall)
    {
      return missing;
    }

    var bashInstalled = bashProfiles.Any(p => File.Exists(p) && (FileContains(p, ShMarkerStart) || FileContains(p, ".copilot_here.sh")));
    if (!bashInstalled)
    {
      missing.Add("bash");
    }

    var zshInstalled = zshProfiles.Any(p => File.Exists(p) && (FileContains(p, ShMarkerStart) || FileContains(p, ".copilot_here.sh")));
    if (!zshInstalled)
    {
      missing.Add("zsh");
    }

    // fish
    var fishDir = Path.Combine(userHome, ".config", "fish");
    var fishConf = Path.Combine(fishDir, "conf.d", "copilot_here.fish");
    var fishInstalled = File.Exists(fishConf) && FileContains(fishConf, ShMarkerStart);

    if (!fishInstalled)
    {
      missing.Add("fish");
    }

    var localBin = Path.Combine(userHome, ".local", "bin");
    var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    if (!envPath.Contains(localBin, StringComparison.OrdinalIgnoreCase))
    {
      missing.Add("PATH");
    }

    return missing;
  }

  private static IReadOnlyList<string> GetMissingTargetsWindows(string userHome)
  {
    var missing = new List<string>();

    var psScriptPath = Path.Combine(userHome, ".copilot_here.ps1");

    var pwshProfile = Path.Combine(userHome, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1");
    var winPsProfile = Path.Combine(userHome, "Documents", "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1");

    var hasAnyPowerShellInstall = File.Exists(psScriptPath) ||
                                 (File.Exists(pwshProfile) && (FileContains(pwshProfile, PsMarkerStart) || FileContains(pwshProfile, ".copilot_here.ps1"))) ||
                                 (File.Exists(winPsProfile) && (FileContains(winPsProfile, PsMarkerStart) || FileContains(winPsProfile, ".copilot_here.ps1")));

    if (!hasAnyPowerShellInstall)
    {
      return missing;
    }

    var pwshInstalled = File.Exists(pwshProfile) && (FileContains(pwshProfile, PsMarkerStart) || FileContains(pwshProfile, ".copilot_here.ps1"));
    if (!pwshInstalled)
    {
      missing.Add("pwsh");
    }

    var winPsInstalled = File.Exists(winPsProfile) && (FileContains(winPsProfile, PsMarkerStart) || FileContains(winPsProfile, ".copilot_here.ps1"));
    if (!winPsInstalled)
    {
      missing.Add("powershell");
    }

    // cmd wrappers
    var cmdBinDir = Path.Combine(userHome, ".local", "bin");
    var cmd1 = Path.Combine(cmdBinDir, "copilot_here.cmd");
    var cmd2 = Path.Combine(cmdBinDir, "copilot_yolo.cmd");

    if (!File.Exists(cmd1) || !File.Exists(cmd2))
    {
      missing.Add("cmd");
    }

    var envPath = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
    if (!envPath.Contains(cmdBinDir, StringComparison.OrdinalIgnoreCase))
    {
      missing.Add("PATH");
    }

    return missing;
  }

  private static void InstallUnix(string userHome)
  {
    var shPath = Path.Combine(userHome, ".copilot_here.sh");
    EnsureDownloadedIfMissing($"{ReleaseUrl}/copilot_here.sh", shPath);

    EnsureSourcedInUnixProfile(GetBestBashProfile(userHome), shPath);
    EnsureSourcedInUnixProfile(GetBestZshProfile(userHome), shPath);
    EnsureFishWrapper(userHome, shPath);
  }

  private static void InstallWindows(string userHome)
  {
    var psPath = Path.Combine(userHome, ".copilot_here.ps1");
    EnsureDownloadedIfMissing($"{ReleaseUrl}/copilot_here.ps1", psPath);

    var pwshProfile = Path.Combine(userHome, "Documents", "PowerShell", "Microsoft.PowerShell_profile.ps1");
    var winPsProfile = Path.Combine(userHome, "Documents", "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1");

    EnsureDotSourcedInPowerShellProfile(pwshProfile, psPath);
    EnsureDotSourcedInPowerShellProfile(winPsProfile, psPath);

    var binDir = Path.Combine(userHome, ".local", "bin");
    Directory.CreateDirectory(binDir);
    WindowsUserPath.EnsureUserPathContains(binDir);

    EnsureCmdWrappers(userHome, psPath);
  }

  private static string GetBestBashProfile(string userHome)
  {
    var bashrc = Path.Combine(userHome, ".bashrc");
    if (File.Exists(bashrc)) return bashrc;

    var bashProfile = Path.Combine(userHome, ".bash_profile");
    if (File.Exists(bashProfile)) return bashProfile;

    var profile = Path.Combine(userHome, ".profile");
    if (File.Exists(profile)) return profile;

    return bashrc;
  }

  private static string GetBestZshProfile(string userHome)
  {
    var zshrc = Path.Combine(userHome, ".zshrc");
    if (File.Exists(zshrc)) return zshrc;

    var zprofile = Path.Combine(userHome, ".zprofile");
    if (File.Exists(zprofile)) return zprofile;

    return zshrc;
  }

  private static void EnsureSourcedInUnixProfile(string profilePath, string shPath)
  {
    var block = $"{ShMarkerStart}\n" +
                "# Ensure user bin directory is on PATH\n" +
                "if [ -d \"$HOME/.local/bin\" ]; then\n" +
                "  case \":$PATH:\" in\n" +
                "    *\":$HOME/.local/bin:\"*) ;;\n" +
                "    *) export PATH=\"$HOME/.local/bin:$PATH\" ;;\n" +
                "  esac\n" +
                "fi\n" +
                $"if [ -f \"$HOME/.copilot_here.sh\" ]; then\n" +
                $"  source \"$HOME/.copilot_here.sh\"\n" +
                $"fi\n" +
                $"{ShMarkerEnd}\n";

    EnsureBlock(profilePath, ShMarkerStart, ShMarkerEnd, block);
  }

  private static void EnsureFishWrapper(string userHome, string shPath)
  {
    var fishDir = Path.Combine(userHome, ".config", "fish", "conf.d");
    Directory.CreateDirectory(fishDir);

    var fishConf = Path.Combine(fishDir, "copilot_here.fish");

    var content = $"{ShMarkerStart}\n" +
                  "# Ensure user bin directory is on PATH\n" +
                  "if test -d $HOME/.local/bin\n" +
                  "  if not contains -- $HOME/.local/bin $PATH\n" +
                  "    set -gx PATH $HOME/.local/bin $PATH\n" +
                  "  end\n" +
                  "end\n" +
                  "function copilot_here\n" +
                  "  bash -lc 'export PATH=\"$HOME/.local/bin:$PATH\"; source \"$HOME/.copilot_here.sh\"; copilot_here \"$@\"' bash $argv\n" +
                  "end\n" +
                  "function copilot_yolo\n" +
                  "  bash -lc 'export PATH=\"$HOME/.local/bin:$PATH\"; source \"$HOME/.copilot_here.sh\"; copilot_yolo \"$@\"' bash $argv\n" +
                  "end\n" +
                  $"{ShMarkerEnd}\n";

    if (!File.Exists(fishConf) || !FileContains(fishConf, ShMarkerStart))
    {
      File.WriteAllText(fishConf, content);
    }
  }

  private static void EnsureDotSourcedInPowerShellProfile(string profilePath, string psPath)
  {
    var block = $"{PsMarkerStart}\r\n" +
                "$ErrorActionPreference = 'SilentlyContinue'\r\n" +
                "$binDir = Join-Path $env:USERPROFILE '.local\\bin'\r\n" +
                "if (Test-Path $binDir) {\r\n" +
                "  $parts = $env:Path -split ';'\r\n" +
                "  if ($parts -notcontains $binDir) { $env:Path = \"$binDir;$env:Path\" }\r\n" +
                "}\r\n" +
                $"$scriptPath = \"{psPath.Replace("\\", "\\\\")}\"\r\n" +
                "if (Test-Path $scriptPath) { . $scriptPath }\r\n" +
                $"{PsMarkerEnd}\r\n";

    EnsureBlock(profilePath, PsMarkerStart, PsMarkerEnd, block);
  }

  private static void EnsureCmdWrappers(string userHome, string psPath)
  {
    var binDir = Path.Combine(userHome, ".local", "bin");
    Directory.CreateDirectory(binDir);

    var hereCmd = Path.Combine(binDir, "copilot_here.cmd");
    var yoloCmd = Path.Combine(binDir, "copilot_yolo.cmd");

    File.WriteAllText(hereCmd, BuildCmdWrapper("copilot_here", psPath));
    File.WriteAllText(yoloCmd, BuildCmdWrapper("copilot_yolo", psPath));
  }

  internal static string BuildCmdWrapper(string functionName, string psPath)
  {
    // Prefer pwsh, fall back to powershell.
    // Pass arguments after `--` and forward via PowerShell `@args` so quoted
    // values (for example --prompt "What is 1 + 1 ?") stay as a single arg.
    return "@echo off\r\n" +
           "setlocal\r\n" +
           "set \"SCRIPT=%USERPROFILE%\\.copilot_here.ps1\"\r\n" +
           "where pwsh >nul 2>nul\r\n" +
           "if %ERRORLEVEL%==0 (\r\n" +
           $"  pwsh -NoProfile -ExecutionPolicy Bypass -Command \"& {{ . '%USERPROFILE%\\.copilot_here.ps1'; {functionName} @args }}\" -- %*\r\n" +
           ") else (\r\n" +
           $"  powershell -NoProfile -ExecutionPolicy Bypass -Command \"& {{ . '%USERPROFILE%\\.copilot_here.ps1'; {functionName} @args }}\" -- %*\r\n" +
           ")\r\n" +
           "endlocal\r\n";
  }

  private static void EnsureDownloadedIfMissing(string url, string destinationPath)
  {
    if (File.Exists(destinationPath))
    {
      return;
    }

    var destDir = Path.GetDirectoryName(destinationPath);
    if (!string.IsNullOrWhiteSpace(destDir))
    {
      Directory.CreateDirectory(destDir);
    }

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var content = http.GetStringAsync(url).GetAwaiter().GetResult();
    File.WriteAllText(destinationPath, content);
  }

  private static void EnsureBlock(string filePath, string markerStart, string markerEnd, string block)
  {
    var dir = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrWhiteSpace(dir))
    {
      Directory.CreateDirectory(dir);
    }

    var existing = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;

    if (existing.Contains(markerStart, StringComparison.Ordinal))
    {
      return;
    }

    var prefix = string.IsNullOrWhiteSpace(existing) || existing.EndsWith("\n") || existing.EndsWith("\r\n")
      ? string.Empty
      : Environment.NewLine;

    File.AppendAllText(filePath, prefix + Environment.NewLine + block);
  }

  private static bool FileContains(string path, string value)
  {
    try
    {
      return File.ReadAllText(path).Contains(value, StringComparison.Ordinal);
    }
    catch
    {
      return false;
    }
  }
}
