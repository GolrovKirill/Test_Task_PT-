#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

# В некоторых версиях Docker Compose (v5) Bake/BuildKit падает с:
# "x-docker-expose-session-sharedkey contains value with non-printable ASCII".
# Classic builder собирает те же Dockerfile без этого бага.
export COMPOSE_BAKE=false
export DOCKER_BUILDKIT=0

docker compose up -d --build "$@"
docker compose ps
