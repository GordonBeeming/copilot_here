namespace CopilotHere.Infrastructure;

/// <summary>
/// Centralized emoji support with automatic fallback for terminals
/// that don't support variation selectors (e.g., Windows Terminal).
/// </summary>
public static class Emoji
{
  public static string Success(bool supportsVariant) => supportsVariant ? "✅" : "✓";
  public static string Error(bool supportsVariant) => supportsVariant ? "❌" : "✗";
  public static string Warning(bool supportsVariant) => supportsVariant ? "⚠️" : "⚠";
  public static string Info(bool supportsVariant) => supportsVariant ? "ℹ️" : "ℹ";
  
  public static string Robot(bool supportsVariant) => "🤖";
  public static string RobotYolo(bool supportsVariant) => supportsVariant ? "🤖⚡️" : "🤖";
  public static string Shield(bool supportsVariant) => supportsVariant ? "🛡️" : "🛡";
  public static string Cleanup(bool supportsVariant) => supportsVariant ? "🧹" : "🧹";
  public static string Trash(bool supportsVariant) => supportsVariant ? "🗑️" : "🗑";
  
  public static string Package(bool supportsVariant) => "📦";
  public static string Download(bool supportsVariant) => "📥";
  public static string Folder(bool supportsVariant) => "📂";
  public static string Dir(bool supportsVariant) => "📁";
  public static string Local(bool supportsVariant) => "📍";
  public static string Global(bool supportsVariant) => "🌍";
  public static string Tool(bool supportsVariant) => "🔧";
  public static string List(bool supportsVariant) => "📋";
  public static string Image(bool supportsVariant) => supportsVariant ? "🖼️" : "🖼";
  public static string Factory(bool supportsVariant) => "🏭";
  
  public static string Rocket(bool supportsVariant) => "🚀";
  public static string Update(bool supportsVariant) => "🔄";
  public static string Skip(bool supportsVariant) => supportsVariant ? "⏭️" : "⏭";
  public static string Stop(bool supportsVariant) => "🛑";
  public static string Notice(bool supportsVariant) => "📢";
  public static string Lightbulb(bool supportsVariant) => "💡";
}
