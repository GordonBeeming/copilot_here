using System.Reflection;

namespace CopilotHere.Infrastructure;

public static class BuildInfo
{
  public static string BuildDate { get; } = ComputeBuildDate();

  private static string ComputeBuildDate()
  {
    var informational = typeof(BuildInfo).Assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
      ?.InformationalVersion;

    if (string.IsNullOrEmpty(informational))
    {
      return "0.0.0-dev";
    }

    // Strip the git-sha suffix appended by CopilotHere.csproj
    // (`<InformationalVersion>$(Version).$(GitSha)</InformationalVersion>`)
    // so consumers see just the YYYY.MM.DD.N portion. Stop at the 5th segment,
    // a '+' (SemVer build metadata), or a '-' (pre-release tag like `0.0.0-dev`).
    var dots = 0;
    for (var i = 0; i < informational.Length; i++)
    {
      var c = informational[i];
      if (c == '+' || c == '-')
      {
        return informational[..i];
      }
      if (c == '.')
      {
        dots++;
        if (dots == 4)
        {
          return informational[..i];
        }
      }
    }
    return informational;
  }
}
