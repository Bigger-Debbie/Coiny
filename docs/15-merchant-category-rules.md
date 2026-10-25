# Merchant/category rules

## Manage household rules

Open **Avatar menu → Merchant rules** on desktop or in the mobile menu. Rules are
shared with the household. Add/edit a rule, choose Exact merchant or Description
contains, enter a phrase and an active Income/Expense category, and save. The list
also supports enable/disable and confirmed deletion. Rules never alter existing
transactions, budgets, transfer links, or payment status.

**Preview matches** checks up to 1,000 recent recorded transactions of the matching
money direction/category type and shows up to 10 examples. It does not inspect the
CSV or save anything. A rule can be saved without existing examples for future
imports. Use a specific phrase: broad words can match unrelated spending.

## Matching in CSV review

Matching happens locally after column mapping in the same review component for
single- and multi-file imports. Case and punctuation are normalized; numeric
references and full merchant identities are retained. Only active rules with active
categories matching Money out → Expense or Money in → Income are eligible.

1. Exact merchant matches take priority, using the mapped merchant field. They do
   not fall back to descriptions when the merchant is absent.
2. Otherwise, the longest normalized matching description phrase takes priority.
3. Equally specific rules selecting different categories leave the row flagged
   **Conflicting rules**. Selecting a category resolves it. Ties agreeing on the same
   category are deterministic by rule id.
4. If no rule matches, initial loading retains the existing matching bank-category
   fallback. Rule suggestions take priority over that fallback, not over user edits.

Known Transfer-classified rows or rows explicitly marked Transfer are not categorized
by rules. Transfer categories cannot be rule targets. Coiny cannot infer an unknown
transfer from every bank description; review ambiguous incoming/outgoing rows.
Excluded rows are skipped. Import still requires explicit final review and existing
server-side category, duplicate, date, and balance checks.

Rule-assigned categories display **Suggested by rule**; row details identify the
phrase. Users can override or clear the cell, or apply a bulk category. Those choices
are never overwritten by **Refresh category rules**. Refresh fills untouched empty
cells, including after a new rule is saved. Editing merchant/description reevaluates
rule-owned suggestions; changing direction resets incompatible categories. If rules
fail to load, a visible notice allows manual categorization and retry.

## Remember while importing

In row details, choose **Remember this merchant** after choosing a non-transfer
category. It opens a prefilled Exact merchant rule for review. When no merchant is
available, **Create description rule** opens a Description contains rule instead.
The full description is suggested up to the 200-character rule limit; shorten it
to a reusable phrase rather than retaining bank-specific references. Saving is
explicit and affects future imports plus untouched blank cells in the current file.
It does not recategorize already-selected rows or other reviewed files.

## Persistence and safeguards

Migration `20261025150814_AddMerchantCategoryRules` adds one household-scoped table.
It does not change existing data. A normalized phrase/match-type pair is unique
within a household, including disabled rules. Edit versions reject stale updates
and deletion. Archived categories are visible on existing rules but are never used
for suggestions; change the target before re-enabling such a rule. No regex engine,
background recategorization, or guessed transfer categorization is included.

Backend tests cover scope, target/phrase validation, duplicate and stale saves,
archival behavior, read-only previews, and HTTP access. Frontend tests cover ranking,
conflicts, active/direction/transfer exclusions, user overrides, merchant edits,
remembering pairs, previews, and shared single/multi-file imports.
