import {useEffect, useId, useMemo, useRef, useState} from "react";
import {useQuery} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {EventCalendar} from "@mui/x-scheduler";
import {ruRU} from "@mui/x-scheduler/locales";
import type {SchedulerEvent} from "@mui/x-scheduler/models";
import {ru as ruDateFns} from "date-fns/locale/ru";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import {Box, Button, IconButton, Stack, useMediaQuery} from "@mui/material";
import * as colors from "@mui/material/colors";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {Link} from "react-router";

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
import {
  addDays,
  formatDateOnly,
  formatWeekday,
  parseDateOnly,
  todayDateOnly,
  toDateOnly,
} from "@/utils/dateOnly";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useSettledValue} from "@/hooks/useSettledValue";
import LoadingOverlay from "@/components/LoadingOverlay";

const monthKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;

function parseMonth(q: string | null): Date {
  const match = q?.match(/^(\d{4})-(\d{2})$/);
  const month = match ? Number(match[2]) : 0;
  if (!match || month < 1 || month > 12) return new Date();
  return new Date(Number(match[1]), month - 1, 1);
}

// The current month is the default, so it is dropped from the URL.
function formatMonth(d: Date): string | null {
  const key = monthKey(d);
  return key === monthKey(new Date()) ? null : key;
}

function parseDay(q: string | null): string {
  return q && /^\d{4}-\d{2}-\d{2}$/.test(q) && toDateOnly(parseDateOnly(q)) === q
    ? q
    : todayDateOnly();
}

function formatDay(day: string): string | null {
  return day === todayDateOnly() ? null : day;
}

export interface AppEventsProps {}

