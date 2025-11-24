# ======================
# BUILD STAGE (Alpine SDK)
# ======================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Install dependencies for NativeAOT/self-contained publish
RUN apk add --no-cache clang zlib-dev musl-dev icu-libs libgcc libstdc++ bash

# Copy project file only and restore dependencies
COPY ["DiscordWhoIs/DiscordWhoIs.csproj", "DiscordWhoIs/"]
RUN dotnet restore "DiscordWhoIs/DiscordWhoIs.csproj"

# Copy source files and publish single-file, self-contained, trimmed binary
COPY ["DiscordWhoIs/", "DiscordWhoIs/"]
WORKDIR "/src/DiscordWhoIs"

RUN dotnet publish DiscordWhoIs.csproj \
    -c Release \
    -o /app/publish \
    -r linux-musl-x64 \
    /p:PublishSingleFile=true \
    /p:SelfContained=true \
    /p:PublishTrimmed=true \
    /p:TrimMode=Link \
    /p:StripSymbols=true \
    /p:DebuggerSupport=false && \
    # Clean up build caches to reduce image size
    rm -rf /root/.nuget /src/DiscordWhoIs/bin /src/DiscordWhoIs/obj

# ======================
# FINAL RUNTIME IMAGE (Alpine)
# ======================
FROM alpine:3.21 AS final
WORKDIR /app

# Minimal runtime dependencies
RUN apk add --no-cache icu-libs libgcc libstdc++ zlib

# Copy only the published single-file binary
COPY --from=build /app/publish/ .

# Make binary executable
RUN chmod +x DiscordWhoIs

ENTRYPOINT ["./DiscordWhoIs"]
