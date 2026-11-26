#!/usr/bin/env bash
#
# Runs the test suite against the integration-test database.
# Loads credentials from `.env` (never committed) and forwards extra arguments
# to `dotnet test`, e.g.:
#   ./scripts/test.sh
#   ./scripts/test.sh --filter "FullyQualifiedName~PT.Tests.Unit"
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi

: "${POSTGRES_USER:=postgres}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD must be set in .env}"
: "${POSTGRES_TEST_DB:=pt_test_db_tests}"

export TEST_DB_CONNECTION="Host=127.0.0.1;Port=5432;Database=${POSTGRES_TEST_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

dotnet test "$@"
