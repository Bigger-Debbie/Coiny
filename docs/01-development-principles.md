# Development Principles

**Project:** Coiny

---

# Purpose

This document defines the engineering principles that guide the design and development of Coiny.

These principles exist to encourage thoughtful architecture, maintainable code, and consistent decision-making throughout the lifetime of the project.

Whenever multiple implementation options exist, these principles should help determine the best path forward.

---

# Principle 1

## Design Before Development

Features should be understood and designed before implementation begins.

This includes defining:

* Business requirements
* Domain model
* Database relationships
* API contracts
* User experience

Writing code is the final step—not the first.

---

# Principle 2

## Business Rules Drive the Software

Coiny models real financial concepts.

The software should reflect how money actually moves through accounts, households, and budgets rather than simply exposing database tables through an API.

The domain model should always represent the business problem being solved.

---

# Principle 3

## Transactions Are the Source of Truth

Financial history should be immutable whenever practical.

Transactions represent the authoritative financial record.

Balances, spending totals, budget utilization, and similar values should be calculated from transaction history rather than manually maintained.

---

# Principle 4

## Do Not Store Derived Data

If a value can be reliably calculated, it should generally not be stored.

Examples include:

* Account balances
* Net worth
* Budget remaining
* Goal progress
* Monthly spending totals

Derived values may be cached later for performance, but calculations remain the authoritative source.

---

# Principle 5

## Readability Over Cleverness

Code should prioritize clarity over brevity.

Future maintainers—including your future self—should be able to understand the intent of the code without unnecessary complexity.

Readable software is maintainable software.

---

# Principle 6

## Small, Incremental Progress

Coiny will be developed through small, well-defined milestones.

Each completed milestone should leave the application in a functional and stable state.

Large features should be broken into manageable pieces whenever possible.

---

# Principle 7

## Documentation Is Part of the Product

Architecture, design decisions, and business rules should be documented alongside the code.

Documentation should explain not only what the software does, but why it was designed that way.

---

# Principle 8

## The Backend Owns the Rules

Business validation belongs within the API.

The frontend exists to provide an excellent user experience, but the backend remains responsible for enforcing business rules and protecting data integrity.

---

# Principle 9

## Build for Tomorrow, Not Just Today

Features should be designed with reasonable future growth in mind.

Coiny should remain easy to extend as additional capabilities are introduced, while avoiding unnecessary complexity or speculative features.

---

# Principle 10

## Favor Explicitness

Relationships, ownership, and business behavior should be explicit.

Avoid hidden assumptions, magic strings, and implicit behavior whenever possible.

Well-defined models lead to predictable software.

---

# Decision Framework

When evaluating implementation choices, ask the following questions:

1. Does this accurately model the real-world financial concept?
2. Does it improve long-term maintainability?
3. Is the design simple to understand?
4. Does it align with the domain model?
5. Can another developer easily understand the reasoning?
6. Would this decision still make sense one year from now?

If the answer to these questions is "yes," the solution is likely aligned with the goals of the project.
