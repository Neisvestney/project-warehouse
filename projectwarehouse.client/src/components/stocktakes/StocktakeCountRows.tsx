import {
  Box,
  Checkbox,
  FormControlLabel,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import StocktakeItemName from "@/components/stocktakes/StocktakeItemName";
import {deltaColor, formatDelta} from "@/components/stocktakes/stocktakeUtils";
import type {DraftRow} from "@/components/stocktakes/stocktakeDraft";

type RowEdit = Partial<Pick<DraftRow, "counted" | "notes">>;

interface StocktakeCountRowsProps {
  rows: DraftRow[];
  canEdit: boolean;
  disabled: boolean;
  onPatch: (key: string, patch: RowEdit) => void;
  onRemove: (key: string) => void;
  onOpenCatalogItem: (id: string) => void;
}

interface RowControlProps {
  row: DraftRow;
  disabled: boolean;
  onPatch: (key: string, patch: RowEdit) => void;
}

function CountedControl({
  row,
  disabled,
  onPatch,
  withLabel,
}: RowControlProps & {withLabel?: boolean}) {
  if (row.kind === "unit") {
    const checkbox = (
      <Checkbox
        size="small"
        checked={row.counted > 0}
        disabled={disabled}
        onChange={(e) => onPatch(row.key, {counted: e.target.checked ? 1 : 0})}
      />
    );
    return withLabel ? (
      <FormControlLabel control={checkbox} label="Найден" sx={{mr: 0}} />
    ) : (
      checkbox
    );
  }
  return (
    <ClampedIntegerField
      label={withLabel ? "Посчитано" : undefined}
      value={row.counted}
      min={0}
      size="small"
      disabled={disabled}
      onCommit={(v) => onPatch(row.key, {counted: v})}
      sx={{width: withLabel ? 120 : 96}}
    />
  );
}

function NotesField({row, disabled, onPatch, withLabel}: RowControlProps & {withLabel?: boolean}) {
  return (
    <TextField
      label={withLabel ? "Примечание" : undefined}
      size="small"
      fullWidth
      value={row.notes}
      disabled={disabled}
      onChange={(e) => onPatch(row.key, {notes: e.target.value})}
    />
  );
}

function LabeledValue({
  label,
  value,
  color,
}: {
  label: string;
  value: string | number;
  color?: string;
}) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" component="div">
        {label}
      </Typography>
      <Typography variant="body2" sx={{color}}>
        {value}
      </Typography>
    </Box>
  );
}

function StocktakeCountRows({
  rows,
  canEdit,
  disabled,
  onPatch,
  onRemove,
  onOpenCatalogItem,
}: StocktakeCountRowsProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const controlsDisabled = !canEdit || disabled;

  if (isMobile) {
    return (
      <Stack spacing={1}>
        {rows.map((row) => {
          const delta = row.counted - row.expected;
          return (
            <Paper key={row.key} variant="outlined" sx={{p: 1.5}}>
              <Stack spacing={1.5}>
                <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
                  <Box sx={{flexGrow: 1, minWidth: 0}}>
                    <StocktakeItemName
                      catalogItemId={row.catalogItemId}
                      catalogItemName={row.catalogItemName}
                      inventoryNumber={row.inventoryNumber}
                      onOpen={onOpenCatalogItem}
                    />
                  </Box>
                  {canEdit && row.expected === 0 && (
                    <IconButton
                      size="small"
                      disabled={disabled}
                      onClick={() => onRemove(row.key)}
                      sx={{mt: -0.5, mr: -0.5}}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </Stack>
                <Stack direction="row" spacing={2} sx={{alignItems: "center"}}>
                  <CountedControl
                    row={row}
                    disabled={controlsDisabled}
                    onPatch={onPatch}
                    withLabel
                  />
                  <LabeledValue label="Ожидается" value={row.expected} />
                  <LabeledValue label="Δ" value={formatDelta(delta)} color={deltaColor(delta)} />
                </Stack>
                <NotesField row={row} disabled={controlsDisabled} onPatch={onPatch} withLabel />
              </Stack>
            </Paper>
          );
        })}
      </Stack>
    );
  }

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>Товар</TableCell>
          <TableCell align="right">Ожидается</TableCell>
          <TableCell align="right">Посчитано</TableCell>
          <TableCell align="right">Δ</TableCell>
          <TableCell>Примечание</TableCell>
          <TableCell width={48} />
        </TableRow>
      </TableHead>
      <TableBody>
        {rows.map((row) => {
          const delta = row.counted - row.expected;
          return (
            <TableRow key={row.key} hover>
              <TableCell>
                <StocktakeItemName
                  catalogItemId={row.catalogItemId}
                  catalogItemName={row.catalogItemName}
                  inventoryNumber={row.inventoryNumber}
                  onOpen={onOpenCatalogItem}
                />
              </TableCell>
              <TableCell align="right">{row.expected}</TableCell>
              <TableCell align="right">
                <CountedControl row={row} disabled={controlsDisabled} onPatch={onPatch} />
              </TableCell>
              <TableCell align="right" sx={{color: deltaColor(delta)}}>
                {formatDelta(delta)}
              </TableCell>
              <TableCell>
                <NotesField row={row} disabled={controlsDisabled} onPatch={onPatch} />
              </TableCell>
              <TableCell>
                {canEdit && row.expected === 0 && (
                  <IconButton size="small" disabled={disabled} onClick={() => onRemove(row.key)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

export default StocktakeCountRows;
