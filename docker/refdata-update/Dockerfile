FROM alpine:3 AS base
RUN apk add --no-cache tzdata yq-go
COPY --chmod=755 refdata-update.sh /usr/local/bin/refdata-update
COPY --chmod=755 entrypoint.sh /usr/local/bin/entrypoint

# Runs the self-check; built explicitly with --target test, the final image does not contain it.
FROM base AS test
RUN apk add --no-cache busybox-extras zip
COPY --chmod=755 test.sh /usr/local/bin/refdata-update-test
RUN refdata-update-test

FROM base
ENTRYPOINT ["/usr/local/bin/entrypoint"]
