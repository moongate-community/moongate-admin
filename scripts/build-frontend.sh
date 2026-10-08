#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
frontend_output="$repo_root/frontend/dist"
frontend_destination="$repo_root/src/Moongate.Admin.Api/wwwroot/frontend"
npm --prefix "$repo_root/frontend" run build
if [[ -L "$frontend_destination" ]]; then
  echo 'Refusing to replace a symlinked frontend destination.' >&2
  exit 1
fi
if [[ -d "$frontend_destination" && ! -f "$frontend_destination/.generated-by-moongate-admin" && -n "$(ls -A "$frontend_destination")" ]]; then
  echo 'Refusing to replace files in an unmarked frontend destination.' >&2
  exit 1
fi
mkdir -p "$frontend_destination"
rsync -a --delete -- "$frontend_output/" "$frontend_destination/"
printf '%s\n' 'Generated frontend artifacts; rebuild with scripts/build-frontend.sh.' > "$frontend_destination/.generated-by-moongate-admin"
echo 'Frontend copied to the dedicated generated wwwroot/frontend directory.'
