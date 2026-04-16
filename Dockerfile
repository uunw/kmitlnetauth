FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG VERSION=0.0.0.1
WORKDIR /src

# Copy project files for layer caching
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/KmitlNetAuth.Core/KmitlNetAuth.Core.csproj src/KmitlNetAuth.Core/
COPY src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj src/KmitlNetAuth.Cli/
RUN dotnet restore src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj -r linux-musl-x64

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/KmitlNetAuth.Cli/KmitlNetAuth.Cli.csproj \
    -c Release \
    -r linux-musl-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    /p:Version=${VERSION} \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
WORKDIR /app

ENV KMITL_USERNAME=""
ENV KMITL_PASSWORD=""
ENV KMITL_IP=""
ENV KMITL_INTERVAL=300
ENV KMITL_MAX_ATTEMPT=20
ENV KMITL_AUTO_LOGIN=true
ENV KMITL_LOG_LEVEL=Information

RUN apk add --no-cache ca-certificates

COPY --from=build /app/publish/kmitlnetauth .
COPY packaging/systemd/kmitlnetauth.service /etc/systemd/system/

ENTRYPOINT ["./kmitlnetauth", "-d"]
