# Install Go
# Using official Go installation method
ENV GOPATH=/usr/local/go-workspace
ENV PATH="/usr/local/go/bin:${GOPATH}/bin:${PATH}"

# Resolve the latest stable Go at image-build time (go.dev/VERSION returns the
# current release, e.g. "go1.26.4"). Strip any leading "go" and re-prepend it so
# GOLANG_VERSION can be pinned as either "1.26.3" or "go1.26.3".
ARG GOLANG_VERSION
RUN ver="${GOLANG_VERSION:-$(curl -fsSL 'https://go.dev/VERSION?m=text' | head -1)}" \
  && test -n "$ver" \
  && ver="${ver#go}" \
  && curl -fsSL "https://go.dev/dl/go${ver}.linux-$(dpkg --print-architecture).tar.gz" -o go.tar.gz \
  && tar -C /usr/local -xzf go.tar.gz \
  && rm go.tar.gz

# Create GOPATH and make it writable by all users
RUN mkdir -p ${GOPATH} \
  && chmod -R a+rwX ${GOPATH}

# Verify installation
RUN go version
