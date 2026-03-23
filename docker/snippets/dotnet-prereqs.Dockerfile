# Install .NET SDK prerequisites and ICU libraries
RUN apt-get update && apt-get install -y \
  wget \
  ca-certificates \
  libicu-dev \
  && rm -rf /var/lib/apt/lists/*

# Add .NET to PATH
ENV PATH="/usr/share/dotnet:${PATH}"
ENV DOTNET_ROOT="/usr/share/dotnet"
