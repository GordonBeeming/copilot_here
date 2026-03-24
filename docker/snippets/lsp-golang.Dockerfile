# Install gopls (Go Language Server) for code intelligence
ARG GOPLS_VERSION=latest
RUN go install golang.org/x/tools/gopls@${GOPLS_VERSION}

# Write LSP config fragment for Go
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "go": { "command": "gopls", "args": ["serve"], "fileExtensions": { ".go": "go" } } } }' \
    > /etc/copilot/lsp-config.d/golang.json
