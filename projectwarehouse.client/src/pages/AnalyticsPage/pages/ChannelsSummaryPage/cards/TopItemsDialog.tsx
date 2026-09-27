import {useState} from "react";
import {
  Alert,
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  LinearProgress,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import SearchInput from "@/components/SearchInput";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage} from "@/utils/errorUtils";
import {periodLabel} from "../period/periodSelection";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import TopItemsTable from "./TopItemsTable";
import {useTopItemsQuery, useTopItemsState} from "./useTopItems";

interface TopItemsDialogProps {
  open: boolean;
  onClose: () => void;
  filters: ReturnType<typeof useChannelsSummaryFilters>;
}

function TopItemsDialog({open, onClose, filters}: TopItemsDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [shown, release] = useRetainedValue(open || null);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      fullScreen={isMobile}
      slotProps={{
        transition: {onExited: release},
        paper: {sx: {pointerEvents: open ? undefined : "none", height: {sm: "85vh"}}},
      }}
    >
      {shown && <TopItemsDialogContent filters={filters} onClose={onClose} />}
    </Dialog>
  );
}

/**
 * Mounted per opening, so the search starts empty every time. The ranking is picked on the card and only
 * named here: the dialog's history entry is walked back on close, and a change made inside would go with it.
 */
function TopItemsDialogContent({
  filters,
  onClose,
}: Pick<TopItemsDialogProps, "filters" | "onClose">) {
  const state = useTopItemsState(filters);
  const {data, error, isError, isFetching} = useTopItemsQuery(state, undefined);
  const [search, setSearch] = useState("");

  const needle = search.trim().toLocaleLowerCase("ru");
  const rows = (data?.items ?? [])
    .map((item, i) => ({rank: i + 1, item}))
    .filter(({item}) => !needle || item.name.toLocaleLowerCase("ru").includes(needle));

  return (
    <>
      <DialogTitle sx={{display: "flex", alignItems: "center", gap: 1, pr: 1}}>
        <Box sx={{flexGrow: 1, minWidth: 0}}>
          Топ товаров
          <Typography variant="body2" color="text.secondary">
            {[
              state.channelLabel,
              state.by === "money" && data?.currencyCode
                ? `по деньгам, ${data.currencyCode}`
                : "по штукам",
              periodLabel(state.selection, state.period),
            ].join(" · ")}
          </Typography>
        </Box>
        <IconButton onClick={onClose} aria-label="Закрыть">
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <Box sx={{px: 3, pb: 1.5}}>
        <SearchInput value={search} onChange={setSearch} sx={{width: 240, maxWidth: "100%"}} />
      </Box>
      <Box sx={{height: 2}}>{isFetching && <LinearProgress sx={{height: 2}} />}</Box>
      <DialogContent dividers>
        {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
        {data &&
          (rows.length === 0 ? (
            <Typography color="text.secondary">
              {needle ? "Ничего не найдено" : "Продаж товаров за период нет"}
            </Typography>
          ) : (
            <TopItemsTable rows={rows} currencyCode={data.currencyCode} />
          ))}
      </DialogContent>
    </>
  );
}

export default TopItemsDialog;
