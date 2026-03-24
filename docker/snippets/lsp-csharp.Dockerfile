# Install C# Language Server for code intelligence
# Install directly to a shared location so appuser can access it
ARG CSHARP_LS_VERSION=0.22.0
RUN dotnet tool install csharp-ls --tool-path /usr/local/bin --version ${CSHARP_LS_VERSION}

# Write LSP config fragment for C#
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "csharp": { "command": "csharp-ls", "args": [], "fileExtensions": { ".cs": "csharp", ".csx": "csharp" } } } }' \
    > /etc/copilot/lsp-config.d/csharp.json
