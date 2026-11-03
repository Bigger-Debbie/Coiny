# Self-hosted logging lab

A synthetic installation of the public app, using the actual self-hosting Compose
file, production API/web images, PostgreSQL 17, and Caddy HTTPS. This lab does not
import or mount data, credentials or keys from any existing installation.

Requires Python 3, Docker Engine/Desktop with Compose v2, free loopback ports 6084
and 6443, and a free Docker subnet `172.30.66.0/24`. Run from the repository root:

```sh
python3 tools/testing/self-hosted-logging/lab.py up
python3 tools/testing/self-hosted-logging/lab.py smoke
python3 tools/testing/self-hosted-logging/lab.py test
```

`up` generates private random secrets in the ignored `.env`, builds the public
images, initializes **empty lab storage only**, and starts the installation at
**https://localhost:6443**. Initialization refuses an existing database; preserve
volumes and investigate if it fails. Never delete volumes to retry an upgrade.

The lab uses project `coiny-selfhost-logging-lab`, versioned lab-only image tags,
its own networks and prefixed named volumes. Only loopback ports are published.
If the subnet conflicts, change both `COINY_PROXY_SUBNET` and `COINY_PROXY_IP` in
the lab's `.env`. Do not change the project name or point it at existing volumes.

Caddy's local CA is copied to ignored `.runtime/root.crt`; smoke requests trust
that certificate explicitly. No certificate is installed in your computer's
trust store. Browsers may show a local certificate warning unless you separately
choose to trust this lab CA. HTTP port 6084 redirects to HTTPS; use the explicit
6443 URL because Caddy's standard redirect does not know the host port mapping.

`smoke` creates a synthetic user and household on first run, then tests sign-in,
financial API routing, session revocation, safe console/file output, request IDs,
rotation configuration and project-specific volumes. Repeat runs reuse the
synthetic account. Its generated credentials are in ignored
`.runtime/smoke-credentials.json` if you want to sign in manually.

`test` builds the integration runner and runs the complete API suite with a
dedicated PostgreSQL test container on its own isolated network, separate from
the lab application database. It also runs
the backup-tool checks during the image build. Results are saved privately in
`.runtime/test-results.txt`; failures go to `.runtime/last-error.txt`.

```sh
python3 tools/testing/self-hosted-logging/lab.py status
python3 tools/testing/self-hosted-logging/lab.py logs
python3 tools/testing/self-hosted-logging/lab.py down
```

`down` stops the lab and preserves **all** volumes. There is deliberately no
reset/delete-data command. The lab's `.env`, credentials, certificates, exports
and diagnostics stay ignored. Do not commit them or share them as source files.

For installation logging commands and retention settings, see
[server logging](../../../Docker/logging.md).
