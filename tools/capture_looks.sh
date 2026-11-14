#!/usr/bin/env bash
# Reproducible captures of the same scripted scene in every installed look, for side-by-side comparison.
# Usage: tools/capture_looks.sh [script.json] [ticks]   (needs a display; on a headless Linux, prefix with xvfb-run -a)
set -euo pipefail
cd "$(dirname "$0")/.."
script="${1:-tools/scripts/dribble-shot-goal.json}"
ticks="${2:-60,200,320,400}"
last="${ticks##*,}"
dotnet build src/Arcade.Desktop -v quiet -nologo >/dev/null
for theme in $(dotnet run --project src/Arcade.Desktop --no-build -- --list-themes); do
  dotnet run --project src/Arcade.Desktop --no-build -- --mode training --theme "$theme" --script "$script" \
    --capture-dir "artifacts/screenshots/looks/$theme" --capture-ticks "$ticks" --exit-tick "$((last + 2))" 2>&1 \
    | grep "Screenshot saved" || true
done
