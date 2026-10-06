#!/usr/bin/env bash
# Builds BankOfRimica.dll for every supported RimWorld version.
set -euo pipefail
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
for v in 1.5 1.6; do
  rm -rf obj bin
  "$DOTNET" build -c Release -p:RimVersion="$v" -nologo -v q
done
rm -rf obj bin
