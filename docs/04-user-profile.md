# Authentication and User Profile

[Documentation index](../README.md#documentation)

## Implemented authentication

ASP.NET Core Identity stores users and password hashes. The API issues signed
JWTs after registration or login. JWT validation checks issuer, audience,
signature, lifetime, and the persisted session/security stamp; revocation is
checked server-side. Current configuration uses issuer `Coiny`, audience
`CoinyUsers`, and a 120-minute expiration; the signing key is configured locally.

| Endpoint | Authentication | Behavior |
| --- | --- | --- |
| `POST /api/auth/register` | Public | Creates a user and returns token/email |
| `POST /api/auth/login` | Public | Validates credentials and returns token/email |
| `GET /api/auth/me` | Bearer token | Returns the authenticated user's profile |

Registration requires `firstName` (maximum 100 characters), email, and password
(minimum eight characters, also subject to Identity's default complexity rules).
The first name is trimmed before saving. Registration does not create a household.

`GET /api/auth/me` returns `id`, `email`, nullable `firstName`, and
`includeMortgageInNetWorth`; it accepts
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
Forgot Password opens `/forgot-password`; emailed links open `/reset-password`. Sign Up opens `/signup`.
The signup form collects first name/email, validates and confirms the password,
then stores the returned token and opens `/household-setup`. Invitation codes
from signup links are retained through signup or the sign-in link.
Protected API 401 responses clear the matching browser session and redirect to
login. Other tabs refresh when the stored identity changes. There is no refresh-token
flow or timer-based page redirect without a request/navigation; sessions have a
fixed expiry. See [account security](20-account-security.md).

## Header profile menu and logout

The header avatar shows the user's first initial, or `?` when no first name is
available. Its right-aligned menu shows first name/email, Categories, Merchant rules,
Alerts, Settings, and Log out. Loading or
failed profile requests do not prevent local logout.

Clicking the avatar toggles the menu. Arrow Down opens it at Categories; Arrow Up
opens it at Log out. Arrow keys and Home/End move between menu items;
native button Enter/Space activation works as well. Clicking outside or moving
focus outside the profile controls closes it. Escape closes it and returns focus
to the avatar. The trigger exposes its expanded state and menu relationship.

Log out removes `coiny_token`, clears cached profile state, cancels any pending
profile response, closes the menu, and navigates to `/login` with history
replacement. A later protected-route visit is denied by the route guard.

Logout also requests server-side session revocation. If the server is unavailable,
local logout still completes and a warning explains that server revocation failed.
The **Settings** menu item navigates to `/settings`, where Household, Preferences,
and Security appear together as page sections. Security includes password change,
recovery codes, session management, and sign-out-everywhere. Password changes and resets invalidate all old sessions.
See [account security](20-account-security.md) for SMTP configuration and limits.

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

**Profile → Settings → Household** shows one card per member with first name,
email, role, and a You label for the caller, followed by the invitation card.
Only owners see Remove for other regular members. Removal requires confirmation,
preserves the user's login and all household financial data, and atomically
revokes any unconsumed invitation. Owners cannot remove themselves or another
owner. Removed users lose membership-based API access and are redirected to
household setup on their next protected navigation. Members can view the roster
but cannot remove people or manage invitations.

Owners generate, copy, or revoke codes in the lower card; members see instructions
to ask the owner. Navigation is blocked during writes. Existing `/household` links
redirect to `/settings`. Codes
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

## Account security and remaining refinements

Password changes/resets, offline recovery codes, revocable sessions, lockout, rate
limits, and protected-request expiry/revocation handling are implemented. Owners
and members manage their own security under **Settings → Security**. SMTP is
optional for code recovery but required for email reset links. See
[account security](20-account-security.md) for the complete flows and configuration.

Refresh tokens, MFA, email confirmation, and email/text invitation delivery remain
later work; copyable join codes work without email. Production proxy/key/SMTP
configuration and an operator account-reset command remain release preparation.
See [development setup](06-development-setup.md) and [demo data](05-demo-data.md).

## Personal dashboard preferences

The avatar menu includes Settings. Each user can choose whether mortgages are
included in their net-worth view; inclusion defaults on. This setting is stored
on the user and returned by `/auth/me` and `/users/me/preferences`. Household
members retain independent choices. Excluded views are labelled explicitly, and
mortgage balances stay visible separately. Saving updates the shared profile
state and refreshes any active dashboard/accounts aggregate view.

### Category management

The avatar menu’s **Categories** item opens a household category modal with Expenses and Income tabs. Users can add categories, rename them, archive them, and restore archived categories through the “Show archived categories” option. System categories are protected, and category type stays fixed. Archiving preserves transaction history. Existing Add Transaction buttons and category creation options remain available. Changes use the shared toast notifications.

### Compact navigation

At widths of 760px and below, the header keeps the Coiny brand and replaces the navigation icons/avatar with a hamburger button. Its panel includes Home, Accounts, Budget, user identity, Categories, Merchant rules, Alerts, Settings, and Log out. Links retain active-page highlighting. Escape closes the panel and restores focus to the toggle; outside clicks, focus leaving, and navigation close it as well. Settings navigates to the same page on both desktop and mobile. Other profile modals opened from the panel return focus to the toggle. Wider screens retain the existing desktop navigation.

## Consolidated Settings page

`/settings` is an authenticated, household-protected page inside the shared app
layout. Household, Preferences, and Security are visible together in separate
responsive cards. Preferences retain their own **Save settings** and **Reset
changes** controls; saving stays on the page and reloads the current household
setting version before further edits. Household and security actions retain their
existing confirmations and password checks. Navigation/reload protection covers
active writes, unsaved preferences, and newly generated recovery codes that have
not been acknowledged. Mandatory session logout still returns to sign-in.
