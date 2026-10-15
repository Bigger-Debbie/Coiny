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

A basic `/login` form is implemented. Logout methods exist in the service/layout,
but the header avatar has no wired logout action. There is no global handling of
401 responses or refresh-token flow. Do not assume an active page automatically
redirects the moment its token expires.

The login UI currently reports failures to the console rather than displaying a
complete user-facing error state. Registration is available through the API,
not a signup screen.

## Dashboard personalization

The dashboard loads `/api/auth/me` and `/api/households/me` independently of its
financial data. Alex in a household named Ross sees:

```text
Welcome back, Alex
The Ross household at a glance.
```

Missing names or failed requests use the generic greeting. Stored household
names already beginning with “The” or ending in “household” avoid duplicate words;
other names are displayed as stored. The greeting does not rename a household.

The header's avatar is still the static letter `A`; dashboard personalization
does not imply that the header profile menu is implemented.

## Next authentication work

Visible logout, complete session-expiration handling, login presentation, signup,
and new-user household onboarding are planned. Password reset, email
confirmation, token revocation/refresh, and member invitations have no public
workflows yet. Login currently does not increment Identity lockout failures.

For local signing-key setup, see [development setup](06-development-setup.md).
For the seeded login, see [demo data](05-demo-data.md).
