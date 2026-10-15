export type TrendRange = '1D' | '1W' | '1M' | '3M' | '6M' | '1Y' | 'YTD';

export function getTrendDates(range: TrendRange, now = new Date()): { startDate: string; endDate: string } {
  // Match the API's calendar day, independently of the browser timezone.
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(now);
  const part = (name: string) => Number(parts.find(value => value.type === name)!.value);
  const year = part('year');
  const month = part('month') - 1;
  const day = part('day');
  const end = new Date(Date.UTC(year, month, day));
  let start: Date;
  if (range === '1D' || range === '1W') {
    start = new Date(Date.UTC(year, month, day - (range === '1D' ? 1 : 7)));
  } else if (range === 'YTD') {
    start = new Date(Date.UTC(year, 0, 1));
  } else {
    const months = { '1M': 1, '3M': 3, '6M': 6, '1Y': 12 }[range];
    const target = new Date(Date.UTC(year, month - months, 1));
    const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
    start = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth(), Math.min(day, lastDay)));
  }
  return { startDate: start.toISOString().slice(0, 10), endDate: end.toISOString().slice(0, 10) };
}
