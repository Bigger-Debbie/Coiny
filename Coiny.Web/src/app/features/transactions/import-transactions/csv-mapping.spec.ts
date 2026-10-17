import { CsvMapping, mapCsv, suggestColumns } from './csv-mapping';
import { parseCsv } from './csv-parser';

describe('CSV column mapping', () => {
  const sample = 'Account Number,Post Date,Check,Description,Debit,Credit,Status,Balance,Classification\n' +
    'TEST-S20,10/16/2026,,Funds Transfer,,400.00,Posted,,Transfer\n' +
    'TEST-S20,10/16/2026,,Coffee,.18,,Posted,,Food &amp; Drink\n' +
    'TEST-S20,10/16/2026,,Pending purchase,23.63,,Pending,,Shopping';
  const mapping: CsvMapping = {
    dateColumn: 1, descriptionColumn: 3, amount: { mode: 'split', debitColumn: 4, creditColumn: 5 },
    dateFormat: 'MM/DD/YYYY', statusColumn: 6, classificationColumn: 8, accountColumn: 0,
  };
  it('suggests sample headers but never guesses date format or signed amount meaning', () => {
    const suggested = suggestColumns(parseCsv(sample).headers);
    expect(suggested).toMatchObject({ dateColumn: 1, descriptionColumn: 3, statusColumn: 6, classificationColumn: 8, accountColumn: 0, amount: mapping.amount });
    expect(suggested.dateFormat).toBeUndefined();
    expect(suggestColumns(['Date', 'Description', 'Amount']).amount).toBeUndefined();
  });
  it('maps separate debit/credit columns and defaults pending rows to excluded', () => {
    const rows = mapCsv(parseCsv(sample), mapping, '2026-10-01');
    expect(rows[0]).toMatchObject({ date: '2026-10-16', amount: 400, direction: 'In', classification: 'Transfer', sourceAccount: 'TEST-S20', excluded: false, errors: [] });
    expect(rows[1]).toMatchObject({ amount: .18, direction: 'Out', classification: 'Food & Drink', errors: [] });
    expect(rows[2].excluded).toBe(true);
    expect(rows[2].exclusionReason).toContain('Pending');
  });
  it('flags dates before opening and long descriptions without truncating source data', () => {
    const csv = parseCsv('Date,Description,Debit,Credit\n10/01/2026,' + 'x'.repeat(251) + ',5,');
    const row = mapCsv(csv, { dateColumn: 0, descriptionColumn: 1, amount: { mode: 'split', debitColumn: 2, creditColumn: 3 }, dateFormat: 'MM/DD/YYYY' }, '2026-10-02')[0];
    expect(row.errors).toHaveLength(2);
    expect(row.description).toHaveLength(251);
    expect(row.excluded).toBe(false);
  });
  it.each([
    ['03/04/2026', 'MM/DD/YYYY', '2026-03-04'],
    ['03/04/2026', 'DD/MM/YYYY', '2026-04-03'],
    ['2024-02-29', 'YYYY-MM-DD', '2024-02-29'],
    ['02/29/2026', 'MM/DD/YYYY', null],
    ['2026-13-01', 'YYYY-MM-DD', null],
    ['04/31/2026', 'MM/DD/YYYY', null],
  ] as const)('validates %s with explicit %s', (date, dateFormat, expected) => {
    const row = mapCsv(parseCsv(`Date,Description,Amount\n${date},Test,5`), {
      dateColumn: 0, descriptionColumn: 1, amount: { mode: 'signed', column: 2, positiveMeans: 'In' }, dateFormat,
    }, '0001-01-01')[0];
    expect(row.date).toBe(expected);
    expect(row.errors.length).toBe(expected ? 0 : 1);
  });
  it.each([
    ['"$1,234.50"', 'In', 1234.5, 'In'],
    ['(25.10)', 'In', 25.1, 'Out'],
    ['+20', 'Out', 20, 'Out'],
    ['-20', 'Out', 20, 'In'],
    ['.18', 'In', .18, 'In'],
  ] as const)('maps signed currency %s when positive means %s', (amount, positiveMeans, expected, direction) => {
    const row = mapCsv(parseCsv(`Date,Description,Amount\n2026-10-16,Test,${amount}`), {
      dateColumn: 0, descriptionColumn: 1, amount: { mode: 'signed', column: 2, positiveMeans }, dateFormat: 'YYYY-MM-DD',
    }, '2026-10-01')[0];
    expect(row).toMatchObject({ amount: expected, direction, errors: [] });
  });
  it.each(['0', '1.234', 'NaN', 'Infinity', '1e3', '"1,23"', '"1.234,50"'])('rejects ambiguous or invalid money %s', amount => {
    const row = mapCsv(parseCsv(`Date,Description,Amount\n2026-10-16,Test,${amount}`), {
      dateColumn: 0, descriptionColumn: 1, amount: { mode: 'signed', column: 2, positiveMeans: 'In' }, dateFormat: 'YYYY-MM-DD',
    }, '2026-10-01')[0];
    expect(row.amount).toBeNull(); expect(row.errors).toHaveLength(1);
  });
  it('rejects two populated split amounts and invalid mappings', () => {
    const csv = parseCsv('Date,Description,Debit,Credit\n2026-10-16,Test,20,20');
    const split: CsvMapping = { dateColumn: 0, descriptionColumn: 1, dateFormat: 'YYYY-MM-DD', amount: { mode: 'split', debitColumn: 2, creditColumn: 3 } };
    expect(mapCsv(csv, split, '2026-10-01')[0].errors).toHaveLength(1);
    expect(() => mapCsv(csv, { ...split, descriptionColumn: 0 }, '2026-10-01')).toThrow('different column');
    expect(() => mapCsv(csv, { ...split, dateColumn: 99 }, '2026-10-01')).toThrow('valid columns');
  });
  it('accepts manually mapped arbitrary headers, European dates, and decimal commas', () => {
    const csv = parseCsv('Buchungstag;Verwendungszweck;Betrag;Zustand\n17.10.2026;Coffee;-4,50;Offen\n16.10.2026;Salary;2.100,00;Gebucht');
    expect(suggestColumns(csv.headers).dateColumn).toBeUndefined();
    const rows = mapCsv(csv, { dateColumn: 0, descriptionColumn: 1, dateFormat: 'DD.MM.YYYY',
      amount: { mode: 'signed', column: 2, positiveMeans: 'In' }, decimalSeparator: ',', statusColumn: 3, excludedStatuses: ['Offen'] }, '2026-10-01');
    expect(rows[0]).toMatchObject({ date: '2026-10-17', amount: 4.5, direction: 'Out', excluded: true });
    expect(rows[1]).toMatchObject({ amount: 2100, direction: 'In', excluded: false, errors: [] });
  });
  it('supports unsigned amount plus custom indicator values and compact dates', () => {
    const csv = parseCsv('When,Details,Value,Code\n20261017,Coffee,4.50,DR\n20261016,Salary,2100,CR\n20261016,Other,25,X');
    const rows = mapCsv(csv, { dateColumn: 0, descriptionColumn: 1, dateFormat: 'YYYYMMDD',
      amount: { mode: 'indicator', column: 2, directionColumn: 3, directions: { DR: 'Out', CR: 'In' } } }, '2026-10-01');
    expect(rows[0]).toMatchObject({ date: '2026-10-17', amount: 4.5, direction: 'Out', errors: [] });
    expect(rows[1].direction).toBe('In');
    expect(rows[2].errors[0]).toContain('indicator');
  });
  it('uses explicit split-column roles even when a bank exports negative debits', () => {
    const rows = mapCsv(parseCsv('Date,Text,Out,In\n17-10-2026,Coffee,-4.50,0'), {
      dateColumn: 0, descriptionColumn: 1, dateFormat: 'DD-MM-YYYY',
      amount: { mode: 'split', debitColumn: 2, creditColumn: 3 },
    }, '2026-10-01');
    expect(rows[0]).toMatchObject({ amount: 4.5, direction: 'Out', errors: [] });
  });
  it.each([
    ['10/16/26', 'MM/DD/YY', '2026-10-16'],
    ['16/10/26', 'DD/MM/YY', '2026-10-16'],
    ['10-16-26', 'MM-DD-YY', '2026-10-16'],
    ['16-10-26', 'DD-MM-YY', '2026-10-16'],
    ['16.10.26', 'DD.MM.YY', '2026-10-16'],
    ['01/01/69', 'MM/DD/YY', '2069-01-01'],
    ['01/01/70', 'MM/DD/YY', '1970-01-01'],
    ['02/29/00', 'MM/DD/YY', '2000-02-29'],
    ['02/29/99', 'MM/DD/YY', null],
    ['10/16/2026', 'MM/DD/YY', null],
  ] as const)('parses two-digit year %s as %s with an explicit century rule', (date, dateFormat, expected) => {
    const row = mapCsv(parseCsv(`Date,Description,Amount\n${date},Test,5`), {
      dateColumn: 0, descriptionColumn: 1, dateFormat, amount: { mode: 'signed', column: 2, positiveMeans: 'In' },
    }, '1900-01-01')[0];
    expect(row.date).toBe(expected);
  });
});
