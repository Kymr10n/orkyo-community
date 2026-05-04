#!/bin/bash
set -e

# Creates the Keycloak database alongside the main application database.
# Runs once when the PostgreSQL container is first initialised.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE "${KEYCLOAK_DB:-keycloak}";
EOSQL
