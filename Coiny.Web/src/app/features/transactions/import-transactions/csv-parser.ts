export const CSV_MAX_ROWS = 2000;
export const CSV_MAX_BYTES = 2 * 1024 * 1024;
export type CsvDelimiter = ',' | ';' | '\t';
export interface CsvRecord { line: number; values: string[]; }
export interface CsvDocument { headers: string[]; rows: CsvRecord[]; delimiter: CsvDelimiter; }

export class CsvError extends Error {}

/** Read data only: quoted CSV fields are never evaluated as formulas or HTML. */
export function parseCsv(input: string, delimiter?: CsvDelimiter, headerLine = 1): CsvDocument {
  if (new TextEncoder().encode(input).length > CSV_MAX_BYTES)
    throw new CsvError('Choose a CSV file of 2 MB or less.');
  if (!Number.isInteger(headerLine) || headerLine < 1 || headerLine > 100)
    throw new CsvError('Choose a header line between 1 and 100.');
  const source = input.replace(/^\uFEFF/, '');
  let start = 0;
  for (let line = 1; line < headerLine; line++) {
    const next = /\r\n|\r|\n/.exec(source.slice(start));
    if (!next) throw new CsvError('The header line is past the end of this file.');
    start += next.index + next[0].length;
  }
  const text = source.slice(start);
  const separator = delimiter ?? detectDelimiter(text);
  const records: CsvRecord[] = [];
  let values: string[] = [], field = '', quoted = false, closedQuote = false;
  let line = headerLine, recordLine = headerLine, recordStarted = false;
  const endField = () => {
    values.push(field); field = ''; closedQuote = false;
    if (values.length > 100) throw new CsvError(`Line ${recordLine}: a file can have at most 100 columns.`);
  };
  const endRecord = () => {
    endField();
    if (values.some(value => value.trim() !== '')) records.push({ line: recordLine, values });
    values = []; recordStarted = false;
    if (records.length > CSV_MAX_ROWS + 1) throw new CsvError('Choose a file with no more than 2,000 transaction rows.');
  };
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (quoted) {
      if (char === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; }
        else { quoted = false; closedQuote = true; }
      } else if (char === '\r' || char === '\n') {
        if (char === '\r' && text[i + 1] === '\n') i++;
        field += '\n'; line++;
      } else field += char;
      continue;
    }
    if (char === separator) { endField(); recordStarted = true; }
    else if (char === '\r' || char === '\n') {
      if (char === '\r' && text[i + 1] === '\n') i++;
      endRecord(); line++; recordLine = line;
    } else if (char === '"') {
      if (field || closedQuote) throw new CsvError(`Line ${line}: unexpected quote. Check the CSV export.`);
      quoted = true; recordStarted = true;
    } else {
      if (closedQuote) {
        if (char === ' ' || char === '\t') continue;
        throw new CsvError(`Line ${line}: unexpected text after a quoted value.`);
      }
      field += char; recordStarted = true;
    }
  }
  if (quoted) throw new CsvError(`Line ${recordLine}: a quoted value is missing its closing quote.`);
  if (recordStarted || values.length || field || closedQuote) endRecord();
  if (records.length < 2) throw new CsvError('The file needs a header and at least one transaction row.');
  const headers = records.shift()!.values.map(value => value.trim());
  if (headers.some(value => !value)) throw new CsvError('Each column needs a header. Check the export or select the correct delimiter.');
  for (const row of records) if (row.values.length !== headers.length)
    throw new CsvError(`Line ${row.line}: expected ${headers.length} columns but found ${row.values.length}.`);
  return { headers, rows: records, delimiter: separator };
}

function detectDelimiter(text: string): CsvDelimiter {
  const counts: Record<CsvDelimiter, number> = { ',': 0, ';': 0, '\t': 0 };
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (char === '"') {
      if (quoted && text[i + 1] === '"') i++;
      else quoted = !quoted;
    } else if (!quoted) {
      if (char === '\r' || char === '\n') {
        if (Object.values(counts).some(Boolean)) break;
      } else if (char === ',' || char === ';' || char === '\t') counts[char]++;
    }
  }
  return (Object.keys(counts) as CsvDelimiter[]).sort((a, b) => counts[b] - counts[a])[0];
}
