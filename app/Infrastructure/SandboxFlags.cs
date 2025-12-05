using System.Text;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles parsing and validation of SANDBOX_FLAGS environment variable.
/// Compatible with Gemini CLI convention.
/// </summary>
public static class SandboxFlags
{
  /// <summary>
  /// Parses SANDBOX_FLAGS environment variable into a list of Docker arguments.
  /// </summary>
  public static List<string> Parse()
  {
    var flags = Environment.GetEnvironmentVariable("SANDBOX_FLAGS");
    if (string.IsNullOrWhiteSpace(flags))
      return [];

    return ShellSplit(flags.Trim());
  }

  /// <summary>
  /// Extracts --network flag value if present, otherwise returns null.
  /// </summary>
  public static string? ExtractNetwork(List<string> flags)
  {
    for (int i = 0; i < flags.Count; i++)
    {
      if ((flags[i] == "--network" || flags[i] == "--net") && i + 1 < flags.Count)
      {
        return flags[i + 1];
      }
    }
    return null;
  }

  /// <summary>
  /// Filters out network-related flags from the list.
  /// </summary>
  public static List<string> FilterNetworkFlags(List<string> flags)
  {
    var result = new List<string>();
    for (int i = 0; i < flags.Count; i++)
    {
      if (flags[i] == "--network" || flags[i] == "--net")
      {
        i++; // Skip the value too
        continue;
      }
      result.Add(flags[i]);
    }
    return result;
  }

  /// <summary>
  /// Converts Docker flags to Docker Compose YAML format.
  /// Used for Airlock mode where we inject flags into the compose file.
  /// </summary>
  public static string ToComposeYaml(List<string> flags)
  {
    if (flags.Count == 0)
      return "";

    var yaml = new StringBuilder();
    
    for (int i = 0; i < flags.Count; i++)
    {
      var flag = flags[i];
      
      if (flag == "--env" && i + 1 < flags.Count)
      {
        if (!yaml.ToString().Contains("environment:"))
          yaml.AppendLine("    environment:");
        yaml.AppendLine($"      - {flags[i + 1]}");
        i++;
      }
      else if (flag == "--cap-add" && i + 1 < flags.Count)
      {
        if (!yaml.ToString().Contains("cap_add:"))
          yaml.AppendLine("    cap_add:");
        yaml.AppendLine($"      - {flags[i + 1]}");
        i++;
      }
      else if (flag == "--cap-drop" && i + 1 < flags.Count)
      {
        if (!yaml.ToString().Contains("cap_drop:"))
          yaml.AppendLine("    cap_drop:");
        yaml.AppendLine($"      - {flags[i + 1]}");
        i++;
      }
      else if (flag == "--ulimit" && i + 1 < flags.Count)
      {
        if (!yaml.ToString().Contains("ulimits:"))
          yaml.AppendLine("    ulimits:");
        var parts = flags[i + 1].Split('=');
        if (parts.Length == 2)
          yaml.AppendLine($"      {parts[0]}: {parts[1]}");
        i++;
      }
      else if (flag == "--memory" && i + 1 < flags.Count)
      {
        yaml.AppendLine($"    mem_limit: {flags[i + 1]}");
        i++;
      }
      else if (flag == "--cpus" && i + 1 < flags.Count)
      {
        yaml.AppendLine($"    cpus: {flags[i + 1]}");
        i++;
      }
      else if (flag.StartsWith("--"))
      {
        // Unknown flag - add as comment
        DebugLogger.Log($"Unknown sandbox flag (added as comment): {flag}");
        yaml.AppendLine($"    # Unsupported flag: {flag}");
      }
    }
    
    return yaml.ToString();
  }

  /// <summary>
  /// Splits a shell-style string into arguments, respecting quotes.
  /// Example: "--network host --env VAR=value" -> ["--network", "host", "--env", "VAR=value"]
  /// </summary>
  private static List<string> ShellSplit(string input)
  {
    var args = new List<string>();
    var current = new StringBuilder();
    var inQuotes = false;
    var quoteChar = '\0';

    for (int i = 0; i < input.Length; i++)
    {
      var c = input[i];

      if (!inQuotes && (c == '"' || c == '\''))
      {
        inQuotes = true;
        quoteChar = c;
      }
      else if (inQuotes && c == quoteChar)
      {
        inQuotes = false;
        quoteChar = '\0';
      }
      else if (!inQuotes && char.IsWhiteSpace(c))
      {
        if (current.Length > 0)
        {
          args.Add(current.ToString());
          current.Clear();
        }
      }
      else
      {
        current.Append(c);
      }
    }

    if (current.Length > 0)
      args.Add(current.ToString());

    return args;
  }
}
