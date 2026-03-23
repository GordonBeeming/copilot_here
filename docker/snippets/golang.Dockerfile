# Install Go
# Using official Go installation method
ENV GOLANG_VERSION=1.25.5
ENV GOPATH=/usr/local/go-workspace
ENV PATH="/usr/local/go/bin:${GOPATH}/bin:${PATH}"

# Install Go from official tarball
RUN curl -fsSL "https://go.dev/dl/go${GOLANG_VERSION}.linux-$(dpkg --print-architecture).tar.gz" -o go.tar.gz \
  && tar -C /usr/local -xzf go.tar.gz \
  && rm go.tar.gz

# Create GOPATH and make it writable by all users
RUN mkdir -p ${GOPATH} \
  && chmod -R a+rwX ${GOPATH}

# Verify installation
RUN go version
