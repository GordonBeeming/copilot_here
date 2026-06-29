# Install Go
# Using official Go installation method
ENV GOPATH=/usr/local/go-workspace
ENV PATH="/usr/local/go/bin:${GOPATH}/bin:${PATH}"

# Resolve the latest stable Go at image-build time (go.dev/VERSION returns the
# current release, e.g. "go1.26.4"). Set GOLANG_VERSION (same "goX.Y.Z" form) to pin.
ARG GOLANG_VERSION
RUN ver="${GOLANG_VERSION:-$(curl -fsSL 'https://go.dev/VERSION?m=text' | head -1)}" \
  && test -n "$ver" \
  && curl -fsSL "https://go.dev/dl/${ver}.linux-$(dpkg --print-architecture).tar.gz" -o go.tar.gz \
  && tar -C /usr/local -xzf go.tar.gz \
  && rm go.tar.gz

# Create GOPATH and make it writable by all users
RUN mkdir -p ${GOPATH} \
  && chmod -R a+rwX ${GOPATH}

# Verify installation
RUN go version
