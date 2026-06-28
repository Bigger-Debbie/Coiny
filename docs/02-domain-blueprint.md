# Domain Blueprint

The domain blueprint provides a high-level overview of the core business entities that make up Coiny.

This document is intended to guide implementation and provide a shared understanding of how the application is modeled. It is intentionally lightweight and will evolve as the project grows.

---

# Core Domain

## Household

**Purpose**

Represents a shared financial space for one or more users.

**Relationships**

* Has many Household Members
* Has many Institutions
* Has many Accounts
* Has many Categories
* Has many Budgets
* Has many Goals

---

## Household Member

**Purpose**

Represents a user's membership within a household.

**Relationships**

* Belongs to one Household
* References one Application User

---

## Institution

**Purpose**

Represents a financial institution such as a bank, brokerage, lender, or credit card provider.

**Relationships**

* Belongs to one Household
* Has many Accounts

---

## Account

**Purpose**

Represents a financial account where money is stored or debt is held.

Examples include:

* Checking
* Savings
* Credit Card
* Investment
* Loan
* Cash

**Relationships**

* Belongs to one Household
* Optionally belongs to one Institution
* Has many Transactions

**Notes**

Current balance will be calculated from the opening balance and transaction history.

---

## Transaction

**Purpose**

Represents a financial event that changes the balance of an account.

Transactions are the authoritative financial record within Coiny.

**Relationships**

* Belongs to one Account
* Optionally belongs to one Category
* Created by one User

---

## Category

**Purpose**

Groups transactions into meaningful spending or income classifications.

Examples include:

* Groceries
* Utilities
* Restaurants
* Salary

**Relationships**

* Belongs to one Household
* Has many Transactions
* May have a parent category

---

## Budget

**Purpose**

Defines planned spending for a category over a period of time.

---

## Goal

**Purpose**

Represents a financial objective such as saving for a purchase or paying down debt.

---

## Recurring Transaction

**Purpose**

Represents income or expenses that occur on a repeating schedule.

Examples include:

* Mortgage
* Rent
* Paycheck
* Netflix
* Utilities

---

# Design Notes

The following principles influence the design of the domain:

* Transactions are the source of truth.
* Derived values should be calculated whenever practical.
* Business rules belong in the backend.
* The simplest correct solution is preferred.
* The model should represent real-world financial concepts.

---

# Initial Development Order

The first implementation will focus on the following entities:

1. Household
2. Household Member
3. Institution
4. Account
5. Category
6. Transaction

Budgets, goals, and recurring transactions will be implemented after the core financial model is complete.
