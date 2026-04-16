#!/bin/bash
# KMITL NetAuth - One-shot login script
# Use this on headless Linux (e.g., Proxmox) to authenticate before installing the full service.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/uunw/kmitlnetauth/main/scripts/kmitl-login.sh | bash
#   # or
#   bash kmitl-login.sh
#   # or with arguments
#   bash kmitl-login.sh -u 670xxxxx -p yourpassword

set -euo pipefail

LOGIN_URL="https://portal.kmitl.ac.th:19008/portalauth/login"
ACIP="10.252.13.10"

USERNAME=""
PASSWORD=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -u|--username) USERNAME="$2"; shift 2 ;;
        -p|--password) PASSWORD="$2"; shift 2 ;;
        -h|--help)
            echo "KMITL NetAuth - One-shot login"
            echo ""
            echo "Usage: $0 [-u username] [-p password]"
            echo ""
            echo "If username/password are not provided, you will be prompted."
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Prompt if not provided
if [ -z "$USERNAME" ]; then
    printf "Student ID: "
    read -r USERNAME
fi

if [ -z "$PASSWORD" ]; then
    printf "Password: "
    read -rs PASSWORD
    echo ""
fi

if [ -z "$USERNAME" ] || [ -z "$PASSWORD" ]; then
    echo "Error: Username and password are required."
    exit 1
fi

# Get MAC address (lowercase, no colons/dashes)
MAC=$(ip link show 2>/dev/null | awk '/ether/ {print $2; exit}' | tr -d ':' | tr '[:upper:]' '[:lower:]')
if [ -z "$MAC" ]; then
    MAC=$(cat /sys/class/net/*/address 2>/dev/null | grep -v '00:00:00:00:00:00' | head -1 | tr -d ':' | tr '[:upper:]' '[:lower:]')
fi
MAC="${MAC:-000000000000}"

echo "Logging in as $USERNAME (MAC: $MAC)..."

# Send login request
HTTP_CODE=$(curl -sk -o /tmp/kmitl-login-response.txt -w "%{http_code}" \
    -X POST "$LOGIN_URL" \
    -d "userName=$USERNAME" \
    -d "userPass=$PASSWORD" \
    -d "uaddress=" \
    -d "umac=$MAC" \
    -d "agreed=1" \
    -d "acip=$ACIP" \
    -d "authType=1")

if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
    echo "Login successful! (HTTP $HTTP_CODE)"

    # Verify internet connectivity
    if curl -sf --max-time 5 "http://detectportal.firefox.com/success.txt" 2>/dev/null | grep -q "success"; then
        echo "Internet connection verified."
    else
        echo "Warning: Login sent but internet check failed. You may need to retry."
    fi
else
    echo "Login failed! (HTTP $HTTP_CODE)"
    echo "Response:"
    cat /tmp/kmitl-login-response.txt 2>/dev/null
    exit 1
fi

rm -f /tmp/kmitl-login-response.txt
