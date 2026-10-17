import { CsvDocument, CsvError } from './csv-parser';

export type CsvDateFormat = 'MM/DD/YYYY' | 'DD/MM/YYYY' | 'YYYY-MM-DD' | 'MM-DD-YYYY' | 'DD-MM-YYYY' | 'DD.MM.YYYY' | 'YYYYMMDD' | 'MM/DD/YY' | 'DD/MM/YY' | 'MM-DD-YY' | 'DD-MM-YY' | 'DD.MM.YY';
export interface CsvMapping {
  dateColumn: number;
  descriptionColumn: number;
  amount: { mode: 'split'; debitColumn: number; creditColumn: number } |
    { mode: 'signed'; column: number; positiveMeans: 'In' | 'Out' } |
    { mode: 'indicator'; column: number; directionColumn: number; directions: Record<string, 'In' | 'Out'> };
  dateFormat: CsvDateFormat;
  statusColumn?: number;
  classificationColumn?: number;
  merchantColumn?: number;
  accountColumn?: number;
  decimalSeparator?: '.' | ',';
  excludedStatuses?: string[];
}
export interface ImportReviewRow {
  line: number;
  date: string | null;
  description: string;
  amount: number | null;
  direction: 'In' | 'Out' | null;
  classification: string;
  merchant: string | null;
  sourceAccount: string | null;
  status: string;
  excluded: boolean;
  exclusionReason: string | null;
  errors: string[];
}

/** Suggestions only; users must confirm mappings, especially date and sign conventions. */
export function suggestColumns(headers: string[]): Partial<CsvMapping> {
  const normalized = headers.map(value => value.toLowerCase().replace(/[^a-z0-9]/g, ''));
  const find = (...aliases: string[]) => {
    const index = normalized.findIndex(value => aliases.includes(value));
    return index < 0 ? undefined : index;
  };
  const debit = find('debit', 'withdrawal', 'withdrawals');
  const credit = find('credit', 'deposit', 'deposits');
  return {
    dateColumn: find('postdate', 'posteddate', 'transactiondate', 'date'),
    descriptionColumn: find('description', 'transactiondescription', 'memo'),
    statusColumn: find('status'), classificationColumn: find('classification', 'category'),
    merchantColumn: find('merchant', 'payee'), accountColumn: find('accountnumber', 'account'),
    ...(debit !== undefined && credit !== undefined ? { amount: { mode: 'split' as const, debitColumn: debit, creditColumn: credit } } : {}),
  };
}

