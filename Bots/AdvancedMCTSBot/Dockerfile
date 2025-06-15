# Stage 1 – builder
FROM public.ecr.aws/docker/library/gcc:12-bookworm AS builder

RUN apt-get update && apt-get install -y --no-install-recommends \
    cmake git curl unzip tar ninja-build \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# ──►  install ALL libs your CMakeLists.txt needs
RUN git clone https://github.com/microsoft/vcpkg \
    && ./vcpkg/bootstrap-vcpkg.sh \
    && ./vcpkg/vcpkg install fmt cpprestsdk microsoft-signalr   # ← added

COPY . .
RUN rm -rf build && \
    cmake -S . -B build -G Ninja \
    -DCMAKE_TOOLCHAIN_FILE=/app/vcpkg/scripts/buildsystems/vcpkg.cmake \
    -DCMAKE_BUILD_TYPE=Release && \
    cmake --build build --parallel $(nproc) --config Release

# Stage 2 – runtime
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y --no-install-recommends \
    libcurl4 libssl3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=builder /app/build/AdvancedMCTSBot .

ENV BOT_NICKNAME=AdvancedMCTS \
    RUNNER_IPV4=http://engine \
    RUNNER_PORT=5000 \
    HUB_NAME=bothub

CMD ["./AdvancedMCTSBot"]
