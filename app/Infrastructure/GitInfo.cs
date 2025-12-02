using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Extracts GitHub repository information from git remote.
/// </summary>
public static class GitInfo
{
  /// <summary>
  /// Gets the GitHub owner and repo from the current git remote.
  /// Returns null if not a GitHub repository or no remote configured.
  /// </summary>
  public static (string Owner, string Repo)? GetGitHubInfo()
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = "git",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("remote");
      startInfo.ArgumentList.Add("get-url");
      startInfo.ArgumentList.Add("origin");

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var output = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();

      if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
        return null;

      return ParseGitHubUrl(output);
    }
    catch
    {
      return null;
    }
  }

  /// <summary>
  /// Parses GitHub owner/repo from various URL formats:
  /// - git@github.com:owner/repo.git
  /// - https://github.com/owner/repo.git
  /// - https://github.com/owner/repo
  /// </summary>
  private static (string Owner, string Repo)? ParseGitHubUrl(string url)
  {
    // SSH format: git@github.com:owner/repo.git
    if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
    {
      var path = url["git@github.com:".Length..];
      return ParseOwnerRepo(path);
    }

    // HTTPS format: https://github.com/owner/repo.git
    if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
    {
      var path = url["https://github.com/".Length..];
      return ParseOwnerRepo(path);
    }

    // HTTP format: http://github.com/owner/repo.git
    if (url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
    {
      var path = url["http://github.com/".Length..];
      return ParseOwnerRepo(path);
    }

    return null;
  }

  private static (string Owner, string Repo)? ParseOwnerRepo(string path)
  {
    // Remove .git suffix
    if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
      path = path[..^4];

    var parts = path.Split('/');
    if (parts.Length < 2) return null;

    var owner = parts[0];
    var repo = parts[1];

    if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
      return null;

    return (owner, repo);
  }
}
