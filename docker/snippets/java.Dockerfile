# Install Java (Eclipse Temurin JDK 25), Maven, and Gradle
# Using Eclipse Temurin - widely used, well-maintained OpenJDK distribution

# Add Eclipse Temurin repository and install JDK
RUN apt-get update && apt-get install -y gnupg unzip \
  && wget -qO - https://packages.adoptium.net/artifactory/api/gpg/key/public | gpg --dearmor -o /usr/share/keyrings/adoptium.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/adoptium.gpg] https://packages.adoptium.net/artifactory/deb $(. /etc/os-release && echo $VERSION_CODENAME) main" > /etc/apt/sources.list.d/adoptium.list \
  && apt-get update && apt-get install -y temurin-25-jdk \
  && rm -rf /var/lib/apt/lists/*

# Set JAVA_HOME dynamically based on architecture (ENV can't use command substitution)
RUN ln -s /usr/lib/jvm/temurin-25-jdk-$(dpkg --print-architecture) /usr/lib/jvm/temurin-25-jdk
ENV JAVA_HOME=/usr/lib/jvm/temurin-25-jdk
ENV PATH="${JAVA_HOME}/bin:${PATH}"

# Install Maven. Resolve the latest stable 3.9.x at build time from the Apache
# mirror listing (the maven-metadata <release> tag points at Maven 4 RCs, which
# we don't want). Set MAVEN_VERSION to pin a specific 3.x release.
ARG MAVEN_VERSION
RUN ver="${MAVEN_VERSION:-$(curl -fsSL https://dlcdn.apache.org/maven/maven-3/ | grep -oE '3\.[0-9]+\.[0-9]+/' | tr -d / | sort -uV | tail -1)}" \
  && test -n "$ver" \
  && curl -fsSL "https://dlcdn.apache.org/maven/maven-3/${ver}/binaries/apache-maven-${ver}-bin.tar.gz" -o maven.tar.gz \
  && tar -C /usr/local -xzf maven.tar.gz \
  && ln -s "/usr/local/apache-maven-${ver}/bin/mvn" /usr/local/bin/mvn \
  && rm maven.tar.gz

# Install Gradle. Resolve the current release at build time from the Gradle
# version API. Set GRADLE_VERSION to pin a specific release.
ARG GRADLE_VERSION
RUN ver="${GRADLE_VERSION}" \
  && if [ -z "$ver" ]; then ver="$(curl -fsSL https://services.gradle.org/versions/current | grep -oP '"version"\s*:\s*"\K[^"]+')"; fi \
  && test -n "$ver" \
  && curl -fsSL "https://services.gradle.org/distributions/gradle-${ver}-bin.zip" -o gradle.zip \
  && unzip -d /usr/local gradle.zip \
  && ln -s "/usr/local/gradle-${ver}/bin/gradle" /usr/local/bin/gradle \
  && rm gradle.zip

# Install PlantUML. The GitHub "latest release" exposes a stable unversioned
# asset URL, and the jar lands at a version-free path. Set PLANTUML_URL to pin.
ARG PLANTUML_URL=https://github.com/plantuml/plantuml/releases/latest/download/plantuml.jar
RUN curl -fsSL "${PLANTUML_URL}" -o /usr/local/lib/plantuml.jar \
  && echo '#!/bin/sh\nexec java -jar /usr/local/lib/plantuml.jar "$@"' > /usr/local/bin/plantuml \
  && chmod +x /usr/local/bin/plantuml

# Install Graphviz (required by PlantUML for many diagram types)
RUN apt-get update && apt-get install -y graphviz \
  && rm -rf /var/lib/apt/lists/*

# Make Maven and Gradle caches writable by all users
RUN mkdir -p /home/appuser/.m2 /home/appuser/.gradle \
  && chmod -R a+rwX /home/appuser/.m2 /home/appuser/.gradle

# Verify installations
RUN java --version && mvn --version && gradle --version && plantuml -version
