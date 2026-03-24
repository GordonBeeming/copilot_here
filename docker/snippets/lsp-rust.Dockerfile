# Install Rust Analyzer for code intelligence
RUN rustup component add rust-analyzer

# Write LSP config fragment for Rust
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "rust": { "command": "rust-analyzer", "args": [], "fileExtensions": { ".rs": "rust" } } } }' \
    > /etc/copilot/lsp-config.d/rust.json
