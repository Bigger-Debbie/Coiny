"""Select one household from a consistent in-memory SQLite backup; never mutate source."""
import argparse
import os
from pathlib import Path
import sqlite3
import tempfile

DIRECT = set('Accounts AccountReconciliations BillPayments BudgetCategoryRules Categories HouseholdAlerts HouseholdBudgets HouseholdRecurringSettings Institutions MerchantCategoryRules RecurringExpenseReviews SavedCsvMappings SavingsFund TransactionBudgetAssignments TransactionImportBatches TransactionImportSessions TransactionImportUndo Transactions'.split())
INDIRECT = {
    'AccountBalanceSnapshots': 'AccountId IN (SELECT Id FROM main.Accounts WHERE HouseholdId=:household)',
    'BillPaymentTransaction': 'BillPaymentId IN (SELECT Id FROM main.BillPayments WHERE HouseholdId=:household)',
    'BudgetRevisions': 'HouseholdBudgetId IN (SELECT Id FROM main.HouseholdBudgets WHERE HouseholdId=:household)',
    'BudgetAllocations': 'BudgetRevisionId IN (SELECT Id FROM main.BudgetRevisions WHERE HouseholdBudgetId IN (SELECT Id FROM main.HouseholdBudgets WHERE HouseholdId=:household))',
    'BudgetIncomeSources': 'BudgetRevisionId IN (SELECT Id FROM main.BudgetRevisions WHERE HouseholdBudgetId IN (SELECT Id FROM main.HouseholdBudgets WHERE HouseholdId=:household))',
    'SavingsActivity': 'SavingsFundId IN (SELECT Id FROM main.SavingsFund WHERE HouseholdId=:household)',
    'HouseholdAlertReads': 'AlertId IN (SELECT Id FROM main.HouseholdAlerts WHERE HouseholdId=:household)',
}
MEMBERS = 'SELECT UserId FROM main.HouseholdMembers WHERE HouseholdId=:household'
ROLES = 'SELECT RoleId FROM main.AspNetUserRoles WHERE UserId IN (' + MEMBERS + ')'
CONTROL = {
    'AspNetUsers': 'Id IN (' + MEMBERS + ')', 'Households': 'Id=:household',
    'HouseholdMembers': 'HouseholdId=:household', 'HouseholdInvitations': 'HouseholdId=:household',
    **{t: 'UserId IN (' + MEMBERS + ')' for t in ['AspNetUserClaims', 'AspNetUserLogins', 'AspNetUserRoles', 'AspNetUserTokens', 'AuthSessions', 'AuthRecoveryCodes']},
    'AspNetRoles': 'Id IN (' + ROLES + ')', 'AspNetRoleClaims': 'RoleId IN (' + ROLES + ')',
    '__EFMigrationsHistory': '1=1', '__EFMigrationsLock': '1=0',
}


def quote(name):
    return '"' + name.replace('"', '""') + '"'


def snapshot(source, email, output):
    source, output = Path(source), Path(output)
    if not source.is_absolute() or not output.is_absolute() or not source.is_file() or output.exists():
        raise ValueError('Require existing absolute source and new absolute destination.')
    # Backup API reads the source in a consistent snapshot, including WAL. All
    # unselected rows stay in RAM; no full multi-household copy is written to disk.
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as original, sqlite3.connect(':memory:') as memory:
        original.backup(memory)
        users = memory.execute('SELECT Id FROM AspNetUsers WHERE lower(Email)=lower(?)', (email,)).fetchall()
        if len(users) != 1: raise ValueError('Expected exactly one selected user.')
        households = memory.execute('SELECT HouseholdId FROM HouseholdMembers WHERE UserId=?', users[0]).fetchall()
        if len(households) != 1: raise ValueError('Expected exactly one selected household.')
        household = households[0][0]
        tables = memory.execute("SELECT name,sql FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").fetchall()
        allowed = DIRECT | INDIRECT.keys() | CONTROL.keys()
        if set(t[0] for t in tables) != allowed:
            raise ValueError('Schema inventory differs from the supported legacy model.')
        if memory.execute("SELECT COUNT(*) FROM sqlite_master WHERE type IN ('trigger','view')").fetchone()[0]:
            raise ValueError('Unexpected legacy views or triggers.')
        with tempfile.TemporaryDirectory(prefix='.household-select-', dir=output.parent) as temporary:
            staged = Path(temporary) / 'selected.sqlite'
            descriptor = os.open(staged, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600); os.close(descriptor)
            memory.execute('ATTACH DATABASE ? AS selected', (str(staged),))
            memory.execute('BEGIN')
            for name, sql in tables:
                if not sql.startswith('CREATE TABLE '): raise ValueError('Unexpected table definition.')
                memory.execute(sql.replace('CREATE TABLE ', 'CREATE TABLE selected.', 1))
            counts = {}
            for name, _ in tables:
                condition = 'HouseholdId=:household' if name in DIRECT else INDIRECT.get(name, CONTROL.get(name))
                memory.execute(f'INSERT INTO selected.{quote(name)} SELECT * FROM main.{quote(name)} WHERE {condition}', {'household': household})
                counts[name] = memory.execute(f'SELECT COUNT(*) FROM selected.{quote(name)}').fetchone()[0]
            # Keep deleted-ID high-water marks, without copying any excluded rows.
            for name, seq in memory.execute('SELECT name,seq FROM main.sqlite_sequence').fetchall():
                memory.execute('DELETE FROM selected.sqlite_sequence WHERE name=?', (name,))
                memory.execute('INSERT INTO selected.sqlite_sequence(name,seq) VALUES (?,?)', (name, seq))
            for (sql,) in memory.execute("SELECT sql FROM main.sqlite_master WHERE type='index' AND sql IS NOT NULL").fetchall():
                memory.execute(sql.replace('INDEX ', 'INDEX selected.', 1))
            if memory.execute('PRAGMA selected.foreign_key_check').fetchall():
                raise ValueError('Selection has references outside retained household members/data.')
            if memory.execute('PRAGMA selected.integrity_check').fetchall() != [('ok',)]:
                raise ValueError('Selection failed integrity checks.')
            memory.commit(); memory.execute('DETACH DATABASE selected')
            with open(staged, 'rb') as stream: os.fsync(stream.fileno())
            os.link(staged, output) # Atomic, no overwrite.
            return household, counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True); parser.add_argument('--email', required=True); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        household, counts = snapshot(args.source, args.email, args.output)
        print(f'Selected one household with {counts["AspNetUsers"]} member users and {counts["Accounts"]} accounts. Other household rows excluded.')
    except (OSError, ValueError, sqlite3.Error):
        parser.exit(1, 'Household snapshot failed; source was read-only and no selection was activated.\n')


if __name__ == '__main__':
    main()