function AppEvents({}: AppEventsProps) {
  const navigate = useNavigate();
  const isMobile = useMediaQuery((theme) => theme.breakpoints.down("sm"));
  const [visibleDate, setVisibleDate] = useSyncedWithQueryState("month", parseMonth, formatMonth);
  const [visibleDay, setVisibleDay] = useSyncedWithQueryState("eventDay", parseDay, formatDay);
  const days = [visibleDay, addDays(visibleDay, -1), addDays(visibleDay, -2)];

  const startDate = useMemo(
    () => toDateOnly(new Date(visibleDate.getFullYear(), visibleDate.getMonth(), 1)),
    [visibleDate],
  );
  const endDate = useMemo(
    () => toDateOnly(new Date(visibleDate.getFullYear(), visibleDate.getMonth() + 1, 0)),
    [visibleDate],
  );

  const {
    data: queryData,
    isLoading,
    isFetching,
    isError,
  } = useQuery(
    eventsGetEventsOptions({
      query: {
        startDate: isMobile ? days[2] : startDate,
        endDate: isMobile ? visibleDay : endDate,
      },
    }),
  );
  const mobileSnapshot = useMemo(
    () => ({data: queryData ?? [], visibleDay}),
    [queryData, visibleDay],
  );
  const data = mobileSnapshot.data;
  const settledMobile = useSettledValue(mobileSnapshot, isFetching);

  const resolved = useMemo(() => data.map((dto) => resolveEntity(dto.appEntity)), [data]);

  const events: SchedulerEvent[] = useMemo(
    () =>
      resolved.map((r, i) => ({
        id: `${r.type}:${r.id ?? i}`,
        title: r.eventCalendarTitle,
        start: data[i].startDate,
        end: data[i].endDate,
        allDay: true,
        color: r.statusColor,
      })),
    [data, resolved],
  );

  const linkById = new Map(events.map((e, i) => [e.id, resolved[i].link]));

  if (isMobile) {
    const shownDay = settledMobile?.visibleDay ?? visibleDay;
    const shownDays = [shownDay, addDays(shownDay, -1), addDays(shownDay, -2)];
    const shownData = settledMobile?.data ?? [];
    return (
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between", gap: 1}}>
          <IconButton
            aria-label="Предыдущие три дня"
            disabled={isFetching}
            onClick={() => setVisibleDay(addDays(visibleDay, -3))}
          >
            <ChevronLeftIcon />
          </IconButton>
          <Button disabled={isFetching} onClick={() => setVisibleDay(todayDateOnly())}>
            Сегодня
          </Button>
          <IconButton
            aria-label="Следующие три дня"
            disabled={isFetching}
            onClick={() => setVisibleDay(addDays(visibleDay, 3))}
          >
            <ChevronRightIcon />
          </IconButton>
        </Stack>
        <Box sx={{position: "relative"}} aria-busy={isFetching}>
          <LoadingOverlay open={isFetching} label="Загрузка событий..." />
          <Stack spacing={1.5}>
            {shownDays.map((day) => (
              <MobileEventDayCard
                key={`${shownDay}:${day}`}
                day={day}
                events={shownData
                  .filter((dto) => dto.startDate <= day && dto.endDate >= day)
                  .map((dto) => resolveEntity(dto.appEntity))}
                loading={isFetching || isError}
              />
            ))}
          </Stack>
          {isError && (
            <Paper
              elevation={3}
              sx={{position: "absolute", bottom: 16, right: 16, p: 1, zIndex: 1}}
            >
              <Typography variant="body2" color="error" role="alert">
                Ошибка загрузки событий
              </Typography>
            </Paper>
          )}
        </Box>
      </Stack>
    );
  }

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
        onEventEditingStart={(occurrence, details) => {
          details.cancel();
          const link = linkById.get(occurrence.id);
          if (link && link !== "no-link" && link !== "#") navigate(link);
        }}
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

function MobileEventDayCard({
  day,
  events,
  loading,
}: {
  day: string;
  events: ReturnType<typeof resolveEntity>[];
  loading: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const [visibleCount, setVisibleCount] = useState(0);
  const [hasClippedTitle, setHasClippedTitle] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const hiddenCount = Math.max(0, events.length - visibleCount);
  const canExpand = expanded || hiddenCount > 0 || hasClippedTitle;

  useEffect(() => {
    const list = listRef.current;
    const content = contentRef.current;
    if (!list || !content || expanded) return;
    const rows = Array.from(content.children) as HTMLElement[];
    const observer = new ResizeObserver(() => {
      const bottom = list.getBoundingClientRect().bottom;
      const count = rows.findIndex((row) => row.getBoundingClientRect().bottom > bottom + 0.5);
      const fittedCount = count === -1 ? events.length : count;
      setVisibleCount(fittedCount);
      setHasClippedTitle(
        rows.slice(0, fittedCount).some((row) => {
          const title = row.querySelector<HTMLElement>("[data-event-title]");
          return (
            title != null &&
            (title.scrollHeight > title.clientHeight || title.scrollWidth > title.clientWidth)
          );
        }),
      );
    });
    observer.observe(list);
    observer.observe(content);
    rows.forEach((row) => observer.observe(row));
    return () => observer.disconnect();
  }, [events, expanded]);

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 2,
        height: expanded ? "auto" : 232,
        minHeight: 232,
        boxSizing: "border-box",
        display: "flex",
        flexDirection: "column",
      }}
    >
      <Typography variant="subtitle1" component="h2" noWrap sx={{mb: 1, flexShrink: 0}}>
        {day === todayDateOnly() ? "Сегодня" : formatWeekday(day)}, {formatDateOnly(day)}
      </Typography>
      <Box
        ref={listRef}
        id={listId}
        sx={{
          flex: expanded ? "0 0 auto" : "1 1 0",
          minHeight: 0,
          overflow: expanded ? "visible" : "hidden",
        }}
      >
        <Stack ref={contentRef} spacing={1}>
          {events.map((event, i) => {
            const hidden = !expanded && i >= visibleCount;
            const hasLink = event.link && event.link !== "no-link" && event.link !== "#";
            const content = (
              <Box
                sx={{
                  borderLeft: 3,
                  borderColor: colors[event.statusColor][500],
                  pl: 1,
                  py: 0.5,
                  boxSizing: "border-box",
                  overflow: "hidden",
                  overflowWrap: "anywhere",
                }}
              >
                <Typography
                  data-event-title
                  variant="body2"
                  sx={
                    expanded
                      ? undefined
                      : {
                          display: "-webkit-box",
                          WebkitBoxOrient: "vertical",
                          WebkitLineClamp: 2,
                          overflow: "hidden",
                        }
                  }
                >
                  {event.eventCalendarTitle}
                </Typography>
              </Box>
            );
            return hasLink ? (
              <Box
                key={`${event.type}:${event.id ?? i}`}
                aria-hidden={hidden || undefined}
                component={Link}
                to={event.link}
                sx={{
                  color: "text.primary",
                  textDecoration: "none",
                  visibility: hidden ? "hidden" : "visible",
                  "&:hover": {bgcolor: "action.hover"},
                }}
              >
                {content}
              </Box>
            ) : (
              <Box
                key={`${event.type}:${event.id ?? i}`}
                aria-hidden={hidden || undefined}
                sx={{visibility: hidden ? "hidden" : "visible"}}
              >
                {content}
              </Box>
            );
          })}
          {!loading && events.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Нет событий
            </Typography>
          )}
        </Stack>
      </Box>
      <Box sx={{mt: "auto", pt: 1, height: 40, boxSizing: "border-box", flexShrink: 0}}>
        {canExpand && (
          <Button
            size="small"
            aria-expanded={expanded}
            aria-controls={listId}
            onClick={() => setExpanded(!expanded)}
          >
            {expanded ? "Свернуть" : hiddenCount > 0 ? `Ещё ${hiddenCount}` : "Ещё"}
          </Button>
        )}
      </Box>
    </Paper>
  );
}
