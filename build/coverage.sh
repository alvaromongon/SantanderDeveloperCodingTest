#!/usr/bin/env bash
# Merges the Cobertura reports produced by `dotnet test --coverage` and fails
# when line coverage is below the threshold (default 80%).
set -euo pipefail

threshold="${COVERAGE_THRESHOLD:-80}"
root="$(cd "$(dirname "$0")/.." && pwd)"
output="$root/artifacts/coverage"

dotnet tool restore > /dev/null
dotnet reportgenerator \
  -reports:"$root/artifacts/TestResults/*.cobertura.xml" \
  -targetdir:"$output" \
  -reporttypes:"Cobertura;TextSummary;MarkdownSummaryGithub;Html" \
  -filefilters:"-*.g.cs;-*/obj/*" \
  -verbosity:Warning

rate="$(grep -o 'line-rate="[0-9.]*"' "$output/Cobertura.xml" | head -1 | grep -o '[0-9.]*')"
coverage="$(awk -v r="$rate" 'BEGIN { printf "%.1f", r * 100 }')"

if awk -v c="$coverage" -v t="$threshold" 'BEGIN { exit !(c < t) }'; then
  echo "Line coverage ${coverage}% is below the ${threshold}% threshold." >&2
  exit 1
fi

echo "Line coverage ${coverage}% (threshold ${threshold}%)."
