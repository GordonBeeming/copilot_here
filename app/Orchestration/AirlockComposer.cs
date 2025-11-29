using System.Text;

namespace CopilotHere.Orchestration;

public sealed class AirlockComposer
{
  public static string GenerateComposeFile(
      string projectName,
      string appImage,
      string proxyImage,
      string currentDir,
      string networkConfigPath,
      List<string> extraMounts)
  {
    // Calculate container work dir logic (ported from shell)
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var containerWorkDir = currentDir.StartsWith(home)
        ? $"/home/appuser{currentDir.Substring(home.Length)}"
        : currentDir;

    // Convert the list of mounts to YAML array items
    var mountsYaml = new StringBuilder();
    foreach (var mount in extraMounts)
    {
      mountsYaml.AppendLine($"      - {mount}");
    }

    return $$$"""
        services:
          proxy:
            image: {{{proxyImage}}}
            container_name: {{{projectName}}}-proxy
            environment:
              - NETWORK_CONFIG={{{networkConfigPath}}}
            volumes:
              - proxy-ca:/etc/ssl/certs
            networks:
              - airlock
              - bridge
        
          app:
            image: {{{appImage}}}
            container_name: {{{projectName}}}-app
            working_dir: {{{containerWorkDir}}}
            environment:
              - HTTP_PROXY=http://proxy:8080
              - HTTPS_PROXY=http://proxy:8080
            volumes:
              - {{{currentDir}}}:{{{containerWorkDir}}}
              - proxy-ca:/usr/local/share/ca-certificates:ro
        {{{mountsYaml}}}
            networks:
              - airlock
            depends_on:
              - proxy
        
        networks:
          airlock:
            internal: true
          bridge:
            driver: bridge
        
        volumes:
          proxy-ca:
        """;
  }
}