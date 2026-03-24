# Install C# Language Server for code intelligence
# Install globally as root, then symlink to a shared location so appuser can access it
RUN dotnet tool install -g csharp-ls \
    && ln -s /root/.dotnet/tools/csharp-ls /usr/local/bin/csharp-ls

# Write LSP config fragment for C#
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "csharp": { "command": "csharp-ls", "args": [], "fileExtensions": { ".cs": "csharp", ".csx": "csharp" } } } }' \
    > /etc/copilot/lsp-config.d/csharp.json
