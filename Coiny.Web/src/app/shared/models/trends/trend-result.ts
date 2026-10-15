export interface TrendDataPoint {
  date: string;
  value: number;
}

export interface TrendResult {
  points: TrendDataPoint[];
  startingValue: number | null;
  endingValue: number | null;
  absoluteChange: number | null;
  percentageChange: number | null;
  direction: 'Up' | 'Down' | 'Neutral';
}
