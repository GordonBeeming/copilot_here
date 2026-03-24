# Install gopls (Go Language Server) for code intelligence
RUN go install golang.org/x/tools/gopls@latest

# Write LSP config fragment for Go
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "go": { "command": "gopls", "args": ["serve"], "fileExtensions": { ".go": "go" } } } }' \
    > /etc/copilot/lsp-config.d/golang.json
