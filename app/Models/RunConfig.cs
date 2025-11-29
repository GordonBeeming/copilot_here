namespace CopilotHere.Models;

internal record RunConfig
{
  public string ImageTag { get; set; } = "latest";
  public bool SkipCleanup { get; set; } = false;
  public bool SkipPull { get; set; } = false;
  public List<string> ReadOnlyMounts { get; } = [];
  public List<string> ReadWriteMounts { get; } = [];
  public List<string> PassthroughArgs { get; } = [];
}