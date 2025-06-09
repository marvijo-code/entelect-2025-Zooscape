# Multi-stage build for Advanced MCTS Bot
FROM ubuntu:22.04 AS builder

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    libcurl4-openssl-dev \
    libjsoncpp-dev \
    pkg-config \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy source code
COPY . .

# Build the bot
RUN chmod +x build.sh && ./build.sh

# Runtime stage
FROM ubuntu:22.04 AS runtime

# Install runtime dependencies
RUN apt-get update && apt-get install -y \
    libcurl4 \
    libjsoncpp25 \
    && rm -rf /var/lib/apt/lists/*

# Create app user
RUN useradd -m -s /bin/bash botuser

# Set working directory
WORKDIR /app

# Copy built executable and config
COPY --from=builder /app/build/AdvancedMCTSBot .
COPY --from=builder /app/build/config.json .

# Change ownership
RUN chown -R botuser:botuser /app

# Switch to app user
USER botuser

# Set environment variables
ENV BOT_NICKNAME=AdvancedMCTSBot
ENV RUNNER_IPV4=localhost
ENV RUNNER_PORT=5000

# Expose any necessary ports (if needed)
# EXPOSE 8080

# Run the bot
CMD ["./AdvancedMCTSBot"]