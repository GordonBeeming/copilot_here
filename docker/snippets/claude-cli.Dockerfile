# ARG for the Claude Code CLI version - passed from build process
# This ensures cache invalidation when a new version is available
ARG CLAUDE_VERSION=latest

# Install Anthropic's Claude Code CLI via npm.
RUN npm install -g @anthropic-ai/claude-code@${CLAUDE_VERSION}
