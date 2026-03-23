# Install Rust dependencies
RUN apt-get update && apt-get install -y \
  build-essential \
  pkg-config \
  libssl-dev \
  && rm -rf /var/lib/apt/lists/*

# Install Rust toolchain to system-wide location
ENV RUSTUP_HOME=/usr/local/rustup
ENV CARGO_HOME=/usr/local/cargo
ENV PATH="/usr/local/cargo/bin:${PATH}"

RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable

# Make cargo registry writable by all users (for downloading crates)
RUN chmod -R a+rwX /usr/local/cargo

# Verify installation
RUN rustc --version && cargo --version
