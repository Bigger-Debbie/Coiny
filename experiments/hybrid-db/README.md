# Coiny hybrid database laboratory

Historical architecture proof: its containers and test volumes were retired after
real-app integration. The commands below explicitly recreate the synthetic POC;
they are not required for normal development or the retained review/recovery labs.
The implementation is now merged into `main`; see the
[current storage guide](../../docs/21-hybrid-storage-and-azure.md).


This is an isolated architecture experiment, not a replacement Coiny build.
The main application, its SQLite database, and its existing migrations are not
referenced, mounted, copied, or modified by the lab. All users and transactions
are synthetic. Do not expose this lab to the public internet.

## Layout

- **PostgreSQL 17 container:** synthetic users, membership/roles, household registry,
  opaque storage keys, owning-node identifiers, provisioning/maintenance state, and
  backup metadata.
- **.NET 10 app container:** signed synthetic-user JWT validation, authorization
  against PostgreSQL before file access, per-household SQLite stores in WAL mode,
  per-household write queues, and local operation/maintenance coordination.
- **Two named volumes:** `coiny-hybrid-lab_control-data` for PostgreSQL and
  `coiny-hybrid-lab_household-data` for financial files/backups. These are lab-only.
- **One localhost port:** `127.0.0.1:6088`. PostgreSQL has no host port published.

Every household filename comes from a server-generated UUID in the registry,
never a client-supplied path. The SQLite file also contains a household identity
marker, which is checked before financial access. Financial routes require a
signed synthetic user's bearer token; the lab administrative key cannot replace
that identity. Authorization is checked per request; already-authorized in-flight
operations are not retrospectively canceled by a membership change.

## Run

Requirements: Docker Desktop with Linux containers, Docker Compose, Python 3.
No local PostgreSQL installation or cloud account is required.

```sh
cd experiments/hybrid-db
python3 lab.py init
python3 lab.py up
python3 lab.py test --lifecycle
python3 lab.py status
```

`init` creates `.env` with random lab-only credentials and mode 0600. It is ignored
by Git and Docker's build context. Do not paste its contents into reports.

The verification uses Python's standard library. A machine-readable report goes
to `results/latest.json`, without bearer tokens or credentials. `--lifecycle`
recreates the lab app and PostgreSQL containers and briefly stops PostgreSQL.
It does not remove either data volume. Checks can also run without lifecycle
operations with `python3 lab.py test`.

```sh
python3 lab.py down
```

`down` stops/removes lab containers and their network but deliberately preserves
volumes. It does not stop the normal Coiny API or Angular dev server. Docker volume
removal is intentionally not automated. Keep `.env` with retained lab volumes:
regenerating a different PostgreSQL password does not change the existing volume's
stored password.

Local compilation, independent of Docker:

```sh
dotnet build Lab/Lab.csproj
```

## Scenarios

1. Anonymous, outsider, cross-household, and nonexistent-household requests fail
   before financial operations; inventory and the financial-file open counter remain unchanged.
2. Twenty imports from eight concurrent clients insert 5,000 rows across two households, with exact
   integer-cent balances. Replayed operations are idempotent; mismatched replay
   bodies are rejected. Two members can write to the same household.
3. While A holds a real SQLite write transaction for four seconds, B can commit
   an import and A can read committed data using WAL. Another A writer queues.
   Timings are observations from the local test, not capacity guarantees.
4. Provisioning interrupted after the PostgreSQL record or after file creation
   resumes idempotently without duplicate household files/data.
5. An additive SQLite schema upgrade from v1 to v2 can finish after its SQL commit
   but before its PostgreSQL status update; recovery recognizes the existing
   column/version and completes without repeating the ALTER.
6. Consistent SQLite Backup API snapshots can restore one household. A persisted
   restore marker lets recovery resume the operation. Restoring financial data
   does not restore revoked membership or change another household. Cross-household
   backup restoration is rejected.
7. A household assigned to another owning node fails closed. Deliberately swapped
   registry mappings also fail closed after the SQLite household identity check.
8. App and PostgreSQL container recreation preserves data and revoked permissions.
   App startup resumes pending provisioning and restore operations.
9. PostgreSQL outage denies financial access instead of trusting a cached membership
   or falling back to opening a known SQLite file. Access resumes after recovery.

The operations that cross databases use explicit PostgreSQL states, idempotent
file initialization/upgrades, and durable pending-restore metadata. There is no
transaction spanning both database engines.

## Boundaries of this evidence

- One owning app process on one Docker host. In-process queues are **not**
  distributed leases or fencing. Do not scale this app service to multiple replicas.
  Node ownership checks demonstrate routing boundaries, not automatic failover.
- Small standalone schemas using Npgsql and Microsoft.Data.Sqlite, not a port of
  Coiny's EF models, ASP.NET Identity, financial calculations, or migrations.
  Splitting the real contexts, cross-database user references, session revocation,
  atomic preference updates, and all existing features still require a migration
  design and integration/regression tests.
- Schema tests deliberately use small explicit SQL migrations. They do not prove
  that existing Coiny migrations can move unchanged between providers.
- Fixture JWT issuance and `/lab/*` administrative endpoints are lab-only. They
  are gated by a random lab key but are not production account/admin features.
  There is no production registration/password/reset implementation in this lab.
- Backup/restore is a mechanical prototype, not the final user-facing feature.
  There is no off-host retention, encrypted backup storage, cleanup policy,
  PostgreSQL point-in-time recovery, or complete multi-layer disaster recovery.
- Financial files and server-readable keys do not provide zero-knowledge privacy.
- Local container recreation is tested. VM loss, disk loss/corruption, forced
  process termination mid-write, multi-node failover, and Azure deployment are not
  established by these checks. Simulated interruption points stop operations at
  explicit boundaries; they are not a broad crash-fault campaign.

## Azure implications

Containers do not require Azure Container Apps. That service currently provides
persistent Azure Files mounts, while container/replica-local storage is ephemeral.
SQLite WAL is not supported on network filesystems, so simply mounting a shared
Azure Files directory is not an appropriate direct deployment of this prototype.

A plausible first Azure test is a Linux VM running the same Compose services,
with household and PostgreSQL volumes placed on persistent Azure managed data
disk storage and a single owning app process. Managed disks are block storage
presented to the VM. An AKS design with Azure Disk is another option, but adds
cluster operations and single-node volume attachment/routing concerns. Neither
option has been deployed or validated by this local lab. No Azure resources or
costs are created by these commands.

Sources:

- [SQLite WAL concurrency and filesystem restrictions](https://www.sqlite.org/wal.html)
- [SQLite Online Backup API](https://www.sqlite.org/backup.html)
- [Azure Container Apps storage](https://learn.microsoft.com/en-us/azure/container-apps/storage-mounts)
- [Azure managed disk overview](https://learn.microsoft.com/en-us/azure/virtual-machines/managed-disks-overview)
- [AKS Azure Disk CSI](https://learn.microsoft.com/en-us/azure/aks/csi-storage-drivers)

Before integrating: agree on the system/household entity boundary; choose and test
persistent Azure storage; design provider-specific EF migrations and existing-data
conversion on disposable copies; define ownership/fencing, maintenance authorization,
backup consistency, and recovery objectives. Keep this branch out of main while
those decisions are open.
