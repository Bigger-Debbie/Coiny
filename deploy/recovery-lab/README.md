# Fresh household recovery lab

Independent, loopback-only recovery destination at **http://localhost:6082**.
The personal review installation remains at http://localhost:6081 with its own
stores and keys. The original development database is not mounted into this lab.

```sh
python3 deploy/recovery-lab/recovery-lab.py up
python3 deploy/recovery-lab/recovery-lab.py status
python3 deploy/recovery-lab/recovery-lab.py smoke
python3 deploy/recovery-lab/recovery-lab.py logs
python3 deploy/recovery-lab/recovery-lab.py down
```

`up` builds separate API/web image tags, generates ignored mode-0600 `.env`
configuration, and initializes empty PostgreSQL control storage through the explicit
`--initialize-hybrid --offline` command. This bootstrap refuses existing control
schemas or nonempty household storage, takes the household root lock, and never
seeds demo data or resets an existing database. Its completion marker is ignored.
Later `up` calls preserve user-created data. An interrupted initialization may need
operator inspection; do not delete stores or markers to bypass a failed check.

The lab has its own Compose project, network, PostgreSQL database volume, household
SQLite volume and Data Protection key volume. Only Caddy publishes a host port,
bound to `127.0.0.1:6082`; PostgreSQL/API have no host ports. Scheduled recurring
scans and outgoing password-recovery email are disabled for this manual rehearsal.
This is local Development configuration, not a public/production deployment.

## Manual restore test

1. On the personal review site (6081), open **Settings → Backup & export**.
   Download a **portable password-protected backup** and save its backup password.
2. Open the recovery lab (6082) and register a new user. Its credentials may differ
   from the source account. Create a new household; no budget setup or bank-account
   entry is needed before restoring from Settings.
3. Choose **Settings → Backup & export → Choose backup**. Select the `.coiny` file,
   provide its backup password, and authorize using the **new recovery-lab account's**
   current password.
4. Review the account/transaction counts. Confirm replacement, re-enter the new
   account password, and restore. Review accounts, budgets, funds and reconciliation.
5. The new user/membership remains. An installation-linked safety backup of the
   recovery household's pre-restore state appears in Settings.

An installation-linked file from 6081 is expected to fail in this lab, because its
recovery keys and household identity are not imported. Use portable mode to test
cross-installation recovery. The backup password is the one chosen for the file,
not automatically either site's account password.

The smoke check verifies UI/deep links/API authentication and disabled scheduling;
it deliberately does not register a user, create a household or apply a restore.
The initial bootstrap was verified with zero users, households and registry rows.
The user has since created a new account/household and successfully restored a
portable backup. The retained lab is therefore no longer empty. A freshly created
lab with new volumes starts empty; `up` does not reset an existing one.
After you restore personal data, these volumes contain private financial information
and should be retained until their deletion is explicitly authorized. `down` stops
the lab but retains all volumes/keys. No reset or automatic volume-deletion command
is supplied.

## Cleanup performed before this lab

The obsolete `coiny-hybrid-lab` POC and `coiny-test` running lab were removed with
their labeled containers, networks and test-data volumes. The personal review site,
private backup artifacts, source worktrees, shared images and build caches were
preserved. The synthetic test runner can recreate its own test project on demand;
that does not make the recovery lab disposable after personal data is restored.
