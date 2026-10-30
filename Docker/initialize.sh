#!/bin/sh
set -eu
dotnet Coiny.API.dll --initialize-hybrid --offline
chown -R 1654:1654 /data /home/app/.aspnet/DataProtection-Keys
