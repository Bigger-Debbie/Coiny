# Local prototype evidence

Historical synthetic POC evidence, retained after its lab was retired. This is not
the current full-app test baseline; see [verification](../../docs/06-development-setup.md#verification).


All ten synthetic-data checks passed using the two-container Compose deployment.
This is an observed local validation run, not a hosted capacity benchmark.

| Check | Result |
| --- | --- |
| Household isolation | Passed |
| Concurrent imports and retry safety | Passed |
| Independent writers and WAL reads | Passed |
| Interrupted provisioning | Passed |
| Interrupted schema migration | Passed |
| Backup, restore, and current permissions | Passed |
| Household node ownership | Passed |
| Incorrect registry mapping | Passed |
| Container recreation and startup recovery | Passed |
| Control database outage | Passed |

The import test used 20 operations from eight concurrent clients, inserting 5,000
rows across two household files and verifying exact integer-cent balances.

With a write transaction held in household A for 4,000 ms:
- Household B completed a 100-row import in 5.9 ms.
- Household A read its committed financial snapshot in 2.0 ms.
- A second writer in household A waited for its own household write queue.

App and PostgreSQL containers were recreated with retained named volumes.
Pending provisioning and restore work recovered at app startup; revoked membership
remained revoked. A control-database outage denied financial access until recovery.

See `EVIDENCE.json` for measured results, `README.md` for test boundaries and Azure
storage implications, and `lab.py` to repeat the checks. Only synthetic data was used.
No existing Coiny code, financial database, or migrations were changed.

This evidence does not establish an Azure deployment, VM/disk-loss recovery,
distributed ownership fencing, production encryption, or a migration of the real
Coiny EF/Identity/financial models.
