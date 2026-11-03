# Development Docker helpers

These are test environments, not self-hosting instructions. For an installation,
use [Docker](../../Docker/README.md).

| Helper | Purpose |
| --- | --- |
| [self-hosted-logging](self-hosted-logging/README.md) | Isolated public self-hosting installation, safe logging checks, HTTPS and PostgreSQL tests |
| [docker-test](docker-test/README.md) | Synthetic demo deployment, PostgreSQL integration tests, migration/maintenance checks |
| [recovery-lab](recovery-lab/README.md) | Separate empty installation for manual portable restore tests |

The retired architecture experiments are removed. Existing review/recovery
installations may contain personal data: moving these source files does not move
or delete their containers, volumes, configuration, or keys. Preserve those stores.
