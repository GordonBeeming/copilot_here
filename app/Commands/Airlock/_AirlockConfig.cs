using System.Text;
using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Configuration for the Airlock network proxy.
/// The enabled flag is read from within the network.json file.
/// Local rules override global rules entirely (no merge).
/// </summary>
public sealed record AirlockConfig
{
  /// <summary>Whether Airlock is enabled.</summary>
  public required bool Enabled { get; init; }

  /// <summary>Source of the enabled flag.</summary>
  public required AirlockConfigSource EnabledSource { get; init; }

  /// <summary>Path to the active rules file (local or global).</summary>
  public string? RulesPath { get; init; }

  /// <summary>Source of the rules file.</summary>
  public AirlockConfigSource RulesSource { get; init; }

  private const string RulesFileName = "network.json";

  /// <summary>UTF-8 BOM. Some Windows editors add one; it must survive a rewrite.</summary>
  private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

  /// <summary>
  /// Matches the deserializer's leniency, so any file that loads can also be toggled.
  /// </summary>
  private static readonly JsonReaderOptions ReaderOptions = new()
  {
    CommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  /// <summary>
  /// Loads Airlock configuration from config files.
  /// The enabled flag is read from within the network.json file.
  /// </summary>
  public static AirlockConfig Load(AppPaths paths)
  {
    // Find rules file (local overrides global entirely)
    string? rulesPath = null;
    var rulesSource = AirlockConfigSource.Default;

    var localRulesPath = paths.GetLocalPath(RulesFileName);
    var globalRulesPath = paths.GetGlobalPath(RulesFileName);

    if (File.Exists(localRulesPath))
    {
      rulesPath = localRulesPath;
      rulesSource = AirlockConfigSource.Local;
    }
    else if (File.Exists(globalRulesPath))
    {
      rulesPath = globalRulesPath;
      rulesSource = AirlockConfigSource.Global;
    }

    // Read enabled flag from the JSON file
    var enabled = false;
    if (rulesPath != null)
    {
      var config = ReadNetworkConfig(rulesPath);
      enabled = config?.Enabled ?? false;
    }

    return new AirlockConfig
    {
      Enabled = enabled,
      EnabledSource = rulesSource,
      RulesPath = rulesPath,
      RulesSource = rulesSource
    };
  }

  /// <summary>
  /// Reads a NetworkConfig from a JSON file.
  /// Returns null if the file doesn't exist.
  /// Throws JsonException if the file contains invalid JSON.
  /// </summary>
  internal static NetworkConfig? ReadNetworkConfig(string path)
  {
    if (!File.Exists(path))
      return null;

    using var stream = File.OpenRead(path);
    return JsonSerializer.Deserialize(stream, NetworkConfigJsonContext.Default.NetworkConfig);
  }

  /// <summary>
  /// Writes a NetworkConfig to a JSON file.
  /// </summary>
  internal static void WriteNetworkConfig(string path, NetworkConfig config)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var stream = File.Create(path);
    JsonSerializer.Serialize(stream, config, NetworkConfigJsonContext.Default.NetworkConfig);
  }

  /// <summary>Enables Airlock in local config by setting enabled:true in network.json.</summary>
  public static AirlockToggleOutcome EnableLocal(AppPaths paths) => SetEnabledLocal(paths, true);

  /// <summary>Enables Airlock in global config by setting enabled:true in network.json.</summary>
  public static AirlockToggleOutcome EnableGlobal(AppPaths paths) => SetEnabledGlobal(paths, true);

  /// <summary>Disables Airlock in local config by setting enabled:false in network.json.</summary>
  public static AirlockToggleOutcome DisableLocal(AppPaths paths) => SetEnabledLocal(paths, false);

  /// <summary>Disables Airlock in global config by setting enabled:false in network.json.</summary>
  public static AirlockToggleOutcome DisableGlobal(AppPaths paths) => SetEnabledGlobal(paths, false);

  private static AirlockToggleOutcome SetEnabledLocal(AppPaths paths, bool enabled) =>
    SetEnabledInJson(paths.GetLocalPath(RulesFileName), enabled, seedFrom: paths.GetGlobalPath(RulesFileName));

  private static AirlockToggleOutcome SetEnabledGlobal(AppPaths paths, bool enabled) =>
    SetEnabledInJson(paths.GetGlobalPath(RulesFileName), enabled, seedFrom: null);

