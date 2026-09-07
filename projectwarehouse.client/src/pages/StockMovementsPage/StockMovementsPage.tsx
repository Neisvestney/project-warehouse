import {useMemo, useState} from "react";
import {Alert, Dialog, IconButton, Stack, Tooltip, Typography} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useSnackbar} from "notistack";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import QueryError from "@/components/QueryError";
import {extractErrorMessage} from "@/utils/errorUtils";
import MetricsEditorDrawer from "./MetricsEditorDrawer";
import {type DraftMetric, stripKeys, withKeys} from "./metricDraft";
import PresetBar from "./PresetBar";
import StockMovementsFilters from "./StockMovementsFilters";
import StockMovementsPivotTable from "./StockMovementsPivotTable";
import {MAX_METRICS} from "./stockMovementsConstants";
import {useCatalogItemsByIds} from "./useCatalogItemsByIds";
import {useStockMovementPresets} from "./useStockMovementPresets";
import {useStockMovementsFilters} from "./useStockMovementsFilters";
import {useStockMovementsPivot} from "./useStockMovementsPivot";

/** Beyond this the table stops being readable and starts being a scroll endurance test. */
const COLUMN_WARNING_THRESHOLD = 120;

function StockMovementsPage() {
  const filters = useStockMovementsFilters();
  const {filter, presetId, setPresetId, expanded, setExpanded} = filters;
  const {enqueueSnackbar} = useSnackbar();

  const presets = useStockMovementPresets(presetId);

  // The draft is what the table actually renders, so a metric can be tried before it is imposed on
  // everyone — the preset is shared and saving to preview would be a change for the whole team.
  // It carries the preset it was made against, so switching presets drops it without an effect.
  const [draft, setDraft] = useState<{
    presetId: string;
    name: string;
    metrics: DraftMetric[];
  } | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const activeId = presets.active?.id;
  const isDirty = draft !== null && draft.presetId === activeId;
  const draftMetrics = isDirty ? draft.metrics : withKeys(presets.active?.metrics ?? []);
  const metrics = stripKeys(draftMetrics);
  const name = isDirty ? draft.name : (presets.active?.name ?? "");

  const editDraft = (changes: {name?: string; metrics?: DraftMetric[]}) => {
    if (!activeId) return;
    setDraft({
      presetId: activeId,
      name: changes.name ?? name,
      metrics: (changes.metrics ?? draftMetrics).slice(0, MAX_METRICS),
    });
  };

  const selectPreset = (id: string) => {
    // A draft belongs to the preset it was made against; keeping it around means coming back to that
    // preset later silently resurrects edits the user walked away from.
    setDraft(null);
    setPresetId(id);
  };

  const knownItems = useCatalogItemsByIds(filter.catalogItemIds);
  const items = useMemo(
    () =>
      filter.catalogItemIds.map(
        (id) =>
          knownItems.get(id) ?? {
            id,
            type: "standard" as const,
            name: "…",
            fullName: "…",
            article: "",
            isArchived: false,
          },
      ),
    [filter.catalogItemIds, knownItems],
  );

  const pivot = useStockMovementsPivot(filter, metrics);
  const hasSelection = filter.catalogItemIds.length > 0;
  const columnCount = (items.length + 1) * (metrics.length + 2);

  const closeEditor = () => {
    setEditorOpen(false);
    setSaveError(null);
  };

  const handleSave = (makeDefault: boolean) => {
    const preset = presets.active;
    if (!preset) return;

    presets.update.mutate(
      {
        path: {id: preset.id},
        body: {
          name: name.trim(),
          isDefault: makeDefault || preset.isDefault,
          metrics,
          version: preset.version,
        },
      },
      {
        onSuccess: () => {
          setDraft(null);
          closeEditor();
          enqueueSnackbar("Пресет сохранён", {variant: "success"});
        },
        onError: (error) => setSaveError(extractErrorMessage(error)),
      },
    );
  };

  const handleSaveAs = (newName: string, next: DraftMetric[]) => {
    presets.create.mutate(
      {body: {name: newName, isDefault: false, metrics: stripKeys(next)}},
      {
        onSuccess: (created) => {
          setDraft(null);
          setPresetId(created.id);
          closeEditor();
          enqueueSnackbar("Пресет создан", {variant: "success"});
        },
        onError: (error) => setSaveError(extractErrorMessage(error)),
      },
    );
  };

  const handleDelete = () => {
    const preset = presets.active;
    if (!preset) return;

    presets.remove.mutate(
      {path: {id: preset.id}},
      {
        onSuccess: () => {
          setPresetId(null);
          closeEditor();
          enqueueSnackbar("Пресет удалён", {variant: "success"});
        },
        onError: (error) => setSaveError(extractErrorMessage(error)),
      },
    );
  };

  // The dialog is not rendered while the query is failing, so without this the expanded page would show
  // an error with no way back to normal mode short of editing the URL.
  const showDialog = expanded && hasSelection && !pivot.error;

  const presetBar = (
    <PresetBar
      presets={presets.presets}
      activeId={presets.active?.id}
      isLoading={presets.isLoading}
      isDirty={isDirty}
      expanded={expanded}
      onSelect={selectPreset}
      onEdit={() => setEditorOpen(true)}
      onToggleExpanded={() => setExpanded(!expanded)}
    />
  );

  const table = (
    <StockMovementsPivotTable
      columns={items}
      metrics={metrics}
      rows={pivot.rows}
      isInfinite={pivot.isInfinite}
      isLoading={pivot.isLoading}
      isFetching={pivot.isFetching}
      isFetchingNextPage={pivot.isFetchingNextPage}
      hasNextPage={pivot.hasNextPage}
      onLoadMore={() => pivot.fetchNextPage()}
      fill={expanded}
    />
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Движения товаров"}]} />

      <PageGenericHeader
        title={
          // The applied zone hangs off a permanently rendered icon: as a line of its own it appeared
          // and vanished with every reload and shifted the page under the cursor.
          <>
            Движения товаров
            <Tooltip
              title={pivot.timeZoneId && `Сутки считаются по часовому поясу ${pivot.timeZoneId}`}
            >
              <InfoOutlinedIcon
                sx={{
                  fontSize: "0.7em",
                  verticalAlign: "middle",
                  ml: 0.5,
                  color: "primary.main",
                  opacity: pivot.timeZoneId ? 1 : 0,
                  pointerEvents: pivot.timeZoneId ? "auto" : "none",
                }}
              />
            </Tooltip>
          </>
        }
        refresh={
          <Tooltip title="Обновить">
            <IconButton color="inherit" onClick={() => pivot.refetch()}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
        }
      />

      <StockMovementsFilters {...filters} items={items} />

      {!showDialog && presetBar}

      {columnCount > COLUMN_WARNING_THRESHOLD && (
        <Alert severity="warning">
          {columnCount} столбцов — уберите позиции или метрики, иначе таблицу придётся листать вбок
          дольше, чем читать.
        </Alert>
      )}

      {!hasSelection ? (
        <Alert severity="info">
          Выберите хотя бы одну позицию каталога — из них строятся столбцы таблицы.
        </Alert>
      ) : pivot.error ? (
        <QueryError />
      ) : showDialog ? (
        // Esc and the focus trap come from the Dialog. `useBackClosable` cannot help here: it
        // marks a pushed history entry, and syncing `full` to the URL replaces that entry and
        // wipes the marker, leaving Back to reopen what was just closed.
        <Dialog fullScreen open onClose={() => setExpanded(false)}>
          <Stack sx={{height: "100%", p: 1, gap: 1}}>
            <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
              <Typography variant="subtitle1" sx={{fontWeight: 600}}>
                Движения товаров
              </Typography>
              <IconButton
                sx={{ml: "auto"}}
                onClick={() => setExpanded(false)}
                title="Свернуть (Esc)"
              >
                <CloseIcon />
              </IconButton>
            </Stack>
            {presetBar}
            {table}
          </Stack>
        </Dialog>
      ) : (
        table
      )}

      <MetricsEditorDrawer
        open={editorOpen}
        onClose={closeEditor}
        preset={presets.active}
        name={name}
        draft={draftMetrics}
        onNameChange={(next) => editDraft({name: next})}
        onDraftChange={(next) => editDraft({metrics: next})}
        onSave={handleSave}
        onSaveAs={handleSaveAs}
        onDelete={handleDelete}
        isSaving={presets.update.isPending || presets.create.isPending || presets.remove.isPending}
        canDelete={presets.presets.length > 1}
        error={saveError}
      />
    </Stack>
  );
}

export default StockMovementsPage;
