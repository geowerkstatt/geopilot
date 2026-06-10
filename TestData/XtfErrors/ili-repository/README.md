# Local INTERLIS model repository

This folder is a local INTERLIS model repository mounted into the
`interlis-check-service` container at `/repository` (see `docker-compose.yml`).

ilivalidator discovers models here through the repository index `ilimodels.xml`,
so any XTF that references a model listed below can be validated without the model
being published on a public repository.

## Layout

- `ilimodels.xml` repository index. Each `ModelMetadata` entry maps a model name
  and version to its `.ili` file and an `md5` checksum of that file.
- `*.ili` the model files referenced by the index.

## Adding a model

1. Copy the `.ili` file into this folder.
2. Compute its MD5 and add or update a `ModelMetadata` entry in `ilimodels.xml`
   with the model `Name`, `Version`, `File` and `md5`.
3. Recreate the container so the mount picks up the change:
   `docker compose up -d --force-recreate interlis-check-service`.

The `md5` must match the file exactly, otherwise ilivalidator rejects the cached model.
