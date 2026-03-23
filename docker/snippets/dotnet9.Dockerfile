ARG DOTNET_SDK_9_VERSION

# Install .NET 9 SDK (latest)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
  && chmod +x dotnet-install.sh \
  && if [ -z "$DOTNET_SDK_9_VERSION" ]; then \
       ./dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet; \
     else \
       ./dotnet-install.sh --version $DOTNET_SDK_9_VERSION --install-dir /usr/share/dotnet; \
     fi \
  && rm dotnet-install.sh
