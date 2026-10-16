# Coiny Web

Angular frontend for Coiny's household dashboard. See the
[root README](../README.md) for project status and the
[setup guide](../docs/06-development-setup.md) for API configuration and local URLs.

## Commands

Run from `Coiny.Web`:

```sh
npm ci
npm start
npm run build
npm test -- --watch=false
```

`npm start` serves `http://localhost:4200`; production output goes to
`dist/Coiny.Web`. Tests use Angular's unit-test builder with Vitest/jsdom.
There is no configured end-to-end runner.

For the known starter-test failure and focused dashboard/accounts test commands, see
[verification](../docs/06-development-setup.md#verification).

## Source layout

| Directory | Responsibility |
| --- | --- |
| `src/app/core/services/` | API calls and token handling |
| `src/app/core/guards/`, `interceptors/` | Route authentication and bearer header |
| `src/app/features/dashboard/` | Hero, range selection, account list, recent transactions |
| `src/app/features/auth/login/` | Branded login, animated coins, password toggle |
| `src/app/features/budget/` | Five-step household budget wizard, save/resume, saved previews, publication, and plan summary |
| `src/app/features/accounts/` | Account selection, scoped trends, transaction filters/pages, creation modals, income/expense breakdown |
| `src/app/features/transactions/create-transaction/` | Account-aware transaction form |
| `src/app/shared/components/` | Header/footer, icons, selectors, modal shell, SVG trend and donut charts |
| `src/app/shared/models/` | API response/request types |
| `src/styles/` | Colors, typography, layout, buttons, cards, forms |
| `src/environments/environment.ts` | API URL configuration |

The current environment export is spelled `environemnt`. The default API URL is
`http://localhost:5298/api`; changing it requires the corresponding API/CORS setup.

## Product behavior

The [dashboard guide](../docs/08-dashboard.md) defines chart ranges, financial
colors, avatar alternation, pane states, and the account-scoped income/expense
donut. The donut follows the account and timeframe independently of list
filters and pagination. The
[authentication guide](../docs/04-user-profile.md) distinguishes existing token
handling from planned logout/signup work. Routes and unfinished navigation are
listed in the dashboard guide; header icons alone do not indicate finished pages.
