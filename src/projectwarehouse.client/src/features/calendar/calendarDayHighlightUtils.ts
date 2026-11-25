import type {CSSObject} from "@mui/material/styles";

export interface CalendarDayHighlight {
  date: {month: number; day: number; year?: number};
  label: string;
  style: Pick<
    CSSObject,
    | "backgroundImage"
    | "backgroundPosition"
    | "backgroundSize"
    | "backgroundRepeat"
    | "backgroundColor"
    | "boxShadow"
  >;
}

export interface CalendarWeekLayout {
  weekStartsOn: number;
  showWeekends: boolean;
}

export function findCalendarDayHighlight(date: Date, highlights: readonly CalendarDayHighlight[]) {
  return highlights.find(
    ({date: match}) =>
      match.month === date.getMonth() + 1 &&
      match.day === date.getDate() &&
      (match.year === undefined || match.year === date.getFullYear()),
  );
}

export function getMonthGridDays(visibleDate: Date, layout: CalendarWeekLayout) {
  const year = visibleDate.getFullYear();
  const month = visibleDate.getMonth();
  const offset = (new Date(year, month, 1).getDay() - layout.weekStartsOn + 7) % 7;
  const date = new Date(year, month, 1 - offset);
  const lastDay = new Date(year, month + 1, 0);
  const cells: {date: Date; row: number; column: number}[] = [];

  for (let row = 1; date <= lastDay; row++) {
    let column = 0;
    for (let day = 0; day < 7; day++) {
      const weekday = date.getDay();
      if (layout.showWeekends || (weekday !== 0 && weekday !== 6)) {
        cells.push({date: new Date(date), row, column: ++column});
      }
      date.setDate(date.getDate() + 1);
    }
  }

  return cells;
}
