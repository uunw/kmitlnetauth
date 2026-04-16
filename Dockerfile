FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
ARG TARGETARCH
WORKDIR /app

ENV KMITL_USERNAME=""
ENV KMITL_PASSWORD=""
ENV KMITL_IP=""
ENV KMITL_INTERVAL=300
ENV KMITL_MAX_ATTEMPT=20
ENV KMITL_AUTO_LOGIN=true
ENV KMITL_LOG_LEVEL=Information

RUN apk add --no-cache ca-certificates

# CI places pre-built binaries at docker-ctx/<arch>/kmitlnetauth
COPY docker-ctx/${TARGETARCH}/kmitlnetauth /app/kmitlnetauth
RUN chmod +x /app/kmitlnetauth

ENTRYPOINT ["./kmitlnetauth", "-d"]
