import {useCallback, useMemo, useState} from "react";
import {useQuery} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {EventCalendar} from "@mui/x-scheduler";
import {ruRU} from "@mui/x-scheduler/locales";
import type {SchedulerEvent} from "@mui/x-scheduler/models";
import {ru as ruDateFns} from "date-fns/locale/ru";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";

// date-fns ru locale quirks fixed for EventCalendar:
// - month: genitive ("мая") → nominative ("Май") by forcing standalone context
// - day: abbreviated 3-letter ("пнд") → short 2-letter ("пн") for column headers
const ru = {
  ...ruDateFns,
  localize: {
    ...ruDateFns.localize,
    month: (...args: Parameters<typeof ruDateFns.localize.month>) =>
      ruDateFns.localize.month(args[0], {...args[1], context: "standalone"}),
    day: (...args: Parameters<typeof ruDateFns.localize.day>) =>
      ruDateFns.localize.day(args[0], {
        ...args[1],
        width: args[1]?.width === "abbreviated" ? "short" : args[1]?.width,
      }),
  },
};
import {eventsGetEventsOptions} from "@/api/@tanstack/react-query.gen";
import {resolveEntity} from "@/utils/appEntityUtils";
import {toDateOnly} from "@/utils/dateOnly";
import type React from "react";

export interface AppEventsProps {}

function AppEvents({}: AppEventsProps) {
  const navigate = useNavigate();
  const [visibleDate, setVisibleDate] = useState(() => new Date());

  const startDate = useMemo(
    () => toDateOnly(new Date(visibleDate.getFullYear(), visibleDate.getMonth(), 1)),
    [visibleDate],
  );
  const endDate = useMemo(
    () => toDateOnly(new Date(visibleDate.getFullYear(), visibleDate.getMonth() + 1, 0)),
    [visibleDate],
  );

  const {
    data = [],
    isLoading,
    isError,
  } = useQuery(
    eventsGetEventsOptions({
      query: {startDate, endDate},
    }),
  );

  const resolved = useMemo(() => data.map((dto) => resolveEntity(dto.appEntity)), [data]);

  const events: SchedulerEvent[] = useMemo(
    () =>
      resolved.map((r, i) => ({
        id: r.id ?? i,
        title: r.eventCalendarTitle,
        start: data[i].startDate,
        end: data[i].endDate,
        allDay: true,
        color: r.statusColor,
      })),
    [data, resolved],
  );

  // Map eventCalendarTitle → first ResolvedEntity with that title (collisions warned, first wins)
  const resolvedByTitle = useMemo(() => {
    const map = new Map<string, (typeof resolved)[number]>();
    for (let i = resolved.length - 1; i >= 0; i--) {
      const title = resolved[i].eventCalendarTitle;
      if (map.has(title)) {
        console.warn(`AppEvents: duplicate eventCalendarTitle "${title}", last occurrence wins`);
      }
      map.set(title, resolved[i]);
    }
    return map;
  }, [resolved]);

  // EventCalendar (alpha) has no onEventClick — intercept via capture phase DOM delegation.
  // In readOnly mode, clicking an event opens a readonly dialog; we prevent that and navigate instead.
  const handleClickCapture = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      const eventEl = (e.target as HTMLElement).closest<HTMLElement>(
        '[data-variant="filled"], [data-variant="compact"]',
      );
      if (!eventEl) return;

      const titleEl = eventEl.querySelector<HTMLElement>('[class*="EventTitle"]');
      const title = titleEl?.textContent?.trim();
      if (!title) return;

      const r = resolvedByTitle.get(title);
      if (!r || r.link === "no-link" || r.link === "#") return;

      e.stopPropagation();
      navigate(r.link);
    },
    [resolvedByTitle, navigate],
  );

  return (
    <div style={{height: "calc(min(800px, 100vh - 120px))", width: "100%", position: "relative"}}>
      <EventCalendar
        events={events}
        readOnly
        view="month"
        views={["month"]}
        dateLocale={ru}
        localeText={{
          ...ruRU.components.MuiEventCalendar.defaultProps.localeText,
          today: "Сегодня",
          hiddenEvents: (c) => `еще ${c}..`,
        }}
        defaultPreferences={{ampm: false, isSidePanelOpen: false}}
        onClickCapture={handleClickCapture}
        visibleDate={visibleDate}
        onVisibleDateChange={(d) => setVisibleDate(d as Date)}
      />
      {(isLoading || isError) && (
        <Paper
          elevation={3}
          sx={{
            position: "absolute",
            bottom: 16,
            right: 16,
            px: 2,
            py: 1,
            display: "flex",
            alignItems: "center",
            gap: 1,
            zIndex: 1,
          }}
        >
          {isLoading && (
            <>
              <CircularProgress size={16} />
              <Typography variant="body2">Загрузка событий...</Typography>
            </>
          )}
          {isError && (
            <Typography variant="body2" color="error">
              Ошибка загрузки событий
            </Typography>
          )}
        </Paper>
      )}
    </div>
  );
}

export default AppEvents;
