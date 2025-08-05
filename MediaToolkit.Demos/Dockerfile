# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

# 安装依赖
RUN apt-get update && apt-get install -y \
    ffmpeg \
    git \
    cmake \
    make \
    g++ \
    unzip \
    && rm -rf /var/lib/apt/lists/*

# 编译安装 Bento4
RUN git clone https://github.com/axiomatic-systems/Bento4.git /tmp/bento4 \
    && cd /tmp/bento4/Source \
    && cmake . \
    && make -j$(nproc) \
    && mkdir -p /opt/bento4/bin \
    && cp -r ../Bin/* /opt/bento4/bin/ \
    && ln -s /opt/bento4/bin/* /usr/local/bin/ \
    && rm -rf /tmp/bento4

# 配置工具路径环境变量
ENV FFMPEG_PATH="/usr/bin/ffmpeg"
ENV BENTO4_PATH="/opt/bento4/bin"

# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MediaToolkit.Demos/MediaToolkit.Demos.csproj", "MediaToolkit.Demos/"]
COPY ["MediaToolkit.Core/MediaToolkit.Core.csproj", "MediaToolkit.Core/"]
COPY ["MediaToolkit.Adapters.FFmpeg/MediaToolkit.Adapters.FFmpeg.csproj", "MediaToolkit.Adapters.FFmpeg/"]
COPY ["MediaToolkit.Adapters.Bento4/MediaToolkit.Adapters.Bento4.csproj", "MediaToolkit.Adapters.Bento4/"]
RUN dotnet restore "./MediaToolkit.Demos/MediaToolkit.Demos.csproj"
COPY . .
WORKDIR "/src/MediaToolkit.Demos"
RUN dotnet build "./MediaToolkit.Demos.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MediaToolkit.Demos.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediaToolkit.Demos.dll"]