import { getTrendDates } from './trend-range';
describe('trend date ranges', () => {
  it('uses seven calendar days back through today for 1W', () => {
    expect(getTrendDates('1W', new Date('2026-01-03T16:00:00Z'))).toEqual({ startDate: '2025-12-27', endDate: '2026-01-03' });
    expect(getTrendDates('1W', new Date('2026-03-09T04:30:00Z'))).toEqual({ startDate: '2026-03-02', endDate: '2026-03-09' });
  });
  it('uses yesterday through today for 1D across year boundaries', () => {
    expect(getTrendDates('1D', new Date('2026-01-01T16:00:00Z'))).toEqual({ startDate: '2025-12-31', endDate: '2026-01-01' });
  });
  it('keeps 1D on calendar dates across daylight-saving time', () => {
    expect(getTrendDates('1D', new Date('2026-03-09T04:30:00Z'))).toEqual({ startDate: '2026-03-08', endDate: '2026-03-09' });
  });
  it('clamps calendar months to the previous month end', () => {
    expect(getTrendDates('1M', new Date('2026-03-31T16:00:00Z'))).toEqual({ startDate: '2026-02-28', endDate: '2026-03-31' });
  });
  it('handles leap years', () => {
    expect(getTrendDates('1Y', new Date('2024-02-29T16:00:00Z'))).toEqual({ startDate: '2023-02-28', endDate: '2024-02-29' });
  });
  it('matches the API timezone after UTC midnight', () => {
    expect(getTrendDates('YTD', new Date('2026-01-01T02:00:00Z'))).toEqual({ startDate: '2025-01-01', endDate: '2025-12-31' });
  });
  it('supports three and six months', () => {
    const now = new Date('2026-10-15T16:00:00Z');
    expect(getTrendDates('3M', now).startDate).toBe('2026-07-15');
    expect(getTrendDates('6M', now).startDate).toBe('2026-04-15');
  });
});
