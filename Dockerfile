FROM rust:alpine AS builder

WORKDIR /app

# Install system dependencies for build
RUN apk add --no-cache musl-dev pkgconfig openssl-dev openssl-libs-static

# Copy manifests
COPY Cargo.toml Cargo.lock ./
COPY crates ./crates

# Build
RUN cargo build --release --bin kmitlnetauth

FROM alpine:latest

WORKDIR /app

# Default Environment Variables
ENV KMITL_USERNAME=""
ENV KMITL_PASSWORD=""
ENV KMITL_IP=""
ENV KMITL_INTERVAL=300
ENV KMITL_MAX_ATTEMPT=20
ENV KMITL_AUTO_LOGIN=true

# Ensure we have CA certificates for HTTPS
RUN apk add --no-cache ca-certificates libgcc

COPY --from=builder /app/target/release/kmitlnetauth .
COPY crates/service/kmitlnetauth.service /etc/systemd/system/

CMD ["./kmitlnetauth"]