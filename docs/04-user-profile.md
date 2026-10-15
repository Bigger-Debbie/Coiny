# Authentication and User Profile

[Documentation index](../README.md#documentation)

## Implemented authentication

ASP.NET Core Identity stores users and password hashes. The API issues signed
JWTs after registration or login. JWT validation checks issuer, audience,
signature, and lifetime. Current configuration uses issuer `Coiny`, audience
`CoinyUsers`, and a 120-minute expiration; the signing key is configured locally.

| Endpoint | Authentication | Behavior |
| --- | --- | --- |
| `POST /api/auth/register` | Public | Creates a user and returns token/email |
| `POST /api/auth/login` | Public | Validates credentials and returns token/email |
| `GET /api/auth/me` | Bearer token | Returns the authenticated user's profile |

Registration requires `firstName` (maximum 100 characters), email, and password
(minimum eight characters, also subject to Identity's default complexity rules).
The first name is trimmed before saving. Registration does not create a household.

`GET /api/auth/me` returns only `id`, `email`, and nullable `firstName`; it accepts
no user ID from the caller. If the token's user no longer exists, it returns 401.
There is no profile-update endpoint.

The `AddUserFirstName` migration adds a nullable column to preserve older users.
Such users keep a null first name until it is populated. Names are not inferred
from email. See the [API reference](07-api-reference.md) for examples.

## Current frontend behavior

`AuthService` stores the token in local storage under `coiny_token`. The HTTP
interceptor attaches it as a bearer token. The route guard checks for a token and
its expiration before entering the protected application layout; invalid or
expired tokens are removed and navigation goes to `/login`.

A basic `/login` form is implemented. There is no global handling of 401 responses
or refresh-token flow. Do not assume an active page automatically redirects the
moment its token expires.

## Header profile menu and local logout

The header avatar shows the user's first initial, or `?` when no first name is
available. Its right-aligned menu shows first name/email and a Log out action.
No profile/settings links are shown until those destinations exist. Loading or
failed profile requests do not prevent local logout.

Clicking the avatar toggles the menu. Arrow Up/Down opens it and focuses Log out;
native button Enter/Space activation works as well. Clicking outside or moving
focus outside the profile controls closes it. Escape closes it and returns focus
to the avatar. The trigger exposes its expanded state and menu relationship.

Log out removes `coiny_token`, clears cached profile state, cancels any pending
profile response, closes the menu, and navigates to `/login` with history
replacement. A later protected-route visit is denied by the route guard.

This is client-side logout. There is no server logout/revocation endpoint;
an already-issued JWT remains valid until it expires. Global expired-session
handling and server-side revocation are separate work.

The login UI currently reports failures to the console rather than displaying a
complete user-facing error state. Registration is available through the API,
not a signup screen.

## Dashboard personalization

`AuthService` shares one in-flight/cached `/api/auth/me` request and a read-only
user signal between the header and dashboard. Setting a token for a new login
also invalidates the previous profile; a failed profile request can be retried by
a later caller. Household data loads separately through `/api/households/me`,
independently of financial data. Alex in a household named Ross sees:

```text
Welcome back, Alex
The Ross household at a glance.
```

Missing names or failed requests use the generic greeting. Stored household
names already beginning with “The” or ending in “household” avoid duplicate words;
other names are displayed as stored. The greeting does not rename a household.

## Next authentication work

Complete session-expiration handling, login presentation, signup,
and new-user household onboarding are planned. Password reset, email
confirmation, token revocation/refresh, and member invitations have no public
workflows yet. Login currently does not increment Identity lockout failures.

For local signing-key setup, see [development setup](06-development-setup.md).
For the seeded login, see [demo data](05-demo-data.md).
