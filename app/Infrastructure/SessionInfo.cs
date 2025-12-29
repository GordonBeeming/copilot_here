using System.Text;
using System.Text.Json;
using CopilotHere.Commands.Mounts;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Generates session information as JSON for use within the container.
/// </summary>
public static class SessionInfo
{
  /// <summary>
  /// Generates session info JSON string for standard Docker run mode.
  /// </summary>
  public static string Generate(
    AppContext ctx,
    string imageTag,
    string imageName,
    List<MountEntry> mounts,
    bool isYolo,
    bool airlockEnabled = false,
    string? airlockRulesPath = null)
  {
    var json = new StringBuilder();
    json.Append('{');
    json.Append($"\"copilot_here_version\":\"{BuildInfo.BuildDate}\",");
    json.Append($"\"image\":{{\"tag\":\"{imageTag}\",\"full_name\":\"{imageName}\"}},");
    json.Append($"\"mode\":\"{(isYolo ? "yolo" : "standard")}\",");
    json.Append($"\"working_directory\":\"{JsonEncode(ctx.Paths.ContainerWorkDir)}\",");
    json.Append("\"mounts\":[");
    
    for (int i = 0; i < mounts.Count; i++)
    {
      var m = mounts[i];
      if (i > 0) json.Append(',');
      json.Append('{');
      json.Append($"\"host_path\":\"{JsonEncode(m.ResolvePath(ctx.Paths.UserHome))}\",");
      json.Append($"\"container_path\":\"{JsonEncode(m.GetContainerPath(ctx.Paths.UserHome))}\",");
      json.Append($"\"mode\":\"{(m.IsReadWrite ? "rw" : "ro")}\",");
      json.Append($"\"source\":\"{m.Source.ToString().ToLowerInvariant()}\"");
      json.Append('}');
    }
    
    json.Append("],");
    json.Append($"\"airlock\":{{\"enabled\":{(airlockEnabled ? "true" : "false")},");
    json.Append($"\"rules_path\":\"{JsonEncode(airlockRulesPath ?? "")}\",");
    json.Append($"\"source\":\"{ctx.AirlockConfig.EnabledSource.ToString().ToLowerInvariant()}\"}}");
    json.Append('}');

    return json.ToString();
  }

  /// <summary>
  /// Generates extended session info JSON for Airlock mode with network config details.
  /// </summary>
  public static string GenerateWithNetworkConfig(
    AppContext ctx,
    string imageTag,
    string imageName,
    List<MountEntry> mounts,
    bool isYolo,
    string airlockRulesPath)
  {
    var baseInfo = Generate(ctx, imageTag, imageName, mounts, isYolo, true, airlockRulesPath);

    // Add network config details if rules file exists
    if (!File.Exists(airlockRulesPath))
      return baseInfo;

    try
    {
      var rulesContent = File.ReadAllText(airlockRulesPath);
      using var doc = JsonDocument.Parse(rulesContent);
      var root = doc.RootElement;

      var networkConfig = new StringBuilder();
      networkConfig.Append("\"network_config\":{");

      var hasAnyField = false;

      // Extract mode
      if (root.TryGetProperty("mode", out var modeElement))
      {
        networkConfig.Append($"\"mode\":\"{JsonEncode(modeElement.GetString() ?? "enforce")}\"");
        hasAnyField = true;
      }

      // Extract logging
      if (root.TryGetProperty("enable_logging", out var loggingElement))
      {
        if (hasAnyField) networkConfig.Append(',');
        networkConfig.Append($"\"logging_enabled\":{(loggingElement.GetBoolean() ? "true" : "false")}");
        hasAnyField = true;
      }

      // Count rules and extract sample domains
      if (root.TryGetProperty("rules", out var rulesElement) && rulesElement.ValueKind == JsonValueKind.Array)
      {
        var ruleCount = rulesElement.GetArrayLength();
        if (hasAnyField) networkConfig.Append(',');
        networkConfig.Append($"\"rules_count\":{ruleCount}");

        // Extract first few domains
        var domains = new List<string>();
        foreach (var rule in rulesElement.EnumerateArray().Take(5))
        {
          if (rule.TryGetProperty("pattern", out var pattern))
          {
            var patternStr = pattern.GetString();
            if (!string.IsNullOrEmpty(patternStr))
              domains.Add(patternStr);
          }
        }

        if (domains.Count > 0)
        {
          networkConfig.Append(",\"sample_domains\":[");
          for (int i = 0; i < domains.Count; i++)
          {
            if (i > 0) networkConfig.Append(',');
            networkConfig.Append($"\"{JsonEncode(domains[i])}\"");
          }
          networkConfig.Append(']');
        }
      }

      networkConfig.Append('}');

      // Insert network_config into airlock object
      // Remove the closing brace of airlock and root, add network_config, then close
      var insertPoint = baseInfo.LastIndexOf("}}", StringComparison.Ordinal);
      if (insertPoint > 0)
      {
        return baseInfo[..insertPoint] + "," + networkConfig + "}}";
      }
    }
    catch
    {
      // Non-fatal - return base info without network config
    }

    return baseInfo;
  }

  /// <summary>
  /// JSON-encodes a string for safe inclusion in JSON.
  /// </summary>
  private static string JsonEncode(string value)
  {
    if (string.IsNullOrEmpty(value))
      return value;

    return value
      .Replace("\\", "\\\\")
      .Replace("\"", "\\\"")
      .Replace("\n", "\\n")
      .Replace("\r", "\\r")
      .Replace("\t", "\\t");
  }
}
