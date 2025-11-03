#!/bin/bash

VERSION=$1

if [ -z "${VERSION}" ]; then
    echo "Error: Version of packages for publication is not specified" >&2
    exit 1
fi

ENV_FILE=".env"

if [ -f "$ENV_FILE" ]; then
    set -a
    source "$ENV_FILE"
    set +a
fi

if [ -z "${NUGET_KEY}" ]; then
    echo "Error: Variable NUGET_KEY not found or empty" >&2
    exit 1
fi

dotnet pack --output nupkgs -p:PackageVersion=${VERSION}

dotnet nuget push nupkgs/Jobby.Core.${VERSION}.nupkg --api-key ${NUGET_KEY} --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs/Jobby.Postgres.${VERSION}.nupkg --api-key ${NUGET_KEY} --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs/Jobby.AspNetCore.${VERSION}.nupkg --api-key ${NUGET_KEY} --source https://api.nuget.org/v3/index.json