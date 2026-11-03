# Server logging

Coiny writes safe structured runtime events to the API container's console and,
when configured, a separate rotating JSON Lines journal. No dashboard, external
collector, or Docker control socket is required. The journal is independent of
PostgreSQL and household databases.

## View and export

From `Docker`:

```sh
docker compose logs --follow --tail 100 api
docker compose logs --since 1h --timestamps api web postgres > coiny-server-logs.txt
```

Keep exports private. Web and PostgreSQL output is native service output, not
Coiny's safe API schema. Do not enable SQL statement/parameter logging, request
body logging, authorization-header logging, or raw URL access logs in production.

Use the API response's `X-Request-ID` header to find a request's journal entry.
Events include UTC timestamps, source, severity, a fixed message, and allowlisted
metadata. Request IDs use the request's W3C trace ID (or a generated ID when
unavailable); a valid upstream trace ID can be propagated. These are diagnostic
references, not proof of identity.

## Persistence and limits

The standard Compose file stores the API journal in the `log-data` volume at
`/logs`, separate from financial data and encryption keys. Settings in `.env`:

| Setting | Default | Supported range |
| --- | --- | --- |
| `COINY_LOG_RETENTION_DAYS` | 7 | 1–30 days |
| `COINY_LOG_FILE_SIZE_MB` | 4 | 1–16 MB per file |
| `COINY_LOG_MAX_FILES` | 8 | 2–32 files |

Both age and size limits apply. High traffic can shorten available history. Files
are pruned on startup and writes; an inactive installation can retain files past
the age limit until the next write or restart. The directory is owner-only on
Unix; new journal files have permissions `600`.

Copy retained API files without a dashboard:

```sh
install -d -m 700 ./private-log-export
docker compose cp api:/logs/. ./private-log-export/
```

A copy during active writing can end with a partial JSON line. Ignore that
incomplete line when parsing, or use Docker console output for immediate viewing.
Outside Compose, set `Operations__Logging__Directory` to a private writable absolute
directory. An empty setting keeps console logging and disables file persistence.
External tools can ingest stdout or read-only journal mounts.

Docker also rotates each core container's console output using the `local`
driver: three 10 MB files per container. New settings apply when containers are
recreated; restarting an existing container alone does not change its log driver.
On update, rebuild and recreate through the normal Compose instructions. Preserve
all existing database, household, protection-key and certificate volumes.

## Privacy and diagnostic detail

Collected runtime events cover HTTP completion (method, matched route template,
status, duration, trace ID), unhandled request failures, application warnings and
errors, email delivery failures and recurring-job failures.

The logger never formats arbitrary state, SQL, exception messages or stack traces.
Only a fixed event catalog and allowlisted fields are stored. Request/response
bodies, raw paths and query strings, cookies, authorization headers, email
addresses, passwords and financial values are not captured. Framework warnings
and errors retain category, event ID and exception type, but omit their raw text.
Information/debug framework messages are intentionally not collected. Raising the
framework log level does not bypass this privacy policy. Startup/maintenance CLI
output and native service logs are separate from this runtime provider.

This is operational logging, not a complete transaction audit or a tamper-proof
record. Add diagnostic fields only after checking they cannot contain secrets or
personal data.

## Failures

The API's bounded 2,048-event queue drops rather than blocking requests when full.
A fixed stderr notice reports loss. Graceful shutdown drains accepted entries
within the host shutdown timeout; abrupt failure can lose queued entries.
If journal storage fails, console collection continues, a fixed stderr notice is
emitted, and file writes retry on subsequent events. Logging is not lossless and
does not replace backups, disk monitoring, or external alerts.

For an isolated validation environment, see the
[self-hosted logging lab](../tools/testing/self-hosted-logging/README.md).
