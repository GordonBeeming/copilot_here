# Set the working directory for the container.
WORKDIR /work

# Copy the entrypoint script into the container and make it executable.
COPY docker/shared/entrypoint.sh /usr/local/bin/
COPY docker/shared/entrypoint-airlock.sh /usr/local/bin/
COPY docker/session-info.sh /usr/local/bin/session-info
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/entrypoint-airlock.sh /usr/local/bin/session-info

# The entrypoint script will handle user creation and command execution.
ENTRYPOINT [ "entrypoint.sh" ]

# The default command to run if none is provided.
CMD [ "copilot", "--banner" ]
