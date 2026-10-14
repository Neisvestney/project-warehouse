import {Box, Skeleton, Stack, Tab, Tabs} from "@mui/material";

interface StatusCount<T extends string> {
  status: T;
  count: number;
}

interface StatusTabsProps<T extends string> {
  value: T | "";
  onChange: (value: T | "") => void;
  /** Tab order; a status left out gets no tab. */
  statuses: T[];
  labels: Record<T, string>;
  /** From the list endpoint's `meta.statusCounts`; omitted while nothing has loaded yet. */
  counts?: StatusCount<T>[];
  /** Covers every badge with a skeleton while the list is fetching, even over retained counts. */
  loading?: boolean;
}

function CountBadge({count, loading}: {count: number | undefined; loading: boolean}) {
  // same box as the badge, so the tabs keep their width when the numbers arrive
  if (count === undefined)
    return <Skeleton variant="rounded" width={24} height={20} sx={{borderRadius: 1}} />;
  const badge = (
    <Box
      component="span"
      sx={{
        px: 0.75,
        borderRadius: 1,
        typography: "caption",
        fontWeight: 600,
        lineHeight: "20px",
        bgcolor: "action.selected",
        color: count === 0 ? "text.disabled" : "text.primary",
      }}
    >
      {count.toLocaleString("ru-RU")}
    </Box>
  );
  // a skeleton with children takes their size, so the tab keeps the retained number's width
  return loading ? (
    <Skeleton variant="rounded" sx={{borderRadius: 1}}>
      {badge}
    </Skeleton>
  ) : (
    badge
  );
}

function StatusTabs<T extends string>({
  value,
  onChange,
  statuses,
  labels,
  counts,
  loading = false,
}: StatusTabsProps<T>) {
  const countOf = (status: T) => counts?.find((c) => c.status === status)?.count;
  const total = counts?.reduce((sum, c) => sum + c.count, 0);

  const label = (text: string, count: number | undefined) => (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <span>{text}</span>
      <CountBadge count={count} loading={loading} />
    </Stack>
  );

  return (
    <Tabs
      value={value}
      onChange={(_, v: T | "") => onChange(v)}
      variant="scrollable"
      scrollButtons="auto"
      allowScrollButtonsMobile
      sx={{borderBottom: 1, borderColor: "divider"}}
    >
      <Tab value="" label={label("Все", total)} />
      {statuses.map((s) => (
        <Tab key={s} value={s} label={label(labels[s], countOf(s))} />
      ))}
    </Tabs>
  );
}

export default StatusTabs;
