# Install Eclipse JDT Language Server for Java code intelligence
ARG JDTLS_VERSION=1.43.0
ARG JDTLS_TIMESTAMP=202501232208
RUN mkdir -p /usr/local/share/jdtls \
  && curl -fsSL "https://download.eclipse.org/jdtls/milestones/${JDTLS_VERSION}/jdt-language-server-${JDTLS_VERSION}-${JDTLS_TIMESTAMP}.tar.gz" -o jdtls.tar.gz \
  && tar -C /usr/local/share/jdtls -xzf jdtls.tar.gz \
  && rm jdtls.tar.gz

# Create launcher script
RUN echo '#!/bin/sh\nexec java \\\n  -Declipse.application=org.eclipse.jdt.ls.core.id1 \\\n  -Dosgi.bundles.defaultStartLevel=4 \\\n  -Declipse.product=org.eclipse.jdt.ls.core.product \\\n  -Dlog.level=ALL \\\n  -noverify \\\n  --add-modules=ALL-SYSTEM \\\n  --add-opens java.base/java.util=ALL-UNNAMED \\\n  --add-opens java.base/java.lang=ALL-UNNAMED \\\n  -jar /usr/local/share/jdtls/plugins/org.eclipse.equinox.launcher_*.jar \\\n  -configuration /usr/local/share/jdtls/config_linux \\\n  -data /tmp/jdtls-workspace \\\n  "$@"' > /usr/local/bin/jdtls \
  && chmod +x /usr/local/bin/jdtls

# Write LSP config fragment for Java
RUN mkdir -p /etc/copilot/lsp-config.d && \
    echo '{ "lspServers": { "java": { "command": "jdtls", "args": [], "fileExtensions": { ".java": "java" } } } }' \
    > /etc/copilot/lsp-config.d/java.json
