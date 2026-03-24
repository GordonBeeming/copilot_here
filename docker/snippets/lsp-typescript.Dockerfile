# Install TypeScript Language Server for code intelligence
ARG TYPESCRIPT_VERSION=latest
ARG TYPESCRIPT_LANGUAGE_SERVER_VERSION=latest
RUN npm install -g typescript@${TYPESCRIPT_VERSION} typescript-language-server@${TYPESCRIPT_LANGUAGE_SERVER_VERSION}

# Write LSP config fragment for TypeScript
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "typescript": { "command": "typescript-language-server", "args": ["--stdio"], "fileExtensions": { ".ts": "typescript", ".tsx": "typescriptreact", ".js": "javascript", ".jsx": "javascriptreact" } } } }' \
    > /etc/copilot/lsp-config.d/typescript.json
