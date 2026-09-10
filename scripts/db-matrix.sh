#!/usr/bin/env bash
# W10.T5 — provider matrix: run the database contract (IModelRegistryMethods + presets + request-history)
# against PostgreSQL, MySQL, and SQL Server using ephemeral Docker containers, then tear them down.
# SQLite is covered by the in-process Touchstone DatabaseSuite/ModelPresetSuite and needs no container.
#
# Usage:  scripts/db-matrix.sh
# Requires: docker, dotnet. Ports 15432 / 33066 / 11433 must be free.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUNNER="$ROOT/src/Test.Automated/Test.Automated.csproj"
FAIL=0

cleanup() {
  docker rm -f sharpai-pg sharpai-mysql sharpai-mssql >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "== starting containers =="
docker rm -f sharpai-pg sharpai-mysql sharpai-mssql >/dev/null 2>&1 || true
docker run -d --name sharpai-pg    -e POSTGRES_PASSWORD=sharpai -e POSTGRES_DB=sharpai -p 15432:5432 postgres:16-alpine >/dev/null
docker run -d --name sharpai-mysql -e MYSQL_ROOT_PASSWORD=sharpai -e MYSQL_DATABASE=sharpai -p 33066:3306 mysql:8.4 >/dev/null
docker run -d --name sharpai-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=SharpAI_Pass123 -p 11433:1433 mcr.microsoft.com/mssql/server:2022-latest >/dev/null

echo "== waiting for readiness =="
for i in $(seq 1 60); do docker exec sharpai-pg pg_isready -U postgres >/dev/null 2>&1 && break; sleep 2; done
for i in $(seq 1 60); do docker exec sharpai-mysql mysqladmin ping -uroot -psharpai >/dev/null 2>&1 && break; sleep 2; done
for i in $(seq 1 90); do MSYS_NO_PATHCONV=1 docker exec sharpai-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SharpAI_Pass123' -C -Q "SELECT 1" >/dev/null 2>&1 && break; sleep 2; done
MSYS_NO_PATHCONV=1 docker exec sharpai-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SharpAI_Pass123' -C -Q "IF DB_ID('sharpai') IS NULL CREATE DATABASE sharpai;" >/dev/null 2>&1

run() { # provider host port user pass
  echo "== $1 =="
  SHARPAI_DBTEST_PROVIDER="$1" SHARPAI_DBTEST_HOST=127.0.0.1 SHARPAI_DBTEST_PORT="$2" \
    SHARPAI_DBTEST_DB=sharpai SHARPAI_DBTEST_USER="$3" SHARPAI_DBTEST_PASS="$4" \
    dotnet run --project "$RUNNER" -c Debug -f net8.0 -- --dbmatrix 2>/dev/null | grep -iE "provider:|checks passed|FAIL" || FAIL=1
}

run postgresql 15432 postgres sharpai
run mysql      33066 root     sharpai
run sqlserver  11433 sa       SharpAI_Pass123

echo "== done (FAIL=$FAIL) =="
exit $FAIL
