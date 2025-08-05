# 基于 .NET 8 SDK 镜像
FROM mcr.microsoft.com/dotnet/sdk:8.0

# 可配置参数
ARG BENTO4_VERSION=1.6.0-639
ARG USE_PREBUILT_BENTO4=false

# 创建 vscode 用户（Dev Container 需要）
RUN groupadd --gid 1000 vscode \
    && useradd --uid 1000 --gid 1000 --create-home --shell /bin/bash vscode \
    && mkdir -p /etc/sudoers.d \
    && echo 'vscode ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/vscode \
    && chmod 440 /etc/sudoers.d/vscode

# 安装依赖
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    git \
    unzip \
    pkg-config \
    build-essential \
    cmake \
    libssl-dev \
    yasm \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# 安装 Bento4
RUN if [ "$USE_PREBUILT_BENTO4" = "true" ]; then \
      mkdir -p /opt/bento4 && \
      curl -L -o bento4.zip https://www.bok.net/Bento4/binaries/Bento4-${BENTO4_VERSION}-Linux-x86_64.zip && \
      unzip bento4.zip -d /opt/bento4 && \
      rm bento4.zip && \
      ln -sf /opt/bento4/Bento4-${BENTO4_VERSION}-Linux-x86_64/bin/* /usr/local/bin/ ; \
    else \
      git clone --branch v${BENTO4_VERSION} --depth 1 https://github.com/axiomatic-systems/Bento4.git /tmp/bento4 && \
      cd /tmp/bento4 && \
      mkdir build && cd build && \
      cmake -DCMAKE_BUILD_TYPE=Release .. && \
      make -j$(nproc) && \
      mkdir -p /opt/bento4/bin && \
      cp mp4* /opt/bento4/bin && \
      ln -sf /opt/bento4/bin/* /usr/local/bin/ && \
      rm -rf /tmp/bento4 ; \
    fi

# 添加到 PATH
ENV PATH="$PATH:/opt/bento4/bin"

WORKDIR /workspace
CMD ["bash"]
