#!/bin/bash
set -e

read -rsp "Thunderstore API Token: " TOKEN
echo

tcli publish \
  --config-path src/Thunderstore/thunderstore.toml \
  --token "$TOKEN"