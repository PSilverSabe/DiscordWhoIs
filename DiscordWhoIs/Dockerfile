# ======================
# BUILD STAGE
# ======================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV PATH="$PATH:/root/.dotnet/tools"

# Build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
        clang zlib1g-dev libicu-dev libssl-dev curl nodejs npm git bash \
    && rm -rf /var/lib/apt/lists/*

# Copy project and restore
COPY ["DiscordWhoIs/DiscordWhoIs.csproj", "DiscordWhoIs/"]
RUN dotnet restore "DiscordWhoIs/DiscordWhoIs.csproj"

# Copy source and build
COPY ["DiscordWhoIs/", "DiscordWhoIs/"]
WORKDIR "/src/DiscordWhoIs"
RUN dotnet build -c Release

# Install Playwright CLI and only Chromium
RUN dotnet tool install --global Microsoft.Playwright.CLI \
    && playwright install chromium \
    && rm -rf /ms-playwright/firefox /ms-playwright/webkit

# Publish app (self-contained, trimmed)
RUN dotnet publish DiscordWhoIs.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    /p:PublishSingleFile=true \
    /p:SelfContained=true \
    /p:PublishTrimmed=false \
    /p:TrimMode=Link \
    /p:StripSymbols=true \
    /p:DebuggerSupport=false \
    /p:TrimmerRootDescriptorFiles=TrimmerRoot.xml

# ======================
# FINAL IMAGE (Debian slim + libicu)
# ======================
FROM debian:bookworm-slim AS final
WORKDIR /app

# Install runtime dependencies including ICU for .NET
RUN apt-get update && apt-get install -y --no-install-recommends \
        libicu72 \
        ca-certificates \
        fonts-liberation \
        libasound2 libatk-bridge2.0-0 libatk1.0-0 libcups2 \
        libdrm2 libgbm1 libgtk-3-0 libnspr4 libnss3 \
        libx11-xcb1 libxcomposite1 libxdamage1 libxrandr2 \
        libxrender1 libxss1 \
        chromium \
    && rm -rf /var/lib/apt/lists/*

# Set Playwright and .NET runtime paths
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy published app and Playwright Chromium/CLI
COPY --from=build /app/publish/ .
COPY --from=build /ms-playwright /ms-playwright
COPY --from=build /root/.dotnet/tools /root/.dotnet/tools

ENTRYPOINT ["./DiscordWhoIs"]
