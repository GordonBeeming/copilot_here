# Install TypeScript Language Server for code intelligence
RUN npm install -g typescript typescript-language-server

# Write LSP config fragment for TypeScript
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "typescript": { "command": "typescript-language-server", "args": ["--stdio"], "fileExtensions": { ".ts": "typescript", ".tsx": "typescriptreact", ".js": "javascript", ".jsx": "javascriptreact" } } } }' \
    > /etc/copilot/lsp-config.d/typescript.json
