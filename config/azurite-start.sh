#!/bin/sh
# Starts Azurite and creates the blob container needed by the application.
#
# Azurite uses a well-known fixed dev account (devstoreaccount1) with a public key.
# See: https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite

BLOB_HOST="0.0.0.0"
BLOB_PORT=10000
CONTAINER_NAME="uploads"

# Bind to all interfaces so other containers can reach Azurite via Docker networking.
azurite-blob --blobHost "$BLOB_HOST" --blobPort "$BLOB_PORT" &
AZURITE_PID=$!

until wget -q --spider "http://127.0.0.1:${BLOB_PORT}" 2>/dev/null; do
    sleep 0.5
done

# Azure Storage REST API requires SharedKey auth even for Azurite,
# so we compute the HMAC-SHA256 signature using Node's built-in crypto module.
node -e "
  var http = require('http');
  var crypto = require('crypto');

  var accountName = 'devstoreaccount1';
  var accountKey  = 'Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==';
  var container   = '${CONTAINER_NAME}';
  var apiVersion  = '2020-10-02';
  var date        = new Date().toUTCString();

  // SharedKey signature format: https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-shared-key
  var stringToSign =
    'PUT\n' +
    '\n\n\n\n\n\n\n\n\n\n\n' +
    'x-ms-date:' + date + '\n' +
    'x-ms-version:' + apiVersion + '\n' +
    '/' + accountName + '/' + container + '\n' +
    'restype:container';

  var signature = crypto
    .createHmac('sha256', Buffer.from(accountKey, 'base64'))
    .update(stringToSign)
    .digest('base64');

  var req = http.request({
    hostname: '127.0.0.1',
    port: ${BLOB_PORT},
    path: '/' + accountName + '/' + container + '?restype=container',
    method: 'PUT',
    headers: {
      'x-ms-date': date,
      'x-ms-version': apiVersion,
      'Authorization': 'SharedKey ' + accountName + ':' + signature,
      'Content-Length': 0
    }
  }, function(res) {
    if (res.statusCode === 201) console.log('Container created.');
    else if (res.statusCode === 409) console.log('Container already exists.');
    else console.log('Unexpected status: ' + res.statusCode);
  });

  req.on('error', function(err) {
    console.error('Failed to create container:', err.message);
  });

  req.end();
"

wait $AZURITE_PID
