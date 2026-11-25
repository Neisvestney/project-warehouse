interface MonthDay {
  month: number;
  day: number;
}

interface SeasonPeriod {
  start: MonthDay;
  end: MonthDay;
}

export const seasons = [
  {id: "newYear", start: {month: 11, day: 25}, end: {month: 1, day: 14}},
] as const satisfies readonly ({id: string} & SeasonPeriod)[];

export type SeasonId = (typeof seasons)[number]["id"];

export function isWithinSeason(date: Date, period: SeasonPeriod): boolean {
  const day = (date.getMonth() + 1) * 100 + date.getDate();
  const start = period.start.month * 100 + period.start.day;
  const end = period.end.month * 100 + period.end.day;
  return start <= end ? day >= start && day <= end : day >= start || day <= end;
}
