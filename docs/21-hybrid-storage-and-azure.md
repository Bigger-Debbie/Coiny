# Storage and hosting

## Database layout

| Store | Data |
| --- | --- |
| PostgreSQL `ControlContext` | Identity users, sessions, recovery codes, households, membership, invitations, household-file registry, migration/maintenance state |
| SQLite `HouseholdContext` | One household's accounts, ledger, snapshots, reconciliation, budgets, payments, savings funds, imports, rules, alerts and detection settings |

The default development mode uses the original combined SQLite `CoinyContext`.
Hybrid mode is explicit configuration; updating the app never migrates data on its own.
`IControlContext` and `IFinancialContext` keep services independent of that choice.
`StorageModelOwnership` lists the entities in each store.

Household filenames are opaque server-generated IDs. Before opening a file, the
API checks the central session, membership, registry state, owning node, schema,
and local identity metadata. Missing storage is an error, not an empty database.
The financial model has local foreign keys; cross-store references use IDs and
explicit checks. Each provider has its own migration chain.

## Writes and recovery

One API process owns a household storage root. Same-household writes and recurring
jobs share a gate; different households can write independently. SQLite WAL files
must remain on the same host, on local storage. This is not a multi-node/HA design.

Cross-store preference updates use a central intent and a local receipt. An
interrupted operation can finish without applying the setting twice. There is no
transaction spanning PostgreSQL and SQLite.

Maintenance uses durable PostgreSQL admission state and an advisory-lock drain.
Stop all API processes/jobs after draining before taking coordinated snapshots.
The pause survives restart until explicitly cleared with its operation UUID.
See [operations](../Docker/operations.md) and the [migration/backup tools](../tools/operations/README.md).

Legacy migration validates a read-only snapshot and reviewed checksum, preserves
IDs and decimal values, journals progress, and verifies copied rows before
activation. Completed migrations do not overwrite newer split data. Keep old and
new installations from accepting writes at the same time.

Household backups restore finances, not users or memberships. Full-installation
recovery restores PostgreSQL, household files, configuration, and protection keys.
See [household backup formats](23-household-backup-restore-export.md).

## Self-hosting and Azure

The [Docker setup](../Docker/README.md) runs Caddy/web, the API, and PostgreSQL.
Caddy handles HTTPS and proxies the API on a private network. The API trusts
forwarded client-IP/scheme headers only from Caddy's configured address. Only
Caddy publishes ports. Hosted and self-hosted installs use the same storage model.

The Azure target is one Linux VM with persistent local managed-disk storage,
Compose, a domain, and Caddy/Let's Encrypt. Do not place SQLite WAL on Azure Files
or temporary VM storage. Build images outside the small hosted VM.

Before public rollout, validate DNS/TLS renewal, SMTP, restarts, resource use,
scheduled encrypted off-VM backups, retention, secret recovery, disk alerts, and
updates/rollback. Azure resources and the final live-data cutover are not deployed.
Review current Azure prices before choosing a VM; the initial budget is $100–125/month.

## Privacy

Separate household files limit data mixing but do not hide data from the server.
TLS and encrypted backups are implemented; client-held keys and zero-knowledge
storage are not. Losing installation protection keys can make linked backups and
reset links unusable; a portable backup needs its separate backup password.
