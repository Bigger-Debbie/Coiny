# Project Vision

[Documentation index](../README.md#documentation)

## Purpose

Coiny helps individuals, couples, and families understand their finances and plan
for the future. Its long-term purpose extends beyond expense tracking: bring
balances, activity, budgets, and financial goals into one clear household view.

The household is the shared financial space. The current implementation provides
shared accounts, budgets, savings funds, and financial history. Owner-generated
join codes and member removal are implemented; leaving/switching and role changes
remain later refinements.

## Questions the product should answer

- How much money do we have, and how much do we owe?
- How has our financial position changed?
- Where is our money going?
- Are we staying within our budget?
- Can we afford a purchase, and what should we save toward it?
- Are we making progress toward our goals?

Accounts and dashboards answer the balance/history questions. Published budgets,
actuals/forecasts, available-cash outlooks, and savings component goals now support
planning and affordability. These are estimates based on recorded data and the
household plan, not predictions of spending habits or guaranteed future income.

## Product pillars

| Pillar | Product direction |
| --- | --- |
| Understand | Balances, debt, net worth, and financial history |
| Organize | Accounts, institutions, categories, and household activity |
| Plan | Budgets, savings goals, debt payoff, and future purchases |
| Analyze | Spending trends, cash flow, comparisons, and forecasting |
| Improve | Make useful financial decisions through clarity and feedback |

## How Coiny should feel

The interface should be approachable, readable, and useful at a glance. Show the
financial picture first, with details available through interaction. Color should
support meaning, with labels, signs, and icons providing additional context.

Financial accuracy matters more than visual polish: the opening balance and
transaction ledger are authoritative. Calculated summaries and snapshots must be
explainable and consistent with that history.

## Delivery approach

Build small, functional milestones. Agree on behavior before implementation,
keep documentation alongside the code, and avoid speculative infrastructure.
The [development principles](01-development-principles.md) describe this approach.

The [root README](../README.md#current-status) tracks implemented versus planned
features. This document describes the enduring vision rather than claiming every
product pillar is already available.
