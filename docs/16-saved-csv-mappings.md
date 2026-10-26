# Saved CSV mappings

## Use a saved layout

In transaction import, choose the destination account and a **Saved CSV mapping**
in the Account and file card, then **Load mapping**. Profiles are household-shared,
not personal. The picker shows profiles for the same account type and either the
same institution or no institution association. This keeps checking and credit-card
sign conventions separate. Loading is explicit rather than silently guessing a bank.

Load before selecting a file to reuse encoding, separator, and header-line options.
You can also load after upload: Coiny re-reads the file, matches its headers, and
restores the column settings. Existing row-review edits require a discard confirmation
before replacing the mapping. The same component works for each file in multi-account
imports and for single-file imports. Other files keep their own review state.

## Save, update, and delete

After choosing valid columns/date/number/amount options, expand **Save these column
settings for next time** in Match your columns. Enter a name and choose **Save as
new mapping**. With a profile selected, **Update selected mapping** explicitly
replaces that saved layout with the current settings. Profiles created from an
account inherit its institution and account type. Names are unique per household.
**Delete mapping** removes only the template after confirmation; current columns,
transaction rows, and imported history stay intact. **Reload mappings** recovers
from stale profile changes or transient loading errors. Failure to load profiles
does not block manually mapping a CSV.

Saved data includes header labels, column choices, date format (including two-digit
years), number format, amount layout/sign or debit-credit indicator choices,
posting-status exclusions, encoding, separator, header line, and bank row order.
Transaction values, filenames, destination/source account choices, and confirmation
checkboxes are not stored. A mapping profile is not a copy of the bank file.

## Header matching and confirmations

Uniquely named mapped columns are resolved by trimmed, whitespace-collapsed,
case-insensitive header names. Reordering them or adding unmapped columns is safe.
A renamed/missing mapped header is rejected instead of reusing a potentially wrong
index. Mapped duplicate names are accepted only when the full normalized ordered
header list is unchanged; otherwise the result is ambiguous and needs manual work.
Extra unmapped columns are not automatically added to the saved mapping.

Loading a profile resets account/date/money-direction confirmation, bank-balance
meaning confirmation, and the source-account selection for multi-account exports.
The destination Coiny account is never changed by a profile. Users must review the
settings and rows before the normal validation, duplicate/transfer checks, and final
import. Changing a bank's debit/credit indicator codes can still require manual
updates; loading a layout does not prove a new file's values follow the old meaning.

## Storage, API, and checks

Migration `20261025190224_AddSavedCsvMappings` adds one metadata table with a unique
household/name index, optional household-validated institution reference, account
type, validated settings JSON, and edit version. It does not alter existing financial
records. Stale updates/deletes are rejected. Only recognized settings are serialized;
the API does not retain arbitrary client payload fields. Hard-deleting an institution
clears the optional association; no financial rows are linked to profiles.

The API is `/api/csv-mappings`; see [API reference](07-api-reference.md#saved-csv-mappings).
Tests cover round-trip storage, household scope, name/version conflicts, foreign
institutions, invalid formats/columns, HTTP access, reordered/missing/duplicate
headers, confirmation resets, metadata-only saving, and shared multi-file usage.
