#!/bin/bash
set -e

dotnet clean src/REPOJapaneseTranslation.csproj -c Release
dotnet build src/REPOJapaneseTranslation.csproj -c Release
tcli build --config-path src/Thunderstore/thunderstore.toml