export function mapCsv(document: CsvDocument, mapping: CsvMapping, openingDate: string): ImportReviewRow[] {
  const required = [mapping.dateColumn, mapping.descriptionColumn,
    ...(mapping.amount?.mode === 'split' ? [mapping.amount.debitColumn, mapping.amount.creditColumn] :
      mapping.amount?.mode === 'indicator' ? [mapping.amount.column, mapping.amount.directionColumn] : [mapping.amount?.column])];
  const optional = [mapping.statusColumn, mapping.classificationColumn, mapping.merchantColumn, mapping.accountColumn].filter(v => v !== undefined);
  if ([...required, ...optional].some(index => !Number.isInteger(index) || index! < 0 || index! >= document.headers.length))
    throw new CsvError('Choose valid columns for the import.');
  if (new Set([...required, ...optional]).size !== required.length + optional.length)
    throw new CsvError('Use a different column for each mapped field.');
  if (!['MM/DD/YYYY', 'DD/MM/YYYY', 'YYYY-MM-DD', 'MM-DD-YYYY', 'DD-MM-YYYY', 'DD.MM.YYYY', 'YYYYMMDD', 'MM/DD/YY', 'DD/MM/YY', 'MM-DD-YY', 'DD-MM-YY', 'DD.MM.YY'].includes(mapping.dateFormat))
    throw new CsvError('Choose the date format used by your bank.');
  if (mapping.amount.mode === 'signed' && !['In', 'Out'].includes(mapping.amount.positiveMeans))
    throw new CsvError('Confirm whether positive amounts mean money in or money out.');
  if (mapping.decimalSeparator !== undefined && !['.', ','].includes(mapping.decimalSeparator))
    throw new CsvError('Choose the decimal separator used in the file.');
  return document.rows.map(row => {
    const get = (index?: number) => index === undefined ? '' : row.values[index].trim();
    const errors: string[] = [];
    const date = parseDate(get(mapping.dateColumn), mapping.dateFormat);
    if (!date) errors.push('Choose a valid transaction date.');
    else if (date < openingDate) errors.push('Date is before this account’s opening balance date.');
    const description = get(mapping.descriptionColumn);
    if (!description) errors.push('Enter a description.');
    else if (description.length > 250) errors.push('Shorten the description to 250 characters or fewer.');
    let amount: number | null = null, direction: 'In' | 'Out' | null = null;
    if (mapping.amount.mode === 'split') {
      const debit = money(get(mapping.amount.debitColumn), mapping.decimalSeparator), credit = money(get(mapping.amount.creditColumn), mapping.decimalSeparator);
      if (debit === undefined || credit === undefined || ((debit ?? 0) !== 0) === ((credit ?? 0) !== 0))
        errors.push('Use one nonzero money-out or money-in amount, not both.');
      else { amount = Math.abs((debit ?? 0) !== 0 ? debit! : credit!); direction = (debit ?? 0) !== 0 ? 'Out' : 'In'; }
    } else {
      const value = money(get(mapping.amount.column), mapping.decimalSeparator);
      if (value === null || value === undefined || value === 0) errors.push('Enter a nonzero amount with up to two decimal places.');
      else if (mapping.amount.mode === 'indicator') {
        const selected = mapping.amount.directions[get(mapping.amount.directionColumn)];
        if (selected !== 'In' && selected !== 'Out') errors.push('Choose money in or money out for this indicator value.');
        else { amount = Math.abs(value); direction = selected; }
      } else { amount = Math.abs(value); direction = value > 0 ? mapping.amount.positiveMeans : mapping.amount.positiveMeans === 'In' ? 'Out' : 'In'; }
    }
    const status = get(mapping.statusColumn);
    const excluded = mapping.excludedStatuses === undefined ? /\bpending\b/i.test(status) :
      mapping.excludedStatuses.some(value => value.trim().toLowerCase() === status.toLowerCase());
    const merchant = get(mapping.merchantColumn) || null;
    if (merchant && merchant.length > 150) errors.push('Shorten the merchant to 150 characters or fewer.');
    return { line: row.line, date, description, amount, direction, merchant,
      classification: decodeEntities(get(mapping.classificationColumn)), sourceAccount: get(mapping.accountColumn) || null,
      status, excluded, exclusionReason: excluded ? mapping.excludedStatuses === undefined ? 'Pending transaction — import after it posts.' : 'Excluded by posting-status selection.' : null, errors };
  });
}

function parseDate(value: string, format: CsvDateFormat): string | null {
  const yearFirst = format === 'YYYY-MM-DD' || format === 'YYYYMMDD';
  const shortYear = format.endsWith('YY') && !format.endsWith('YYYY');
  const pattern = format === 'YYYYMMDD' ? /^(\d{4})(\d{2})(\d{2})$/ : format === 'YYYY-MM-DD' ? /^(\d{4})-(\d{1,2})-(\d{1,2})$/ :
    format.includes('/') ? /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/ : format.includes('.') ? /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/ : /^(\d{1,2})-(\d{1,2})-(\d{4})$/;
  const match = value.match(shortYear ? new RegExp(pattern.source.replace('\\d{4}', '\\d{2}')) : pattern);
  if (!match) return null;
  const rawYear = Number(match[yearFirst ? 1 : 3]);
  const year = shortYear ? rawYear + (rawYear <= 69 ? 2000 : 1900) : rawYear;
  const month = Number(match[yearFirst ? 2 : format.startsWith('MM') ? 1 : 2]);
  const day = Number(match[yearFirst ? 3 : format.startsWith('MM') ? 2 : 1]);
  const date = new Date(0); date.setUTCFullYear(year, month - 1, day);
  if (year < 1 || date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return null;
  return `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
}

/** Dot-decimal currency only; reject ambiguous formats rather than changing their meaning. */
function money(value: string, decimalSeparator: '.' | ',' = '.'): number | null | undefined {
  if (!value) return null;
  let raw = value.replace(/^\$/, '');
  if (/^\(.*\)$/.test(raw)) raw = '-' + raw.slice(1, -1).replace(/^\$/, '');
  if (decimalSeparator === ',') raw = raw.replace(/[.,]/g, char => char === ',' ? '.' : ',');
  if (!/^[+-]?(?:(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?|\.\d{1,2})$/.test(raw)) return undefined;
  const result = Number(raw.replace(/,/g, ''));
  // Keep cents within JavaScript's exact-integer range; the backend remains authoritative.
  return Number.isFinite(result) && Math.abs(result) <= Number.MAX_SAFE_INTEGER / 100 ? result : undefined;
}
function decodeEntities(value: string): string {
  const entities: Record<string, string> = { amp: '&', quot: '"', apos: "'", lt: '<', gt: '>' };
  return value.replace(/&(amp|quot|apos|lt|gt);/g, (_, key: string) => entities[key]);
}
