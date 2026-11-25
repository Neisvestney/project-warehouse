import type {CSSObject} from "@mui/material/styles";
import {eventCalendarClasses} from "@mui/x-scheduler/event-calendar";
import {
  findCalendarDayHighlight,
  getMonthGridDays,
  type CalendarDayHighlight,
  type CalendarWeekLayout,
} from "@/features/calendar/calendarDayHighlightUtils";

export function getCalendarDayHighlightStyle(highlight: CalendarDayHighlight): CSSObject {
  return {
    ...highlight.style,
    "@media print": {backgroundImage: "none", backgroundColor: "transparent", boxShadow: "none"},
  };
}

export function getMonthDayHighlightStyles(
  visibleDate: Date,
  layout: CalendarWeekLayout,
  highlights: readonly CalendarDayHighlight[],
): CSSObject {
  const styles: CSSObject = {};

  // Scheduler has no day-cell slot; isolate its DOM-specific selectors here.
  for (const {date, row, column} of getMonthGridDays(visibleDate, layout)) {
    const highlight = findCalendarDayHighlight(date, highlights);
    if (!highlight) continue;
    const selector = `& .${eventCalendarClasses.monthViewRow}:nth-child(${row}) .${eventCalendarClasses.monthViewCell}[aria-colindex="${column}"]`;
    styles[selector] = getCalendarDayHighlightStyle(highlight);
  }

  return styles;
}