  /// <summary>
  /// Sets the enabled flag in a network.json file, leaving every other byte of the
  /// file alone. Comments, key order, formatting and keys this app doesn't model all
  /// survive, because a rules file people hand-edit shouldn't be reformatted under them.
  /// </summary>
  /// <param name="seedFrom">
  /// Config file to copy when <paramref name="path"/> doesn't exist yet. A local file
  /// shadows the global one entirely (see <see cref="Load"/>), so creating an empty
  /// local file would silently drop the user out of their global ruleset.
  /// </param>
  private static AirlockToggleOutcome SetEnabledInJson(string path, bool enabled, string? seedFrom)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    if (File.Exists(path))
    {
      WriteEnabled(path, readFrom: path, enabled);
      return AirlockToggleOutcome.UpdatedExisting;
    }

    if (!string.IsNullOrEmpty(seedFrom) && File.Exists(seedFrom))
    {
      WriteEnabled(path, readFrom: seedFrom, enabled);
      return AirlockToggleOutcome.SeededFromGlobal;
    }

    WriteNetworkConfig(path, NetworkConfig.CreateDefault(enabled));
    return AirlockToggleOutcome.CreatedDefault;
  }

  /// <summary>
  /// Rewrites <paramref name="readFrom"/>'s bytes with the new enabled flag and saves
  /// them to <paramref name="path"/>. The whole edit happens in memory first, so a file
  /// that fails to parse is never partially written. When the two paths differ, a broken
  /// source leaves no new file behind at all.
  /// </summary>
  private static void WriteEnabled(string path, string readFrom, bool enabled)
  {
    byte[] updated;
    try
    {
      updated = SetEnabledInJsonBytes(File.ReadAllBytes(readFrom), enabled);
    }
    catch (JsonException ex)
    {
      throw new JsonException($"{readFrom}: {ex.Message}", ex);
    }

    File.WriteAllBytes(path, updated);
  }

  /// <summary>
  /// Returns <paramref name="json"/> with the root object's "enabled" value replaced,
  /// inserting the property if it isn't there. Every other byte is copied verbatim.
  /// </summary>
  internal static byte[] SetEnabledInJsonBytes(byte[] json, bool enabled)
  {
    var bomLength = json.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
    var body = json.AsSpan(bomLength);

    // The search below stops as soon as it finds "enabled". Without a full pass first,
    // a file that is broken further down would still get rewritten, handing the user
    // back a config that won't load.
    ValidateJson(body);

    ReadOnlySpan<byte> replacement = enabled ? "true"u8 : "false"u8;

    var existing = FindRootEnabledValue(body);
    if (existing is var (start, length))
      return Splice(json, bomLength + start, length, replacement);

    return InsertRootEnabled(json, bomLength, replacement);
  }

  /// <summary>Throws JsonException if the document isn't well-formed.</summary>
  private static void ValidateJson(ReadOnlySpan<byte> body)
  {
    var reader = new Utf8JsonReader(body, ReaderOptions);
    while (reader.Read())
    {
    }
  }

  /// <summary>
  /// Locates the value of the root object's "enabled" property.
  /// Offsets are relative to <paramref name="body"/>. Returns null when absent.
  /// </summary>
  private static (int Start, int Length)? FindRootEnabledValue(ReadOnlySpan<byte> body)
  {
    var reader = new Utf8JsonReader(body, ReaderOptions);
    (int Start, int Length)? match = null;

    while (reader.Read())
    {
      // Depth 1 is a property of the root object. Without this check, a host or
      // path containing "enabled" inside allowed_rules would match instead.
      if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
        continue;

      if (!reader.ValueTextEquals("enabled"u8))
        continue;

      if (!reader.Read())
        break;

      if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
      {
        throw new JsonException(
          "The \"enabled\" property must be a boolean, but it holds an object or array.");
      }

      var start = (int)reader.TokenStartIndex;
      match = (start, (int)reader.BytesConsumed - start);

      // Keep scanning instead of returning here: a hand-edited file can carry a
      // duplicate root "enabled" key, and System.Text.Json's deserializer resolves
      // that to the last occurrence, so the splice has to target the same one
      // Load() will actually read - otherwise the two disagree after a toggle.
    }

    return match;
  }

  /// <summary>
  /// Adds an "enabled" property as the first entry of the root object, matching the
  /// file's existing newline style and indentation.
  /// </summary>
  private static byte[] InsertRootEnabled(byte[] json, int bomLength, ReadOnlySpan<byte> value)
  {
    var body = json.AsSpan(bomLength);
    var reader = new Utf8JsonReader(body, ReaderOptions);

    if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("network.json must have a JSON object at its root.");

    var afterBrace = (int)reader.BytesConsumed;

    if (!reader.Read())
      throw new JsonException("network.json must have a JSON object at its root.");

    var nextTokenStart = (int)reader.TokenStartIndex;

    // A file with no newline anywhere is deliberately single-line; inserting our
    // usual newline+indent would reformat it onto multiple lines for no reason.
    string newline;
    string indent;
    if (body.IndexOf((byte)'\n') >= 0 || body.IndexOf((byte)'\r') >= 0)
    {
      newline = body.IndexOf((byte)'\r') >= 0 ? "\r\n" : "\n";
      indent = DetectIndent(body, nextTokenStart);
    }
    else
    {
      newline = "";
      indent = " ";
    }

    // EndObject means no property follows, so there's never a comma to add.
    // A root object holding nothing but comments still lands here too (the
    // comments aren't tokens), which is why the two sub-cases below insert
    // ahead of any existing content rather than replacing it outright.
    if (reader.TokenType == JsonTokenType.EndObject)
    {
      if (nextTokenStart == afterBrace)
      {
        // Truly empty root object - nothing at all between the braces, not even
        // whitespace - so synthesize the whole line ourselves.
        var text = $"{newline}{indent}\"enabled\": {Encoding.UTF8.GetString(value)}{newline}";
        return Splice(json, bomLength + afterBrace, 0, Encoding.UTF8.GetBytes(text));
      }

      // The object holds only whitespace or comments - insert ahead of that
      // content so it survives. No trailing comma: nothing follows the new
      // property, and one here would make the file strict-JSON-invalid even
      // though our own lenient reader would tolerate it.
      var noFollowingProperty = $"{newline}{indent}\"enabled\": {Encoding.UTF8.GetString(value)}";
      return Splice(json, bomLength + afterBrace, 0, Encoding.UTF8.GetBytes(noFollowingProperty));
    }

    // A real property follows, so the comma is required.
    var inserted = $"{newline}{indent}\"enabled\": {Encoding.UTF8.GetString(value)},";
    return Splice(json, bomLength + afterBrace, 0, Encoding.UTF8.GetBytes(inserted));
  }

  /// <summary>
  /// Reads the whitespace at the start of the line holding <paramref name="tokenStart"/>,
  /// so an inserted property lines up with its siblings. Falls back to two spaces.
  /// </summary>
  private static string DetectIndent(ReadOnlySpan<byte> body, int tokenStart)
  {
    var lineStart = tokenStart;
    while (lineStart > 0 && body[lineStart - 1] is not ((byte)'\n' or (byte)'\r'))
      lineStart--;

    var indent = body[lineStart..tokenStart];
    foreach (var b in indent)
    {
      if (b is not ((byte)' ' or (byte)'\t'))
        return "  ";
    }

    return indent.IsEmpty ? "  " : Encoding.UTF8.GetString(indent);
  }

  private static byte[] Splice(byte[] source, int start, int length, ReadOnlySpan<byte> replacement)
  {
    var result = new byte[source.Length - length + replacement.Length];
    source.AsSpan(0, start).CopyTo(result);
    replacement.CopyTo(result.AsSpan(start));
    source.AsSpan(start + length).CopyTo(result.AsSpan(start + replacement.Length));
    return result;
  }

  /// <summary>Gets the path to the local rules file (creates dir if needed).</summary>
  public static string GetLocalRulesPath(AppPaths paths)
  {
    var path = paths.GetLocalPath(RulesFileName);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    return path;
  }

  /// <summary>Gets the path to the global rules file (creates dir if needed).</summary>
  public static string GetGlobalRulesPath(AppPaths paths)
  {
    var path = paths.GetGlobalPath(RulesFileName);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    return path;
  }
}

public enum AirlockConfigSource
{
  Default,
  Global,
  Local
}

/// <summary>What a toggle did to the config file, so commands can say so.</summary>
public enum AirlockToggleOutcome
{
  /// <summary>The file already existed and only its enabled flag changed.</summary>
  UpdatedExisting,

  /// <summary>A new local file was created from the global one, carrying its rules across.</summary>
  SeededFromGlobal,

  /// <summary>No config existed anywhere, so a default one was written.</summary>
  CreatedDefault
}
