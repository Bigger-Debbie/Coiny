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

The branded `/login` form includes a password visibility toggle, validation,
loading feedback, and user-facing sign-in errors. Background coins drift behind
the form, with a pause control and a static reduced-motion alternative.
Forgot Password displays a coming-soon message. Sign Up opens `/signup`.
The signup form collects first name/email, validates and confirms the password,
then stores the returned token and opens `/household-setup`. Invitation codes
from signup links are retained through signup or the sign-in link.
There is no global handling of 401 responses
or refresh-token flow. Do not assume an active page automatically redirects the
moment its token expires.

## Header profile menu and local logout

The header avatar shows the user's first initial, or `?` when no first name is
available. Its right-aligned menu shows first name/email, a Household action, and
a Log out action. Loading or
failed profile requests do not prevent local logout.

Clicking the avatar toggles the menu. Arrow Down opens it at Household; Arrow Up
opens it at Log out. Arrow keys and Home/End move between menu items;
native button Enter/Space activation works as well. Clicking outside or moving
focus outside the profile controls closes it. Escape closes it and returns focus
to the avatar. The trigger exposes its expanded state and menu relationship.

Log out removes `coiny_token`, clears cached profile state, cancels any pending
profile response, closes the menu, and navigates to `/login` with history
replacement. A later protected-route visit is denied by the route guard.

This is client-side logout. There is no server logout/revocation endpoint;
an already-issued JWT remains valid until it expires. Global expired-session
handling and server-side revocation are separate work.

## Household creation and joining

New users choose **Create a household** or **Join a household** at
`/household-setup`. Authenticated financial routes check current membership and
redirect household-less users here. A failed status request shows a retry state;
it is not treated as permission to create a second household.

Creating a household trims/validates its name, makes the creator Owner, and opens
the first-account form. Joining previews the household name and explains shared
financial access before **Confirm and join** redeems the invitation. Previewing
never creates membership. Expired/revoked/used codes return to code entry. Joined
users become Members and open the household's existing dashboard, without a new
household budget wizard. Already joined users cannot create/join another household.
A unique UserId membership index and transactional create/redeem operations enforce
this under concurrent requests. Self-service leaving, switching, and member role changes remain future work.

**Profile → Household** opens an in-place modal, preserving the current page and
any unsaved budget work. The modal lists one card per member with first name,
email, role, and a You label for the caller, followed by the invitation card.
Only owners see Remove for other regular members. Removal requires confirmation,
preserves the user's login and all household financial data, and atomically
revokes any unconsumed invitation. Owners cannot remove themselves or another
owner. Removed users lose membership-based API access and are redirected to
household setup on their next protected navigation. Members can view the roster
but cannot remove people or manage invitations.

Owners generate, copy, or revoke codes in the lower card; members see instructions
to ask the owner. Escape/backdrop/close dismiss the modal and restore avatar focus;
closing is blocked during writes. Existing `/household` links open the same modal
as a route and return to the dashboard on dismissal. Codes
use eight cryptographically random characters, displayed `K7MP-4X9Q`, excluding
I/O/0/1. They expire after 10 minutes and admit one member. New generation replaces
the previous code; only one invitation record/code can be active per household.

Only a SHA-256 code hash is stored. Plain codes are returned only on generation,
kept in component memory, and never recovered from the status endpoint. Reloading
an owner page shows active status; generate another code if its plaintext was
lost. Copy code or **Copy invitation link** shares `/signup?code=...`; the recipient
can sign up or sign in first. Codes are submitted in POST bodies, not API URLs.
No email/SMS delivery service is used.

Preview/redemption share a 5-request/minute per-user limit; generation permits
5/minute per user. Invitation routes also share a 60-request/minute per-IP cap.
Excess attempts return 429 with Retry-After. These middleware limits are process
local; a deployment with multiple API instances would need shared limits. Expiry,
revocation, hash matching, and one-time consumption are checked server-side;
consumption and membership insertion commit or roll back together.

`AddHouseholdJoinInvitations` adds the invitation table and unique membership
index. It preserves existing household/financial data and requires any existing
multiple-household memberships to be resolved before applying it. It does not
reset demo budgets. See [API routes](07-api-reference.md#household-invitations).

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

Complete session-expiration handling, password reset, email confirmation, and
token revocation/refresh remain future work. Email/text invitation delivery is
not implemented; copyable join codes work without it. Login currently does not increment Identity lockout failures.

For local signing-key setup, see [development setup](06-development-setup.md).
For the seeded login, see [demo data](05-demo-data.md).


## Personal dashboard preferences

The avatar menu includes Settings. Each user can choose whether mortgages are
included in their net-worth view; inclusion defaults on. This setting is stored
on the user and returned by `/auth/me` and `/users/me/preferences`. Household
members retain independent choices. Excluded views are labelled explicitly, and
mortgage balances stay visible separately. Saving updates the shared profile
state and refreshes any active dashboard/accounts aggregate view.
