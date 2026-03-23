# ARG for the Copilot CLI version - passed from build process
# This ensures cache invalidation when a new version is available
ARG COPILOT_VERSION=latest

# Install the standalone GitHub Copilot CLI via npm.
RUN npm install -g @github/copilot@${COPILOT_VERSION}
