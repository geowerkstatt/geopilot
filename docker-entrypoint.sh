#!/bin/bash
set -e

# Sets the umask from the docker default 0022, to 0002. This has the effect that newly created files and directories
# will have the group write permission set. With the default 0022, groups that own these directories won't be able to edit them.
umask 0002

if [ "$(id -u)" -eq 0 ]; then
  # Use default user:group if no $PUID and/or $PGID is provided.
  groupmod -o -g ${PGID:-1654} app && \
    usermod -o -u ${PUID:-1654} app &> /dev/null

  # Change owner for our mounted data folders
  echo -n "Fix permissions for mounted volumes ..." && \
    chown -R app:app $Storage__DownloadDirectory && \
    chown -R app:app $Storage__AssetsDirectory && \
    chown -R app:app $Storage__PipelineDirectory && \
    chown -R app:app $Storage__ResourcesDirectory && \
    chown -R app:app $Storage__VisualizationDirectory && \

    # Sets group permission and sticky bit at the end, which makes all children inherit group ownership
    chmod -R g+rwXs $Storage__DownloadDirectory && \
    chmod -R g+rwXs $Storage__AssetsDirectory && \
    chmod -R g+rwXs $Storage__PipelineDirectory && \
    chmod -R g+rwXs $Storage__ResourcesDirectory && \
    chmod -R g+rwXs $Storage__VisualizationDirectory && \
    echo "done!"

  # Trust additional CA certificates if present (for Azurite HTTPS in development).
  for pem in /https/*.pem; do
    [ -f "$pem" ] && cp "$pem" "/usr/local/share/ca-certificates/$(basename "$pem" .pem).crt"
  done
  update-ca-certificates 2>/dev/null

  app_uid=$(id -u app)
  app_gid=$(id -g app)
else
  # The platform chose the user, so it also owns volume permissions and trusted certificates.
  echo "Running as non-root user $(id -u):$(id -g), skipping:"
  echo "  - PUID/PGID user mapping"
  echo "  - permission fix for mounted volumes"
  echo "  - installation of CA certificates from /https"

  app_uid=$(id -u)
  app_gid=$(id -g)
fi

echo "
--------------------------------------------------------------------------
http proxy:                       ${PROXY:-no proxy set}
http proxy exceptions:            $([[ -n $NO_PROXY ]] && echo $NO_PROXY || echo undefined)
user uid:                         $app_uid
user gid:                         $app_gid
timezone:                         $TZ
--------------------------------------------------------------------------
"

echo -e "geopilot app is up and running!\n"
if [ "$(id -u)" -eq 0 ]; then
  exec gosu app dotnet Geopilot.Api.dll
fi
exec dotnet Geopilot.Api.dll
