-- Creates additional databases needed for integration and E2E testing.
-- This script runs automatically on first postgres container initialization (initdb).
-- Pre-existing volumes do NOT execute this script.
--
-- To re-run (after destroying the volume):
--   docker compose down -v && docker compose --profile infra up -d
--
-- For existing volumes, run once manually:
--   docker exec project-postgres-1 psql -U mybudget -c "CREATE DATABASE mybudget_test;"
--   docker exec project-postgres-1 psql -U mybudget -c "CREATE DATABASE mybudget_e2e;"

SELECT 'CREATE DATABASE mybudget_test'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'mybudget_test')\gexec

SELECT 'CREATE DATABASE mybudget_e2e'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'mybudget_e2e')\gexec
