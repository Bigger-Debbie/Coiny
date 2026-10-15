# API and Postman Reference

[Documentation index](../README.md#documentation)

## Conventions

The default local API base is `http://localhost:5298/api`. Request/response fields
use camelCase. Send JSON creation bodies with `Content-Type: application/json`.
Use named enum strings from the tables below. Dates in examples are illustrative;
choose appropriate historical dates for your test account.

All endpoints except registration and login require:

```http
Authorization: Bearer <token>
```

Financial services derive the current household from the authenticated user.
Create a household before calling financial endpoints for a newly registered user.
The current app supports one household per user through its creation workflow.

## Endpoint inventory

Paths below include `/api`; in Postman, append the part after `/api` to `{{baseUrl}}`.

| Method | Path | Successful response | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | 200 | Register and issue token |
| POST | `/api/auth/login` | 200 | Sign in and issue token |
| GET | `/api/auth/me` | 200 | Current user profile |
| GET | `/api/households/me` | 200 | Current household and role; 404 if none |
| POST | `/api/households` | 201 | Create household and owner membership |
| GET | `/api/institutions` | 200 | Household institutions |
| POST | `/api/institutions` | 201 | Create institution |
| GET | `/api/accounts` | 200 | Accounts, opening dates, and current balances |
| POST | `/api/accounts` | 201 | Create account and opening-day snapshot |
| GET | `/api/categories` | 200 | Household categories |
| POST | `/api/categories` | 201 | Create category |
| GET | `/api/transactions` | 200 | Household transactions, newest first |
| POST | `/api/transactions` | 201 | Create transaction and update snapshots |
| GET | `/api/dashboard` | 200 | Totals, accounts with monthly trends, ten recent transactions |
| GET | `/api/dashboard/net-worth-trend` | 200 | Daily net-worth series for the requested range |

List responses are JSON arrays. There are no public GET-by-ID, update, delete,
logout, refresh-token, individual-account trend, or snapshot-management endpoints.
List endpoints do not currently provide pagination.

In Development, `/openapi/v1.json` exposes the generated contract. It is outside
the `/api` base and is a JSON document, not a configured Swagger UI.

## Postman workflow

Create environment variables:

| Variable | Example/use |
| --- | --- |
| `baseUrl` | `http://localhost:5298/api` |
| `token` | JWT returned by login/registration |
| `institutionId` | ID returned by institution creation/listing |
| `accountId` | ID returned by account creation/listing |
| `categoryId` | ID returned by category creation/listing |

Use collection-level Bearer Token authorization with `{{token}}`. Set login and
registration requests to **No Auth**. An optional post-response script on login
or registration can save the token:

```js
const body = pm.response.json();
if (pm.response.code === 200 && body.token) {
  pm.environment.set('token', body.token);
}
```

For the [demo database](05-demo-data.md), log in, inspect `/auth/me` and
`/households/me`, then request `/dashboard` and `/dashboard/net-worth-trend`.
Do not register the seeded email again.

For a fresh user: register → create household → optionally create institution →
create accounts and categories → create transactions → inspect dashboard/trends.
Use returned IDs rather than assuming they match the examples.

## Authentication payloads

`POST /api/auth/register`:

```json
{
  "firstName": "Alex",
  "email": "alex@example.test",
  "password": "DemoOnly!2026"
}
```

`firstName` is required, trimmed, and limited to 100 characters. Email must be
valid. Password must be at least eight characters and satisfy Identity's
complexity requirements. Registration returns a token but no household.

`POST /api/auth/login`:

```json
{
  "email": "alex@example.test",
  "password": "DemoOnly!2026"
}
```

Both successful calls return:

```json
{
  "token": "<jwt>",
  "email": "alex@example.test"
}
```

`GET /api/auth/me` returns:

```json
{
  "id": "<identity-user-id>",
  "email": "alex@example.test",
  "firstName": "Alex"
}
```

`firstName` can be null for an older user. See
[authentication behavior](04-user-profile.md) for token storage and current limits.

## Household and institution payloads

`POST /api/households`:

```json
{
  "name": "Ross"
}
```

Name is required, maximum 100 characters. Creation establishes an `Owner`
membership and rejects a user who already belongs to a household.
`GET /api/households/me` returns `id`, `name`, and `role` (`Owner` or `Member`).

`POST /api/institutions`:

```json
{
  "name": "Demo Community Bank",
  "website": "https://example.com"
}
```

Name is required, maximum 100 characters; website is optional, maximum 250.
Institution responses contain `id`, `name`, `website`, and `isActive`.

## Account payload

`POST /api/accounts`:

```json
{
  "name": "Primary Checking",
  "accountType": "Checking",
  "institutionId": 1,
  "openingBalance": 1000.00,
  "openingBalanceDate": "2026-04-01"
}
```

Name is required, maximum 100 characters. `institutionId` is optional/null; if
supplied, it must belong to the household. Opening balance must be nonnegative;
opening date is required and represents the start of that calendar day.

Account types: `Checking`, `Savings`, `CreditCard`, `Investment`, `Retirement`,
`Loan`, `Cash`, `Crypto`, `Other`.

Create/list responses include `id`, `name`, `accountType`, `openingBalance`,
`openingBalanceDate`, `currentBalance`, `isActive`, `institutionId`, and
`institutionName`. Institution fields can be null. These endpoints do **not**
include monthly trends; use `/dashboard` for the account pane's data.

## Category payload

`POST /api/categories`:

```json
{
  "name": "Groceries",
  "categoryType": "Expense"
}
```

Name is required; the service rejects a duplicate name in the household
case-insensitively. Responses contain `id`, `name`, `categoryType`, `isSystem`,
and `isActive`.

Types are `Income`, `Expense`, and `Transfer`. Transfer categories can be created,
but using one to create a transaction is currently rejected.

## Transaction payload

`POST /api/transactions`:

```json
{
  "accountId": 1,
  "categoryId": 1,
  "amount": 92.35,
  "transactionDate": "2026-10-15",
  "description": "Weekly groceries",
  "merchant": "Demo Market",
  "notes": "Postman example",
  "isCleared": true
}
```

Use actual IDs from the selected household. Both account and category must belong
to it. The amount must be at least 0.01; do not send a negative expense. The
category determines the financial effect. Description is required, maximum 250
characters; merchant/notes are optional with maximum lengths 150/1000.

The date cannot precede the account's opening date. Backdated entries update
later stored snapshots atomically. Future entries are currently accepted;
see the [current-balance distinction](03-account-trends.md#dates-and-calculations).

Create/list responses contain `id`, `institutionName`, `accountName`,
`categoryName`, `categoryType`, `amount`, `transactionDate`, `description`,
`merchant`, and `isCleared`. Creation does not currently populate
`institutionName` in its response; listing resolves it. Notes are stored but are
not included in these response DTOs. Listing orders by date descending, then ID
descending.

## Dashboard summary

`GET /api/dashboard` returns three top-level fields:

| Field | Content |
| --- | --- |
| `netWorth` | `currentValue`, `retirement`, `liquidFunds`, `debt` |
| `accounts` | All household accounts in name order, with `monthlyTrend` |
| `recentTransactions` | Ten newest transactions, ordered by date then ID descending |

An illustrative account entry:

```json
{
  "id": 1,
  "name": "Primary Checking",
  "accountType": "Checking",
  "currentBalance": 1200.00,
  "institutionName": "Demo Community Bank",
  "monthlyTrend": {
    "startDate": "2026-09-15",
    "endDate": "2026-10-15",
    "absoluteChange": 200.00,
    "percentageChange": 20.0,
    "direction": "Up"
  }
}
```

The account trend period is fixed at one calendar month through today. Changes
can be null when no history exists; percentage can also be null for a zero
starting balance. Direction is `Up`, `Down`, or `Neutral`.

Recent transaction entries contain `id`, `accountName`, `institutionName`,
`categoryName`, `categoryType`, `amount`, `transactionDate`, `description`, and
`merchant`. The dashboard DTO does not include `isCleared` or notes.

## Net-worth trend

```http
GET {{baseUrl}}/dashboard/net-worth-trend?startDate=2026-10-14&endDate=2026-10-15
```

Both query dates are required. Missing/malformed/reversed dates or a future
start return 400. A future end is capped at today in America/New_York. Dates are
inclusive. An illustrative two-day result:

```json
{
  "points": [
    { "date": "2026-10-14", "value": 1000.00 },
    { "date": "2026-10-15", "value": 950.00 }
  ],
  "startingValue": 1000.00,
  "endingValue": 950.00,
  "absoluteChange": -50.00,
  "percentageChange": -5.0,
  "direction": "Down"
}
```

An empty household/range can return an empty `points` array with null values and
changes, and neutral direction. Full calculation and carry-forward rules are in
[account trends](03-account-trends.md).

## Errors and current boundaries

- Invalid credentials return 401. Protected endpoints require a valid bearer token.
- An authenticated `/auth/me` request whose user no longer exists returns 401.
- No current household returns 404 from `/households/me`.
- DTO validation and caught business-rule failures return 400. Payloads vary
  between validation details, Identity errors, and plain messages; there is no
  unified application error envelope yet.
- Some GET financial endpoints do not translate a missing-household exception
  into a friendly response. Complete new-user setup before calling them.
- Created responses currently point to collection actions, not implemented
  single-resource GET endpoints.

These are current contracts, not promises of update/delete, account management,
or complete authentication workflows. Planned capabilities are tracked in the
[root README](../README.md#current-status).
