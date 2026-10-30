# Account security

The profile menu’s **Settings** item opens `/settings` on desktop and mobile. Its
**Security** section appears alongside Household and Preferences. Users can change
their password, manage offline recovery codes, inspect active sessions, revoke another browser, or sign out
everywhere. Password changes and signing out everywhere require the current
password and invalidate all existing sessions, including the current browser.

The login page links to password recovery. Reset links expire after 30 minutes,
are single-use after a successful reset, and are invalidated by a password change
or sign-out-everywhere action. Resetting a password ends all existing sessions and
clears a temporary login lockout. Recovery responses do not disclose whether an
email address has an account. Recovery does not sign the user in automatically.

## Recovery codes for self-hosted installations

SMTP is optional. Before losing access, open **Profile → Settings → Security → Recovery
codes**, enter the current password, and generate a set of ten codes. Copy them
into a password manager or download the text file, then select **I've saved my
codes**. Plaintext codes are displayed only in the generation response/view and
are not persisted in browser storage or retrievable from the status endpoint.
Leaving the page loses that display; generate a replacement set if necessary.
Downloaded files contain secrets and should be stored privately.

Each code contains 128 bits of cryptographic randomness. The database stores
only SHA-256 hashes bound to the user and current Identity security stamp.
Generating replacements or revoking the set requires the current password.
Generating replacements invalidates all previous codes; revoking disables code
recovery until a new set is generated. Neither action signs out existing sessions.

On **Forgot password**, Coiny loads installation-wide recovery options without
looking up an account. With SMTP configured, the email form and code alternative
are available. Without SMTP, the page offers recovery codes and guidance to
contact the installation administrator. **Use a recovery code** opens
`/recover-with-code`: enter the account email, a saved code, and a new password.
Code input ignores case, spaces, and hyphens.

A successful code recovery updates the password, clears temporary password
lockout, invalidates every session, and deletes the entire recovery-code set in
one transaction. Only one concurrent redemption can succeed. Incorrect codes,
password-policy failures, or storage failures do not consume usable codes or
partially update the password. Invalid email/code/password combinations receive a
generic error. Recovery attempts have an independent IP limit of five per 15
minutes and do not increment the account's password-lockout counter.

Password changes, email resets, and sign-out-everywhere also invalidate the entire
code set. Sign in and generate fresh codes afterward. A normal session logout or
individual session revocation does not invalidate them. There is no expiry date
for saved codes while the set and security stamp remain current.

Codes must be generated while signed in; a locked-out user cannot create new
ones. A server-side recovery command is planned alongside container packaging:
it will require explicit server/container access and generate a short-lived reset
link for a specified account. That command is not implemented in this feature.
A household role alone does not grant permission to reset another member's login.
Encrypted household backup recovery is implemented separately from login recovery;
see [backup/restore](23-household-backup-restore-export.md). An account password
reset does not reset a portable backup password or recover lost installation keys.

## Sessions and login protection

- Login and registration issue the existing bearer-token response (`token`, `email`)
  backed by an `AuthSessions` row. Each protected request checks the session, expiry,
  user, and Identity security stamp. Legacy tokens without a matching session are
  rejected: users must sign in again after this upgrade.
- Default expiry remains two hours (`Jwt:ExpirationMinutes`); there is no automatic
  renewal or remember-me session. JWT clock skew is zero. Keys must contain at least
  32 bytes, with expiry between 1 and 1,440 minutes.
- Logout revokes the current server session and removes the browser token. If the
  server is unreachable, local sign-out still completes and a warning explains
  that server revocation failed; the session remains subject to normal expiry.
- Session lists are user-scoped, with browser/platform labels and creation/expiry
  dates. Revocation cannot target another user's sessions. Expired/revoked rows are
  pruned at that user's next successful login.
- Five failed password checks lock an account for 15 minutes. Existing identities
  that predate lockout have it enabled when a password check is attempted.
- Login, registration, reset, password changes, and sign-out-everywhere share an IP
  limit of 10 attempts per minute. Recovery requests have a separate limit of three
  per 15 minutes per IP. Rejections return 429 with `Retry-After` when available.
- Passwords require at least eight characters with uppercase/lowercase letters,
  a number and a symbol; requests are capped at 128 characters. Existing password
  hashes remain valid; no forced password rewrite occurs during migration.
- The frontend attaches tokens only to the configured Coiny API. A protected API
  401 clears the matching session and sends the user to login. An older request
  cannot clear a newer login. Storage changes refresh other tabs to prevent stale
  profile/household views after signing in or out.

## Recovery email configuration

Configure `RecoveryEmail` through environment variables or .NET user secrets.
Never commit SMTP credentials. Configuration keys:

