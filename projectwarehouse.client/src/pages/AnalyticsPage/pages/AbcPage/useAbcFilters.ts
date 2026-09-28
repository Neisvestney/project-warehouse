import type {AbcClass, AnalyticsAbcBasis, AnalyticsAbcSubject, XyzClass} from "@/api/types.gen";
import {
  channelSelectionQuery,
  parseList,
  serializeList,
} from "@/components/analytics/channelsQuery";
import {
  type PeriodSelection,
  resolvePeriod,
  shiftPeriod,
  withPreset,
} from "@/components/analytics/period/periodSelection";
import {usePeriodParam} from "@/components/analytics/period/usePeriodParam";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {todayDateOnly} from "@/utils/dateOnly";
import {SUBJECTS} from "./abcSubjects";

const BASES: AnalyticsAbcBasis[] = ["units", "price", "payout"];
const ABC_CLASSES: AbcClass[] = ["a", "b", "c"];
const XYZ_CLASSES: XyzClass[] = ["x", "y", "z"];

/** The last finished quarter: a dozen full weeks for XYZ and no week still selling. */
function previousQuarter(): PeriodSelection {
  const today = todayDateOnly();
  return shiftPeriod(withPreset("quarter", {from: today, to: today}), -1);
}

function parseOneOf<T extends string>(values: T[]) {
  return (q: string | null): T | null => (values.includes(q as T) ? (q as T) : null);
}

export function useAbcFilters() {
  const [selection, setSelection] = usePeriodParam("period", previousQuarter());
  const [channels, setChannels] = useSyncedWithQueryState("channels", parseList, serializeList);
  const [directTagIds, setDirectTagIds] = useSyncedWithQueryState("tags", parseList, serializeList);
  const [basis, setBasis] = useSyncedWithQueryState<AnalyticsAbcBasis>(
    "basis",
    (q) => parseOneOf(BASES)(q) ?? "units",
    (v) => (v === "units" ? null : v),
  );
  const [subject, setSubject] = useSyncedWithQueryState<AnalyticsAbcSubject>(
    "by",
    (q) => parseOneOf(SUBJECTS)(q) ?? "catalogItem",
    (v) => (v === "catalogItem" ? null : v),
  );
  // On by default: counting the weeks before an item existed as zero demand classes every newcomer as Z
  const [xyzFromFirstSale, setXyzFromFirstSale] = useSyncedWithQueryState(
    "xyzfirst",
    (q) => q !== "0",
    (v) => (v ? null : "0"),
  );
  const [currencyCode, setCurrencyCode] = useSyncedWithQueryState<string | null>(
    "cur",
    (q) => q || null,
    (v) => v,
  );
  const [abcClass, setAbcClass] = useSyncedWithQueryState("abc", parseOneOf(ABC_CLASSES), (v) => v);
  const [xyzClass, setXyzClass] = useSyncedWithQueryState("xyz", parseOneOf(XYZ_CLASSES), (v) => v);
  const [searchInput, setSearchInput, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const today = todayDateOnly();
  const {from, to} = resolvePeriod(selection, {from: today, to: today});

  const filterQuery = {
    From: from,
    To: to,
    Basis: basis,
    Subject: subject,
    CurrencyCode: basis === "units" ? undefined : (currencyCode ?? undefined),
    XyzFromFirstSale: xyzFromFirstSale,
    AbcClass: abcClass ?? undefined,
    XyzClass: xyzClass ?? undefined,
    SearchString: searchString || undefined,
    ...channelSelectionQuery(channels),
    DirectTagIds: directTagIds,
  };

  // Any filter change sends the table back to its first page
  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    filterQuery,
    [JSON.stringify(filterQuery)],
    {defaultPageSize: 25},
  );
  const {page: fetchPage, pageSize: fetchPageSize, ...rest} = fetchParams;

  return {
    query: {...rest, Page: fetchPage, PageSize: fetchPageSize},
    selection,
    from,
    to,
    channels,
    directTagIds,
    basis,
    subject,
    xyzFromFirstSale,
    abcClass,
    xyzClass,
    searchInput,
    page,
    pageSize,
    setSelection,
    setChannels,
    setDirectTagIds,
    setBasis,
    setSubject,
    setXyzFromFirstSale,
    setCurrencyCode,
    setMatrixCell: (abc: AbcClass | null, xyz: XyzClass | null) => {
      setAbcClass(abc);
      setXyzClass(xyz);
    },
    setSearchInput,
    setPage,
    setPageSize,
  };
}
