#!/bin/bash
set -e

# Change owner for our uploads folder
echo -n "Fix permissions for mounted volumes ..." && \
  chown -R app:app $Storage__UploadDirectory && \
  chown -R app:app $Storage__AssetsDirectory && \
  chown -R app:app $PublicAssetsOverride && \
  echo "done!"

# Override public assets in app's public directory.
(cp -R $PublicAssetsOverride/* $HOME/wwwroot/ || true)

echo "
--------------------------------------------------------------------------
http proxy:                       ${PROXY:-no proxy set}
http proxy exceptions:            $([[ -n $NO_PROXY ]] && echo $NO_PROXY || echo undefined)
user uid:                         $(id -u app)
user gid:                         $(id -g app)
timezone:                         $TZ
--------------------------------------------------------------------------
"

echo -e "geopilot app is up and running!\n" && \
  sudo -H --preserve-env --user app dotnet Geopilot.Api.dll
