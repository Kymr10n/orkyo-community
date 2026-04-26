#!/bin/sh
set -e

# If node_modules is empty (fresh volume), install dependencies
if [ ! -d "node_modules" ] || [ -z "$(ls -A node_modules 2>/dev/null)" ]; then
  echo "📦 Installing npm dependencies..."
  npm install
fi

# Execute the main command
exec "$@"
