#!/bin/bash
# usage: start-api.sh <log-suffix> [extra env assignments...]  (local only, Development, port 5243)
S=$1; shift
cd "$(dirname "$0")/../../../../Mentora.API"
nohup env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5243 \
  "Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information" "$@" \
  dotnet run --no-build --no-launch-profile > "../docs/review/lot10/10.2b/api-run-$S.log" 2> "../docs/review/lot10/10.2b/api-run-$S.err.log" &
