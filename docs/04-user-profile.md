# Current User and Dashboard Greeting

`GET /api/auth/me` requires a bearer token and returns only the authenticated
user's `id`, `email`, and nullable `firstName`. A token whose user no longer exists
receives HTTP 401. The endpoint does not accept a user ID from the caller.

New registrations require `firstName` (maximum 100 characters), in addition to
email and password. Leading and trailing whitespace is removed before saving.
The `AddUserFirstName` migration adds a nullable column so existing users and
their financial data are preserved. Existing users retain a null first name;
this milestone does not add a profile-edit endpoint or infer names from email.

The dashboard loads the profile and `GET /api/households/me` independently of its
financial requests. A first name of `Alex` and household name of `Ross` produce
"Welcome back, Alex" and "The Ross household at a glance." Missing names or
failed requests fall back to the generic greeting. Stored household names that
already include "The" or end in "household" do not receive duplicate words.

Trend controls are ordered `1D`, `1W`, `1M`, `3M`, `6M`, `1Y`, `YTD`. `1M`
remains the default. `1D` starts yesterday; `1W` starts seven calendar days ago.
Both ranges include today, using the API's America/New_York calendar dates.
Unavailable percentage changes are omitted entirely; valid dollar changes and
zero-percent changes remain visible.
