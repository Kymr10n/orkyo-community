#!/bin/sh
set -e

envsubst '${API_BASE_URL}' \
  < /runtime_config.tpl.js \
  > /usr/share/nginx/html/__runtime_config.js

exec nginx -g 'daemon off;'
