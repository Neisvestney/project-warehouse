import newYearTree from "@/assets/new-year-tree.svg";
import type {CalendarDayHighlight} from "./calendarDayHighlightUtils";

export const calendarDayHighlights: readonly CalendarDayHighlight[] = [
  {
    date: {month: 12, day: 31},
    label: "Новый год",
    style: {
      backgroundImage: `url("${newYearTree}"), linear-gradient(135deg, rgba(233,180,67,0.09), rgba(38,150,117,0.09))`,
      backgroundPosition: "right 8px bottom 4px, center",
      backgroundSize: "70px 84px, cover",
      backgroundRepeat: "no-repeat",
      boxShadow: "inset 0 0 0 2px #d4ad62",
    },
  },
];
