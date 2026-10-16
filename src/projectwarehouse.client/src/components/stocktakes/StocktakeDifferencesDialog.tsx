import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  stocktakesFinishMutation,
  stocktakesGetDifferencesOptions,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {
  DIFFERENCE_RESOLUTION_LABELS,
  deltaColor,
  formatDelta,
} from "@/components/stocktakes/stocktakeUtils";
import StocktakeItemName from "@/components/stocktakes/StocktakeItemName";
import type {StocktakeDifferenceLineDto, StocktakeDto} from "@/api/types.gen";

function lineKey(line: StocktakeDifferenceLineDto, index: number): string {
  return `${line.catalogItemId}-${line.inventoryNumber ?? index}`;
}

function ResolutionText({line}: {line: StocktakeDifferenceLineDto}) {
  return (
    <Stack>
      <Typography variant="body2">{DIFFERENCE_RESOLUTION_LABELS[line.resolution]}</Typography>
      {line.missingFromDocument && (
        <Typography variant="caption" color="error.dark">
          нет в документе — будет списано
        </Typography>
      )}
      {line.currentNodePath && (
        <Typography variant="caption" color="text.secondary">
          сейчас в {formatStoragePlaceNodeName(line.currentNodePath)}
        </Typography>
      )}
    </Stack>
  );
}

const MISSING_ROW_SX = {backgroundColor: "error.light", opacity: 0.9};

interface StocktakeDifferencesDialogProps {
  open: boolean;
  stocktake: StocktakeDto;
  onClose: () => void;
  onFinished: (updated: StocktakeDto) => void;
}

function StocktakeDifferencesDialog({
  open,
  stocktake,
  onClose,
  onFinished,
}: StocktakeDifferencesDialogProps) {
  const canEdit = useHasPermission(["stocktakes.edit", "stocktakes.edit_assigned"]);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const {data, isLoading, isError, error} = useQuery({
    ...stocktakesGetDifferencesOptions({path: {id: stocktake.id}}),
    enabled: open,
    gcTime: 0,
    meta: {suppressGlobalError: true},
  });

  const finishMutation = useMutation({
    ...stocktakesFinishMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: onFinished,
  });

  const canFinish =
    canEdit &&
    stocktake.status === "inProgress" &&
    !!data &&
    data.problems.length === 0 &&
    !finishMutation.isPending;

  useBackClosable(open, onClose);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth fullScreen={isMobile}>
      <DialogTitle>Расхождения</DialogTitle>
      <DialogContent dividers sx={{px: {xs: 1.5, sm: 3}}}>
        {isLoading ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
            <CircularProgress />
          </Box>
        ) : isError ? (
          <Alert severity="error">{extractErrorMessage(error)}</Alert>
        ) : !data ? null : (
          <Stack spacing={2}>
            <Stack direction="row" spacing={1} sx={{flexWrap: "wrap", gap: 1}}>
              <Chip
                label={`Излишки: ${data.totalSurplusQuantity}`}
                color={data.totalSurplusQuantity > 0 ? "success" : "default"}
                size="small"
              />
              <Chip
                label={`Недостачи: ${data.totalShortageQuantity}`}
                color={data.totalShortageQuantity > 0 ? "error" : "default"}
                size="small"
              />
              <Chip label={`Перемещений: ${data.totalRelocations}`} size="small" />
            </Stack>

            {data.problems.length > 0 && (
              <Alert severity="error">
                <Typography variant="subtitle2">Провести нельзя:</Typography>
                {data.problems.map((p, i) => (
                  <Typography key={i} variant="body2">
                    {p.message}
                  </Typography>
                ))}
              </Alert>
            )}

            {!data.hasDifferences && (
              <Alert severity="success">Расхождений нет — остатки совпадают с подсчётом.</Alert>
            )}

            {data.nodes.map((node) => (
              <Box key={node.storagePlaceNodeId}>
                <Typography variant="subtitle2" sx={{mb: 0.5}}>
                  {formatStoragePlaceNodeName(node.nodePath)}
                </Typography>
                {isMobile ? (
                  <Stack spacing={1}>
                    {node.lines.map((line, i) => (
                      <Paper
                        key={lineKey(line, i)}
                        variant="outlined"
                        sx={{p: 1.5, ...(line.missingFromDocument ? MISSING_ROW_SX : {})}}
                      >
                        <Stack spacing={1}>
                          <StocktakeItemName
                            catalogItemId={line.catalogItemId}
                            catalogItemName={line.catalogItemName}
                            inventoryNumber={line.inventoryNumber}
                          />
                          <Stack direction="row" spacing={3}>
                            {(
                              [
                                ["Ожидается", line.expected, undefined],
                                ["Посчитано", line.counted, undefined],
                                ["Δ", formatDelta(line.delta), deltaColor(line.delta)],
                              ] as const
                            ).map(([label, value, color]) => (
                              <Box key={label}>
                                <Typography
                                  variant="caption"
                                  color="text.secondary"
                                  component="div"
                                >
                                  {label}
                                </Typography>
                                <Typography variant="body2" sx={{color}}>
                                  {value}
                                </Typography>
                              </Box>
                            ))}
                          </Stack>
                          <ResolutionText line={line} />
                        </Stack>
                      </Paper>
                    ))}
                  </Stack>
                ) : (
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Товар</TableCell>
                        <TableCell align="right">Ожидается</TableCell>
                        <TableCell align="right">Посчитано</TableCell>
                        <TableCell align="right">Δ</TableCell>
                        <TableCell>Что будет сделано</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {node.lines.map((line, i) => (
                        <TableRow
                          key={lineKey(line, i)}
                          sx={line.missingFromDocument ? MISSING_ROW_SX : undefined}
                        >
                          <TableCell>
                            <StocktakeItemName
                              catalogItemId={line.catalogItemId}
                              catalogItemName={line.catalogItemName}
                              inventoryNumber={line.inventoryNumber}
                            />
                          </TableCell>
                          <TableCell align="right">{line.expected}</TableCell>
                          <TableCell align="right">{line.counted}</TableCell>
                          <TableCell align="right" sx={{color: deltaColor(line.delta)}}>
                            {formatDelta(line.delta)}
                          </TableCell>
                          <TableCell>
                            <ResolutionText line={line} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </Box>
            ))}

            {finishMutation.isError && (
              <Alert severity="error">{extractErrorMessage(finishMutation.error)}</Alert>
            )}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Закрыть</Button>
        {stocktake.status === "inProgress" && (
          <Button
            variant="contained"
            color="success"
            disabled={!canFinish}
            loading={finishMutation.isPending}
            onClick={() => finishMutation.mutate({path: {id: stocktake.id}})}
          >
            Завершить и применить
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

export default StocktakeDifferencesDialog;
