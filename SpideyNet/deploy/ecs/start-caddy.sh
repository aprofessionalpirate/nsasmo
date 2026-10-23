#!/bin/sh
set -eu

# With a public task ENI and no NAT gateway, outbound requests use the task's public IP.
# Discover it at each start so a replacement task requests the correct certificate.
attempt=0
max_attempts=5
retry_seconds=2
ip_url=https://checkip.amazonaws.com
public_ip=

while [ "$attempt" -lt "$max_attempts" ]; do
    attempt=$((attempt + 1))
    if response=$(wget -q -T 10 -O - "$ip_url"); then
        public_ip=$(printf '%s' "$response" | tr -d '\r\n')
        break
    fi

    sleep "$retry_seconds"
done

# Reject unexpected responses before using them in a TLS configuration.
if ! printf '%s\n' "$public_ip" | awk -F. '
    NF != 4 { exit 1 }
    { for (i = 1; i <= NF; i++) if ($i !~ /^[0-9]+$/ || $i > 255) exit 1 }
'; then
    echo 'Could not discover the task public IPv4 address.' >&2
    exit 1
fi

export RELAY_PUBLIC_IP="$public_ip"
echo "Relay endpoint: wss://$RELAY_PUBLIC_IP"
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
