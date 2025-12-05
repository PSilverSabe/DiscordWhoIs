# ======================
# BUILD STAGE
# ======================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Build deps
RUN apk add --no-cache --virtual .build-deps \
        clang zlib-dev musl-dev icu-dev libgcc libstdc++ bash

# Restore dependencies
COPY ["DiscordWhoIs/DiscordWhoIs.csproj", "DiscordWhoIs/"]
RUN dotnet restore "DiscordWhoIs/DiscordWhoIs.csproj"

# Copy source
COPY ["DiscordWhoIs/", "DiscordWhoIs/"]
WORKDIR "/src/DiscordWhoIs"

# Publish trimmed NativeAOT binary
RUN dotnet publish DiscordWhoIs.csproj \
    -c Release \
    -o /app/publish \
    -r linux-musl-x64 \
    /p:PublishSingleFile=true \
    /p:SelfContained=true \
    /p:PublishTrimmed=true \
    /p:TrimMode=Link \
    /p:StripSymbols=true \
    /p:DebuggerSupport=false \
 && rm -rf /root/.nuget /src/DiscordWhoIs/bin /src/DiscordWhoIs/obj \
 && apk del .build-deps

# ======================
# FINAL IMAGE
# ======================
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS final
WORKDIR /app

# ICU required for globalization
RUN apk add --no-cache icu-libs

COPY --from=build /app/publish/ .

RUN mkdir -p /databases
RUN chown -R $APP_USER:$APP_USER /databases

ENTRYPOINT ["./DiscordWhoIs"]

