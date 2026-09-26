#!/usr/bin/env bash
# Builds a BOCA package for an rbx problem and imports it into patito over
# /api/boca-import (see docs/problem-import-format.md and BocaPackageReader.cs
# for why the format lines up: `rbx package boca` already emits the classic
# BOCA zip layout that endpoint expects).
#
# rbx's own `--upload` flag can't be repointed at patito by swapping a domain:
# it scrapes the *classic BOCA PHP admin* (a login page with a JS-salted
# password hash, then a specific HTML form on /admin/problem.php). Patito's
# admin is a JSON REST API with JWT auth, so this script does the equivalent
# two calls (login, then preview+confirm) instead of reusing that scraper.
#
# Usage:
#   PATITO_BASE_URL=https://admin.patito.example \
#   PATITO_USER=admin PATITO_PASSWORD=secret PATITO_SITE_ID=1 \
#   ./scripts/rbx-boca-upload.sh [path/to/problem]
#
# Requires: rbx, curl, python3 (for JSON, so no jq dependency).
set -euo pipefail

problem_dir="${1:-.}"
: "${PATITO_BASE_URL:?set PATITO_BASE_URL, e.g. https://admin.patito.example}"
: "${PATITO_USER:?set PATITO_USER}"
: "${PATITO_PASSWORD:?set PATITO_PASSWORD}"
: "${PATITO_SITE_ID:?set PATITO_SITE_ID}"

zip_rel=$(
  cd "$problem_dir"
  rbx package boca >&2
  ls -t build/*.zip | head -n1
)
zip_path="$(cd "$problem_dir" && pwd)/$zip_rel"
echo "Built $zip_path" >&2

token=$(
  curl -sf -X POST "$PATITO_BASE_URL/api/public/auth/login" \
    -H 'Content-Type: application/json' \
    -d "$(python3 -c 'import json,sys; print(json.dumps({"userId": sys.argv[1], "password": sys.argv[2], "siteId": int(sys.argv[3])}))' \
      "$PATITO_USER" "$PATITO_PASSWORD" "$PATITO_SITE_ID")" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])'
)

preview=$(curl -sf -X POST "$PATITO_BASE_URL/api/boca-import/preview" \
  -H "Authorization: Bearer $token" \
  -F "files=@$zip_path")

echo "$preview" | python3 -m json.tool >&2

staging_id=$(echo "$preview" | python3 -c '
import json, sys
result = json.load(sys.stdin)["results"][0]
if not result["success"]:
    sys.exit("preview failed: " + result.get("error", "unknown error"))
print(result["stagingId"])
')

confirm=$(curl -sf -X POST "$PATITO_BASE_URL/api/boca-import/confirm" \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"stagingIds\": [\"$staging_id\"]}")

echo "$confirm" | python3 -m json.tool
