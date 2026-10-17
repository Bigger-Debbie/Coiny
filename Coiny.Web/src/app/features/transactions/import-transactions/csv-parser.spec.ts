import { CSV_MAX_BYTES, parseCsv } from './csv-parser';

describe('CSV parsing', () => {
  it('handles BOM, CRLF, quoted delimiters, escaped quotes, and multiline descriptions', () => {
    const csv = parseCsv('\uFEFFDate,Description,Debit,Credit\r\n10/16/2026,"Coffee, \"\"large\"\"\r\nsecond line",.18,\r\n');
    expect(csv.headers).toEqual(['Date', 'Description', 'Debit', 'Credit']);
    expect(csv.rows).toEqual([{ line: 2, values: ['10/16/2026', 'Coffee, "large"\nsecond line', '.18', ''] }]);
  });
  it.each([';', '\t'] as const)('detects %s without counting delimiters inside quotes', delimiter => {
    const csv = parseCsv(`Date${delimiter}"Description, details"${delimiter}Amount\n2026-10-16${delimiter}Coffee${delimiter}-5`);
    expect(csv.delimiter).toBe(delimiter);
    expect(csv.rows[0].values).toEqual(['2026-10-16', 'Coffee', '-5']);
  });
  it('preserves blank optional cells, source line numbers, and inert formula-like strings', () => {
    const csv = parseCsv('\nDate,Description,Amount\n\n2026-10-16,=SUM(A1:A2),5\n');
    expect(csv.rows[0].line).toBe(4);
    expect(csv.rows[0].values[1]).toBe('=SUM(A1:A2)');
  });
  it.each([
    ['Date,Amount\n"unclosed,5', 'closing quote'],
    ['Date,Amount\n"date"extra,5', 'unexpected text'],
    ['Date,Amount\n20"26,5', 'unexpected quote'],
    ['Date,Amount\n2026,5,extra', 'expected 2 columns'],
    ['Date,,Amount\n2026,desc,5', 'header'],
    ['Date,Amount\n', 'at least one'],
  ])('rejects malformed CSV: %s', (input, message) => {
    expect(() => parseCsv(input)).toThrow(message);
  });
  it('accepts 2,000 transaction rows and rejects the next row', () => {
    const csv = 'Date,Amount\n' + Array(2000).fill('2026-10-16,5').join('\n');
    expect(parseCsv(csv).rows).toHaveLength(2000);
    expect(() => parseCsv(csv + '\n2026-10-16,5')).toThrow('2,000');
  });
  it('enforces byte and column limits', () => {
    expect(() => parseCsv('é'.repeat(CSV_MAX_BYTES / 2 + 1))).toThrow('2 MB');
    expect(() => parseCsv(Array(101).fill('column').join(',') + '\n1')).toThrow('100 columns');
  });
  it('skips a bank-specific preamble only when the header line is explicitly selected', () => {
    const csv = parseCsv('Example bank export\r\nAccount summary,not,a,header\r\nWhen;Memo;Value\r\n17.10.2026;Coffee;-4,50', ';', 3);
    expect(csv.headers).toEqual(['When', 'Memo', 'Value']);
    expect(csv.rows[0]).toEqual({ line: 4, values: ['17.10.2026', 'Coffee', '-4,50'] });
    expect(() => parseCsv('Header\nData', ',', 3)).toThrow('past the end');
  });
});
