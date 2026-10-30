#!/bin/sh
set -eu
if [ "${HybridStorage__Enabled:-false}" = "true" ]; then
    exec dotnet Coiny.API.dll
fi
if [ -f /data/.hybrid-migration-started ]; then
    echo "This test volume began hybrid migration. Run test-stack.py migrate-hybrid; refusing stale legacy mode." >&2
    exit 1
fi
# Fixed path in the coiny-test volume; never mounts the development database.
if [ ! -f /data/.demo-ready ]; then
    if [ -f /data/coiny-test.db ]; then
        echo "Test database exists without its completed seed marker; refusing to overwrite it." >&2
        exit 1
    fi
    dotnet Coiny.API.dll --seed-demo
    touch /data/.demo-ready
fi
exec dotnet Coiny.API.dll
