# ======================
# BUILD STAGE (Alpine SDK)
# ======================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Dependencies for NativeAOT trimming + musl publish
RUN apk add --no-cache clang zlib-dev musl-dev icu-dev libgcc libstdc++ bash

# Copy csproj first (layer caching)
COPY ["DiscordWhoIs/DiscordWhoIs.csproj", "DiscordWhoIs/"]
RUN dotnet restore "DiscordWhoIs/DiscordWhoIs.csproj"

# Copy source
COPY ["DiscordWhoIs/", "DiscordWhoIs/"]
WORKDIR "/src/DiscordWhoIs"

# Publish NativeAOT trimmed binary
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
    rm -rf /root/.nuget /src/DiscordWhoIs/bin /src/DiscordWhoIs/obj

# ======================
# FINAL RUNTIME IMAGE (Alpine)
# ======================
FROM alpine:3.21 AS final
WORKDIR /app

# Runtime deps for trimmed .NET binary
RUN apk add --no-cache icu-libs libgcc libstdc++ zlib

# Copy self-contained binary
COPY --from=build /app/publish/ .

# Make executable
RUN chmod +x DiscordWhoIs

# Bind ASP.NET to Render's PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Required by Render Web Services
EXPOSE 10000

ENTRYPOINT ["./DiscordWhoIs"]