| Key | Purpose |
| --- | --- |
| `RecoveryEmail:AppUrl` | Trusted frontend root, e.g. `https://coiny.example` |
| `RecoveryEmail:Host` | SMTP hostname |
| `RecoveryEmail:Port` | SMTP port; default 587 |
| `RecoveryEmail:EnableSsl` | Require SMTP STARTTLS; default true |
| `RecoveryEmail:From` | Sender email address |
| `RecoveryEmail:Username` | Optional SMTP authentication username |
| `RecoveryEmail:Password` | SMTP authentication secret |

Environment variables use double underscores, e.g. `RecoveryEmail__Host`.
Recovery returns 503 for every email while configuration is absent or invalid.
Password changes and session controls do not depend on SMTP.

For local development, point `AppUrl` to `http://localhost:4200` and use a local
SMTP capture service. Unencrypted SMTP is allowed only in Development with a
loopback SMTP host. Outside Development, both the frontend reset link and SMTP
transport must be encrypted. SMTP port 587 with STARTTLS is supported; implicit
TLS on port 465 is not supported by this sender.

Links are generated from configured `AppUrl`, never the incoming request's Host.
Email and reset token use an encoded URL fragment, so they are not sent in HTTP
requests or Referer headers. The reset screen removes the fragment from the
current history entry after reading it. Tokens and credentials are never logged
or returned by recovery APIs.

The sender uses a bounded in-memory background queue, decoupling SMTP latency from
account-existence responses. Delivery failures log a message without recipient or
token details. Pending mail does not survive an API restart; users can request a
new link after the rate-limit window. This is not a durable email outbox.

## API

Public routes:

- `GET /api/auth/recovery-options`: `{ email, recoveryCode }` flags for the
  installation; no account-specific information.
- `POST /api/auth/recover-with-code`: `{ email, recoveryCode, newPassword }`; 200
  on success, generic 400 on rejection, or 429 when throttled.

- `POST /api/auth/forgot-password`: `{ email }`; generic 200 confirmation, 503 when
  mail isn't configured, or 429 when throttled.
- `POST /api/auth/reset-password`: `{ email, token, newPassword }`; 200 on success,
  400 for an invalid/expired link or password-policy failure.

Authenticated routes:

- `GET /api/auth/recovery-codes`: `{ remaining }`; never returns plaintext codes.
- `POST /api/auth/recovery-codes`: `{ currentPassword }`; replaces previous codes
  and returns `{ codes: string[] }` exactly once. Shares the authenticated password
  operation rate limit.
- `POST /api/auth/recovery-codes/revoke`: `{ currentPassword }`; delete the owned
  set, 204. Shares the authenticated password operation rate limit.

- `POST /api/auth/change-password`: `{ currentPassword, newPassword }`; 200 on
  success. Sign in again afterward.
- `GET /api/auth/sessions`: active rows with `id`, `device`, `createdAt`,
  `expiresAt`, and `isCurrent`.
- `DELETE /api/auth/sessions/{id}`: revoke an owned session; idempotent 204.
- `POST /api/auth/logout`: empty object; revoke the current session, 204.
- `POST /api/auth/logout-all`: `{ currentPassword }`; revoke all sessions and
  rotate the security stamp, 204.

Auth responses carry `Cache-Control: no-store`.

## Upgrade and deployment

For legacy development, back up SQLite, apply `20261028162624_AddAuthSessions`
using `--context CoinyContext`, and restart the API. The
migration creates one empty session table and index. Recovery codes additionally
require `20261028165724_AddAuthRecoveryCodes`, which adds an empty hash table and
index. Neither migration rewrites users, transactions, budgets, fund activities,
or balances. No codes are inferred or generated by migration. Existing browser tokens
require a fresh sign-in. Do not reset or seed a populated database.
Hybrid installations store these tables in PostgreSQL and use the separate control
migration chain; follow the initialization/migration runbook rather than applying
legacy SQLite migrations to a household file.

Hosted deployment must configure HTTPS, trusted reverse-proxy forwarding before
rate limiting (never blindly trust forwarded IP headers), SMTP, a private strong
JWT signing key, and persistent protected ASP.NET Data Protection keys. Reset
links use Data Protection; changing/loss of its keys invalidates outstanding links.
IP limits are process-local and need a shared gateway/limiter for multiple API
instances. Browser tokens still use localStorage, so script injection prevention
and a production CSP remain deployment concerns. MFA, email-address changes, and
email-verification workflows are outside this pass. This feature does not provide
end-to-end encryption or zero-knowledge storage.

Implementation uses ASP.NET Core Identity's password hashing, reset tokens,
security stamps, and lockout rather than custom password/token cryptography. See
[Microsoft's Identity guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0).
