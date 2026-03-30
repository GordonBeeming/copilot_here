# Install Java (Eclipse Temurin JDK 21), Maven, and Gradle
# Using Eclipse Temurin - widely used, well-maintained OpenJDK distribution

# Add Eclipse Temurin repository and install JDK
RUN apt-get update && apt-get install -y gnupg unzip \
  && wget -qO - https://packages.adoptium.net/artifactory/api/gpg/key/public | gpg --dearmor -o /usr/share/keyrings/adoptium.gpg \
  && echo "deb [signed-by=/usr/share/keyrings/adoptium.gpg] https://packages.adoptium.net/artifactory/deb $(. /etc/os-release && echo $VERSION_CODENAME) main" > /etc/apt/sources.list.d/adoptium.list \
  && apt-get update && apt-get install -y temurin-21-jdk \
  && rm -rf /var/lib/apt/lists/*

# Set JAVA_HOME dynamically based on architecture (ENV can't use command substitution)
RUN ln -s /usr/lib/jvm/temurin-21-jdk-$(dpkg --print-architecture) /usr/lib/jvm/temurin-21-jdk
ENV JAVA_HOME=/usr/lib/jvm/temurin-21-jdk
ENV PATH="${JAVA_HOME}/bin:${PATH}"

# Install Maven
ARG MAVEN_VERSION=3.9.9
RUN curl -fsSL "https://archive.apache.org/dist/maven/maven-3/${MAVEN_VERSION}/binaries/apache-maven-${MAVEN_VERSION}-bin.tar.gz" -o maven.tar.gz \
  && tar -C /usr/local -xzf maven.tar.gz \
  && ln -s /usr/local/apache-maven-${MAVEN_VERSION}/bin/mvn /usr/local/bin/mvn \
  && rm maven.tar.gz

# Install Gradle
ARG GRADLE_VERSION=8.12
RUN curl -fsSL "https://services.gradle.org/distributions/gradle-${GRADLE_VERSION}-bin.zip" -o gradle.zip \
  && unzip -d /usr/local gradle.zip \
  && ln -s /usr/local/gradle-${GRADLE_VERSION}/bin/gradle /usr/local/bin/gradle \
  && rm gradle.zip

# Install PlantUML
ARG PLANTUML_VERSION=1.2025.2
RUN curl -fsSL "https://github.com/plantuml/plantuml/releases/download/v${PLANTUML_VERSION}/plantuml-${PLANTUML_VERSION}.jar" -o /usr/local/lib/plantuml.jar \
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
