import {useState} from "react";
import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Divider,
  Drawer,
  FormControl,
  IconButton,
  InputLabel,
  ListItemText,
  MenuItem,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import DeleteIcon from "@mui/icons-material/Delete";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import StarIcon from "@mui/icons-material/Star";
import type {StockMovementDirection, StockMovementReportPresetDto} from "@/api/types.gen";
import ReceiptTagsFilter from "@/components/receipts/ReceiptTagsFilter";
import {type DraftMetric, newMetricKey} from "./metricDraft";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useHasPermission} from "@/hooks/usePermission";
import {
  MAX_METRICS,
  STOCK_MOVEMENT_ACTIONS,
  STOCK_MOVEMENT_DIRECTIONS,
} from "./stockMovementsConstants";

const DRAWER_WIDTH = 520;

interface MetricRowProps {
  id: string;
  metric: DraftMetric;
  canViewReceipts: boolean;
  onChange: (metric: DraftMetric) => void;
  onRemove: () => void;
}

function MetricRow({id, metric, canViewReceipts, onChange, onRemove}: MetricRowProps) {
  const {attributes, listeners, setNodeRef, transform, transition, isDragging} = useSortable({id});

  return (
    <Stack
      ref={setNodeRef}
      spacing={1}
      sx={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.4 : 1,
        p: 1,
        border: 1,
        borderColor: "divider",
        borderRadius: 1,
        bgcolor: "background.paper",
      }}
    >
      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
        <Box
          {...attributes}
          {...listeners}
          sx={{display: "flex", cursor: "grab", touchAction: "none", color: "text.disabled"}}
        >
          <DragIndicatorIcon fontSize="small" />
        </Box>
        <TextField
          size="small"
          label="Название"
          fullWidth
          value={metric.name}
          onChange={(e) => onChange({...metric, name: e.target.value})}
        />
        <IconButton size="small" onClick={onRemove} title="Удалить метрику">
          <DeleteIcon fontSize="small" />
        </IconButton>
      </Stack>

      <Stack direction="row" spacing={1} sx={{flexWrap: "wrap", gap: 1}}>
        <FormControl size="small" sx={{minWidth: 200, flex: 1}}>
          <InputLabel>Операции</InputLabel>
          <Select
            multiple
            label="Операции"
            value={metric.actions ?? []}
            onChange={(e) => onChange({...metric, actions: e.target.value as string[]})}
            renderValue={(selected) =>
              selected.length === 1
                ? STOCK_MOVEMENT_ACTIONS.find((a) => a.value === selected[0])?.label
                : `${selected.length} операций`
            }
          >
            {STOCK_MOVEMENT_ACTIONS.map(({value, label}) => (
              <MenuItem key={value} value={value}>
                <Checkbox size="small" checked={(metric.actions ?? []).includes(value)} />
                <ListItemText primary={label} />
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{minWidth: 180, flex: 1}}>
          <InputLabel>Направление</InputLabel>
          <Select
            multiple
            label="Направление"
            value={metric.directions ?? []}
            onChange={(e) =>
              onChange({...metric, directions: e.target.value as StockMovementDirection[]})
            }
            renderValue={(selected) =>
              selected.length === 1
                ? STOCK_MOVEMENT_DIRECTIONS.find((d) => d.value === selected[0])?.label
                : `${selected.length} направления`
            }
          >
            {STOCK_MOVEMENT_DIRECTIONS.map(({value, label}) => (
              <MenuItem key={value} value={value}>
                <Checkbox size="small" checked={(metric.directions ?? []).includes(value)} />
                <ListItemText primary={label} />
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>

      {canViewReceipts && (
        <ReceiptTagsFilter
          value={metric.receiptTagIds ?? []}
          onChange={(receiptTagIds) => onChange({...metric, receiptTagIds})}
          label="Теги приёмки"
        />
      )}
    </Stack>
  );
}

interface MetricsEditorDrawerProps {
  open: boolean;
  onClose: () => void;
  preset: StockMovementReportPresetDto | undefined;
  /** Unsaved name driving the preset chip right now. */
  name: string;
  /** Unsaved metrics driving the table right now, so the drawer opens on what is on screen. */
  draft: DraftMetric[];
  onNameChange: (name: string) => void;
  onDraftChange: (metrics: DraftMetric[]) => void;
  onSave: (makeDefault: boolean) => void;
  onSaveAs: (name: string, metrics: DraftMetric[]) => void;
  onDelete: () => void;
  isSaving: boolean;
  canDelete: boolean;
  error: string | null;
}

function MetricsEditorDrawer({
  open,
  onClose,
  preset,
  name,
  draft,
  onNameChange,
  onDraftChange,
  onSave,
  onSaveAs,
  onDelete,
  isSaving,
  canDelete,
  error,
}: MetricsEditorDrawerProps) {
  const canViewReceipts = useHasPermission(["receipts.view", "receipts.view_assigned"]);
  const [newName, setNewName] = useState("");
  const [saveAsOpen, setSaveAsOpen] = useState(false);

  useBackClosable(open, onClose);

  const sensors = useSensors(
    useSensor(PointerSensor, {activationConstraint: {distance: 5}}),
    useSensor(KeyboardSensor, {coordinateGetter: sortableKeyboardCoordinates}),
  );

  const ids = draft.map((metric) => metric.key);

  const handleDragEnd = ({active, over}: DragEndEvent) => {
    if (!over || active.id === over.id) return;

    const from = ids.indexOf(String(active.id));
    const to = ids.indexOf(String(over.id));
    if (from < 0 || to < 0) return;

    onDraftChange(arrayMove(draft, from, to));
  };

  const replaceAt = (index: number, metric: DraftMetric) =>
    onDraftChange(draft.map((m, i) => (i === index ? metric : m)));

  const isValid =
    name.trim().length > 0 && draft.length > 0 && draft.every((m) => m.name.trim().length > 0);

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      slotProps={{paper: {sx: {width: DRAWER_WIDTH, maxWidth: "calc(100vw - 10px)"}}}}
    >
      <Stack sx={{height: "100%"}}>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", p: 2, borderBottom: 1, borderColor: "divider"}}
        >
          <Typography variant="h6" sx={{flex: 1}} noWrap>
            Пресет
          </Typography>
          <IconButton onClick={onClose} title="Закрыть">
            <CloseIcon />
          </IconButton>
        </Stack>

        <Stack spacing={1.5} sx={{flex: 1, overflow: "auto", p: 2}}>
          <TextField
            size="small"
            label="Название пресета"
            value={name}
            disabled={!preset}
            error={name.trim().length === 0}
            onChange={(e) => onNameChange(e.target.value)}
          />

          <Alert severity="info">
            Пресет общий: изменения увидят все, у кого есть доступ к отчёту.
            {preset?.updatedByName && (
              <Typography variant="caption" component="div" sx={{color: "text.secondary"}}>
                Последним правил {preset.updatedByName},{" "}
                {new Date(preset.updatedAt).toLocaleString("ru-RU")}
              </Typography>
            )}
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
          >
            <SortableContext items={ids} strategy={verticalListSortingStrategy}>
              <Stack spacing={1}>
                {draft.map((metric, index) => (
                  <MetricRow
                    key={ids[index]}
                    id={ids[index]}
                    metric={metric}
                    canViewReceipts={canViewReceipts}
                    onChange={(next) => replaceAt(index, next)}
                    onRemove={() => onDraftChange(draft.filter((_, i) => i !== index))}
                  />
                ))}
              </Stack>
            </SortableContext>
          </DndContext>

          <Button
            size="small"
            startIcon={<AddIcon />}
            sx={{alignSelf: "flex-start"}}
            disabled={draft.length >= MAX_METRICS}
            onClick={() => onDraftChange([...draft, {name: "Новая метрика", key: newMetricKey()}])}
          >
            Метрика
          </Button>

          <Typography variant="caption" sx={{color: "text.secondary"}}>
            «Итого движение» и «Остаток» закреплены и всегда идут последними в группе товара.
            Метрика без единого условия считает все движения.
          </Typography>

          {saveAsOpen && (
            <>
              <Divider />
              <TextField
                size="small"
                label="Имя нового пресета"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                autoFocus
              />
            </>
          )}
        </Stack>

        <Stack
          direction="row"
          spacing={1}
          sx={{p: 2, borderTop: 1, borderColor: "divider", flexWrap: "wrap", gap: 1}}
        >
          {saveAsOpen ? (
            <>
              <Button
                variant="contained"
                disabled={!isValid || newName.trim().length === 0 || isSaving}
                onClick={() => {
                  onSaveAs(newName.trim(), draft);
                  setSaveAsOpen(false);
                  setNewName("");
                }}
              >
                Создать
              </Button>
              <Button onClick={() => setSaveAsOpen(false)}>Отмена</Button>
            </>
          ) : (
            <>
              <Button
                variant="contained"
                disabled={!isValid || isSaving || !preset}
                onClick={() => onSave(false)}
              >
                Сохранить
              </Button>
              <Button disabled={!isValid || isSaving} onClick={() => setSaveAsOpen(true)}>
                Сохранить как новый
              </Button>
              {preset && !preset.isDefault && (
                <Tooltip title="Открывать этот пресет по умолчанию">
                  <span>
                    <IconButton
                      disabled={!isValid || isSaving}
                      onClick={() => onSave(true)}
                      title="Сделать пресетом по умолчанию"
                    >
                      <StarIcon />
                    </IconButton>
                  </span>
                </Tooltip>
              )}
              <IconButton
                sx={{ml: "auto"}}
                disabled={!canDelete || isSaving}
                onClick={onDelete}
                title={canDelete ? "Удалить пресет" : "Последний пресет удалить нельзя"}
              >
                <DeleteIcon />
              </IconButton>
            </>
          )}
        </Stack>
      </Stack>
    </Drawer>
  );
}

export default MetricsEditorDrawer;
