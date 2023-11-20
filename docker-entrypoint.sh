#!/bin/bash
set -e

# Change owner for our uploads folder
echo -n "Fix permissions for mounted volumes ..." && \
  chown -R abc:abc $GEOCOP_UPLOADS_DIR && \
  echo "done!"


echo "
--------------------------------------------------------------------------
http proxy:                       ${PROXY:-no proxy set}
http proxy exceptions:            $([[ -n $NO_PROXY ]] && echo $NO_PROXY || echo undefined)
user uid:                         $(id -u abc)
user gid:                         $(id -g abc)
timezone:                         $TZ
--------------------------------------------------------------------------
"

echo -e "geocop app is up and running!\n" && \
  sudo -H --preserve-env --user abc dotnet GeoCop.Api.dll
