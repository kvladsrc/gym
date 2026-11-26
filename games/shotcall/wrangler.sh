#!/usr/bin/env bash
# wrangler with a Cloudflare account token from CLOUDFLARE_API_KEY (or
# CLOUDFLARE_API_TOKEN). wrangler would take CLOUDFLARE_API_KEY for a global
# API key, so it is passed on as CLOUDFLARE_API_TOKEN instead.
set -euo pipefail
cd "$(dirname "$0")"
export CLOUDFLARE_API_TOKEN="${CLOUDFLARE_API_TOKEN:-${CLOUDFLARE_API_KEY:?set CLOUDFLARE_API_KEY to a Cloudflare account token}}"
unset CLOUDFLARE_API_KEY
if [ -z "${CLOUDFLARE_ACCOUNT_ID:-}" ]; then
    # An account token cannot list memberships, so wrangler needs the id.
    CLOUDFLARE_ACCOUNT_ID=$(curl -fsS -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
        https://api.cloudflare.com/client/v4/accounts |
        python3 -c 'import json, sys; print(json.load(sys.stdin)["result"][0]["id"])')
    export CLOUDFLARE_ACCOUNT_ID
fi
export WRANGLER_SEND_METRICS=false
exec npx -y wrangler@4 "$@"
