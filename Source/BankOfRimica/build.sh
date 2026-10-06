#!/usr/bin/env bash
# Builds BankOfRimica.dll for RimWorld 1.6 (Odyssey required).
set -euo pipefail
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
for v in 1.6; do
  rm -rf obj bin
  "$DOTNET" build -c Release -p:RimVersion="$v" -nologo -v q
done
rm -rf obj bin
