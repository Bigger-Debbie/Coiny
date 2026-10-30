# Self-host Coiny

Coiny runs as three containers: Caddy serves the website and HTTPS, the API runs
Coiny, and PostgreSQL holds users and household registrations. Each household's
financial data lives in its own SQLite file. Only Caddy exposes ports.

## Requirements

- A Linux host with Docker Engine and Compose v2, or Docker Desktop for local testing.
- A domain pointing at the host. Allow inbound TCP 80/443 for Caddy and certificate renewal.
- Persistent local storage. Keep SQLite on a local filesystem, not an SMB/NFS share.

The images build from this checkout. A published image registry is not configured.

## Install

From the repository root:

```sh
cd Docker
cp .env.example .env
chmod 600 .env
```

Edit `.env`: set `COINY_HOST` to your domain and `ACME_EMAIL` to your email.
Generate two different secrets with `openssl rand -hex 32` and use them for
`POSTGRES_PASSWORD` and `JWT_KEY`. Keep `.env` private and backed up.

```sh
docker compose build
docker compose up -d --wait postgres
docker compose run --rm --no-deps --user 0:0 --entrypoint sh api /app/initialize.sh
docker compose up -d
docker compose logs --tail 50 api web
```

Initialization is for **new, empty storage only**. It creates no demo users and
refuses an existing database. Do not run it again during an update. If it fails,
inspect the logs and preserve the volumes; deleting data is not a recovery step.

Open `https://YOUR_DOMAIN`, register, and create a household. Registration is open;
this version has no installation-wide invitation-only switch. Caddy obtains and
renews certificates automatically. Its `/data` volume keeps certificate state.

For a local test, `COINY_HOST=localhost` uses Caddy's local certificate authority;
your browser must trust that CA. This is separate from a public Let's Encrypt setup.

## Email and account recovery

SMTP is optional. Configure `SMTP_HOST`, `SMTP_PORT` (normally 587), `SMTP_FROM`,
`SMTP_USERNAME`, and `SMTP_PASSWORD` for STARTTLS email resets. Without SMTP, users
should generate offline recovery codes in **Settings → Security** while signed in.
The server does not yet have an operator command to issue an account reset link.

## Storage

| Volume | Contents |
| --- | --- |
| `control-data` | PostgreSQL users, memberships, sessions and household registry |
| `household-data` | Household SQLite files and local recovery files |
| `protection-keys` | Keys for reset links and installation-linked backups |
| `caddy-data`, `caddy-config` | HTTPS state and Caddy configuration |

Keep the Compose project name (`coiny` by default), volume names, and storage node
identity stable. Changing the PostgreSQL password in `.env` does not change an
existing database user's password. Rotate it deliberately on both sides.

Caddy has a fixed address on a dedicated Docker subnet. The API accepts forwarded
scheme/client-IP headers only from that address. If the subnet conflicts with an
existing network, change both `COINY_PROXY_SUBNET` and `COINY_PROXY_IP` before
installation. Do not publish the API or PostgreSQL ports or put another proxy in
front without reviewing that trust configuration.

## Back up, update, and recover

See [operations](operations.md) for the commands. A single household can also use
**Settings → Backup & export**. Portable password-protected backups can restore to
a different installation; installation-linked files need the original keys and
household identity. CSV export is for reading data, not restoring it.

To stop without removing data, run `docker compose down`. **Do not add `-v`**
unless you intend to delete the installation's volumes.
