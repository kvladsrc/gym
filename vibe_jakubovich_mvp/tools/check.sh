#!/usr/bin/env bash
set -euo pipefail
mkdir -p tmp
godot_cmd="${GODOT:-godot}"
"${godot_cmd}" --version
"${godot_cmd}" --headless --path . --import --quit >tmp/import.log 2>&1
# The pinned Nix Godot 4.6 build embeds a literal 'False' CA filename.
# This offline game never uses TLS. Keep all actual script/render errors fatal.
if rg -n 'SCRIPT ERROR|Parse Error|ERROR:' tmp/import.log |
    rg -v "Cannot open X509CertificateMbedTLS file 'False'"; then
    exit 1
fi
test_data=$(mktemp -d /tmp/pole-chudes-tests.XXXXXXXX)
XDG_DATA_HOME="${test_data}" "${godot_cmd}" --headless --path . \
    --script tools/test.gd >tmp/tests.log 2>&1
if rg -n 'SCRIPT ERROR|Parse Error|ERROR:|FAIL:' tmp/tests.log |
    rg -v "Cannot open X509CertificateMbedTLS file 'False'"; then
    exit 1
fi
tail -5 tmp/tests.log
