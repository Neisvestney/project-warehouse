import {useState} from "react";
import {IconButton, Table, TableBody, TableCell, TableHead, TableRow, Tooltip} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import {useQuery} from "@tanstack/react-query";
import {marketplacesGetSyncRunsOptions} from "@/api/@tanstack/react-query.gen";
import type {MarketplaceSyncRunDto} from "@/api/types.gen";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import MarketplaceStatusChip from "../../components/MarketplaceStatusChip";
import {
  SYNC_SCOPE_LABELS,
  formatDateTime,
  formatDuration,
  syncRunCreatedTotal,
  syncRunProcessedTotal,
} from "../../marketplaceUtils";
import SyncRunDetailsDialog from "./SyncRunDetailsDialog";

/** Запасной опрос: работает, только пока страница не подписана на аккаунт по SSE. */
const RUNNING_POLL_MS = 3000;

interface AccountSyncRunsTabProps {
  accountId: string;
  isRunning: boolean;
  isLive: boolean;
}

function AccountSyncRunsTab({accountId, isRunning, isLive}: AccountSyncRunsTabProps) {
  const [selected, setSelected] = useState<MarketplaceSyncRunDto | null>(null);

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams({}, [], {}, []);

  const {data, isLoading, isFetching} = useQuery({
    ...marketplacesGetSyncRunsOptions({path: {id: accountId}, query: fetchParams}),
    refetchInterval: !isLive && isRunning ? RUNNING_POLL_MS : false,
  });

  // prefer the refetched copy so an open dialog follows a running sync
  const selectedRun = selected && (data?.items.find((run) => run.id === selected.id) ?? selected);

  return (
    <>
      <DataTableContainer
        isFetching={isFetching}
        count={data?.total ?? 0}
        page={page}
        onPageChange={setPage}
        rowsPerPage={pageSize}
        onRowsPerPageChange={setPageSize}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Начат</TableCell>
              <TableCell>Длительность</TableCell>
              <TableCell>Объём</TableCell>
              <TableCell>Статус</TableCell>
              <TableCell align="right">Обработано</TableCell>
              <TableCell align="right">Новых</TableCell>
              <TableCell>Кем запущен</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Синхронизаций ещё не было" />
            ) : (
              data?.items.map((run) => (
                <TableRow
                  key={run.id}
                  hover
                  onClick={() => setSelected(run)}
                  sx={{cursor: "pointer"}}
                >
                  <TableCell>{formatDateTime(run.startedAt)}</TableCell>
                  <TableCell>{formatDuration(run.startedAt, run.finishedAt)}</TableCell>
                  <TableCell>{SYNC_SCOPE_LABELS[run.scope]}</TableCell>
                  <TableCell>
                    <MarketplaceStatusChip status={run.status} />
                  </TableCell>
                  <TableCell align="right">{syncRunProcessedTotal(run)}</TableCell>
                  <TableCell align="right">{syncRunCreatedTotal(run)}</TableCell>
                  <TableCell>{run.triggeredByName ?? "Планировщик"}</TableCell>
                  <TableCell sx={{width: 48}}>
                    <Tooltip title="Подробнее">
                      <IconButton size="small" onClick={() => setSelected(run)}>
                        <InfoOutlinedIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
      <SyncRunDetailsDialog run={selectedRun} onClose={() => setSelected(null)} />
    </>
  );
}

export default AccountSyncRunsTab;
