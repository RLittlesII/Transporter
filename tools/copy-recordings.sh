#!/usr/bin/env bash

# Copies the recordings the Mac Catalyst head wrote into .recordings/ at the root of this
# checkout (0087). The head is sandboxed and can write only inside its own container (0061).

set -euo pipefail

container="$HOME/Library/Containers/com.companyname.gui/Data/Documents/recordings"
destination="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/.recordings"

if [[ ! -d "$container" ]]; then
    echo "No recordings directory at $container - the head has not recorded on this machine." >&2
    exit 1
fi

mkdir -p "$destination"

# --update: a recording still being written is brought up to date on the next run, and a copy
# edited since - scrubbed, say - is left alone. The sample is generated, not recorded, so it
# stays where the generator put it.
listed="$(rsync --archive --update --out-format='%n' \
    --exclude='aircraft-sample.ndjson' --include='*.ndjson' --exclude='*' \
    "$container/" "$destination/")"
copied="$(grep -v '^\./$' <<< "$listed" || true)"

if [[ -z "$copied" ]]; then
    echo "Nothing new to copy into $destination."
else
    echo "Copied into $destination:"
    echo "$copied"
fi
