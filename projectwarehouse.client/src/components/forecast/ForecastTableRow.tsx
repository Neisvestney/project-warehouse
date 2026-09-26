import {Box, IconButton, Stack, TableCell, TableRow, Tooltip, Typography} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import EditIcon from "@mui/icons-material/Edit";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import PushPinIcon from "@mui/icons-material/PushPin";
import type {StockForecastRowDto} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import StockForecastChip from "@/components/forecast/StockForecastChip";

/**
 * `0` — currently at zero. `null` — never hit zero anywhere in the window, so the label carries the
 * window length as a floor rather than an exact count.
 */
function formatDaysSinceZeroStock(days: number | null, windowDays: number): string {
  if (days === 0) return "—";
  if (days === null) return `${windowDays}+ дн.`;
  return `${days} дн.`;
}

interface ForecastTableRowProps {
  row: StockForecastRowDto;
  windowDays: number;
  dimmed: boolean;
  canEditThreshold: boolean;
  onOpenItem: (catalogItemId: string) => void;
  onEditThreshold: (row: StockForecastRowDto) => void;
  /** Reserves the chevron's place; set on every row of a page that has any variation row. */
  withExpandSlot: boolean;
  /** Set on a variation row; a member row nested under it passes `isMember` instead. */
  expanded?: boolean;
  onToggleExpanded?: () => void;
  isMember?: boolean;
}

function ForecastTableRow({
  row,
  windowDays,
  dimmed,
  canEditThreshold,
  onOpenItem,
  onEditThreshold,
  withExpandSlot,
  expanded,
  onToggleExpanded,
  isMember = false,
}: ForecastTableRowProps) {
  return (
    <TableRow
      sx={{
        opacity: dimmed ? 0.5 : 1,
        transition: "opacity 0.2s",
        bgcolor: isMember ? "action.hover" : undefined,
      }}
    >
      <TableCell sx={{width: 110, whiteSpace: "nowrap"}}>
        <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
          {withExpandSlot && (
            // Fixed slot on every row, so the type chips line up whether a row has a chevron or not.
            <Box sx={{width: 24, display: "flex", justifyContent: "center", flexShrink: 0}}>
              {onToggleExpanded && (
                <Tooltip title={expanded ? "Свернуть участников" : "Показать участников"}>
                  <IconButton size="small" sx={{m: -0.5}} onClick={onToggleExpanded}>
                    {expanded ? (
                      <KeyboardArrowDownIcon fontSize="small" />
                    ) : (
                      <KeyboardArrowRightIcon fontSize="small" />
                    )}
                  </IconButton>
                </Tooltip>
              )}
            </Box>
          )}
          <CatalogItemTypeChip type={row.catalogItem.type} />
        </Stack>
      </TableCell>
      <TableCell>
        <CatalogItemLink catalogItemId={row.catalogItemId} onOpen={onOpenItem}>
          <Typography variant="body2">{row.catalogItem.fullName}</Typography>
          {row.catalogItem.isArchived && (
            <ArchiveIcon sx={{fontSize: 14, color: "warning.main", flexShrink: 0}} />
          )}
        </CatalogItemLink>
      </TableCell>
      <TableCell>{row.catalogItem.article}</TableCell>
      <TableCell align="right">
        <Typography variant="body2" sx={{fontWeight: 500}}>
          {row.stock}
        </Typography>
      </TableCell>
      <TableCell align="right">{row.dailyConsumption}</TableCell>
      <TableCell align="right">
        <StockForecastChip forecast={row} />
      </TableCell>
      <TableCell align="right">
        <Typography variant="body2" color="text.secondary">
          {formatDaysSinceZeroStock(row.daysSinceLastZeroStock ?? null, windowDays)}
        </Typography>
      </TableCell>
      <TableCell align="right">
        <Typography variant="body2" color="text.secondary">
          {row.outOfStockDays > 0 ? `${row.outOfStockDays} дн.` : "—"}
        </Typography>
      </TableCell>
      <TableCell align="right">
        <Stack
          direction="row"
          spacing={0.5}
          sx={{alignItems: "center", justifyContent: "flex-end"}}
        >
          <Typography
            variant="body2"
            sx={{fontWeight: row.isWarningOverridden ? 600 : 400, whiteSpace: "nowrap"}}
          >
            {row.warningDays} дн.
          </Typography>
          {row.isWarningOverridden && (
            <Tooltip title="Порог задан для этой позиции">
              <PushPinIcon sx={{fontSize: 14, color: "info.main"}} />
            </Tooltip>
          )}
          {canEditThreshold && (
            <Tooltip title="Изменить порог">
              <IconButton size="small" onClick={() => onEditThreshold(row)}>
                <EditIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      </TableCell>
    </TableRow>
  );
}

export default ForecastTableRow;
