import {useEffect, useRef, useState} from "react";
import {
  Alert,
  Box,
  CircularProgress,
  Divider,
  ListItemIcon,
  ListItemText,
  MenuItem,
  MenuList,
  Popover,
  type PopoverActions,
  TextField,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RemoveIcon from "@mui/icons-material/Remove";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import type {TagBatchOperation} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage} from "@/utils/errorUtils";
import {
  createDocumentTag,
  documentTagsQueryOptions,
  type DocumentTag,
  type TagPickerKind,
} from "./documentTags";

export interface BulkTagsSelection {
  ids: string[];
  position: {top: number; left: number};
}

interface BulkTagsMenuProps {
  kind: TagPickerKind;
  selection: BulkTagsSelection | null;
  isPending: boolean;
  summary: string;
  error: string | null;
  onClose: () => void;
  onConfirm: (tag: DocumentTag, operation: TagBatchOperation) => void;
}

export default function BulkTagsMenu(props: BulkTagsMenuProps) {
  const actions = useRef<PopoverActions>(null);
  const [creating, setCreating] = useState(false);
  const [initialPosition, setInitialPosition] = useState<BulkTagsSelection["position"] | null>(
    null,
  );
  const [shown, release] = useRetainedValue(props.selection);
  const [shownSummary, releaseSummary] = useRetainedValue(props.selection ? props.summary : null);
  return (
    <Popover
      action={actions}
      open={props.selection != null}
      anchorReference="anchorPosition"
      anchorPosition={initialPosition ?? shown?.position}
      onClose={props.isPending || creating ? undefined : props.onClose}
      slotProps={{
        transition: {
          onEnter: (element) => {
            if (!shown) return;
            setInitialPosition({
              top: shown.position.top - element.offsetHeight / 2,
              left: shown.position.left - element.offsetWidth / 2,
            });
          },
          onExited: () => {
            setInitialPosition(null);
            release();
            releaseSummary();
          },
        },
        paper: {
          sx: {
            width: 320,
            maxWidth: "calc(100% - 32px)",
            pointerEvents: props.selection ? undefined : "none",
          },
        },
      }}
    >
      {shown && (
        <BulkTagsMenuContent
          {...props}
          summary={shownSummary ?? ""}
          onCreatingChange={setCreating}
          onResize={() => actions.current?.updatePosition()}
        />
      )}
    </Popover>
  );
}

function BulkTagsMenuContent({
  kind,
  selection,
  isPending,
  summary,
  error,
  onClose,
  onConfirm,
  onCreatingChange,
  onResize,
}: BulkTagsMenuProps & {onCreatingChange: (value: boolean) => void; onResize: () => void}) {
  const content = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const observer = new ResizeObserver(onResize);
    if (content.current) observer.observe(content.current);
    return () => observer.disconnect();
  }, [onResize]);
  const [operation, setOperation] = useState<TagBatchOperation | null>(null);
  const [search, setSearch] = useState("");
  const queryClient = useQueryClient();
  const options = documentTagsQueryOptions(kind);
  const tags = useQuery({
    ...options,
    enabled: selection != null && operation != null,
    meta: {suppressGlobalError: true},
  });
  const create = useMutation({
    mutationFn: (name: string) => createDocumentTag(kind, name),
    meta: {suppressGlobalError: true},
    onMutate: () => onCreatingChange(true),
    onSettled: () => onCreatingChange(false),
    onSuccess: (tag) => {
      queryClient.setQueryData(options.queryKey, (current: DocumentTag[] | undefined) => [
        ...(current ?? []).filter((item) => item.id !== tag.id),
        tag,
      ]);
      onConfirm(tag, "add");
    },
  });
  const pending = isPending || create.isPending;
  const loading = pending || (operation != null && tags.isPending);
  useBackClosable(selection != null && !pending, onClose);
  const name = search.trim();
  const normalized = name.toLocaleLowerCase("ru-RU");
  const results = (tags.data ?? []).filter((tag) =>
    tag.name.toLocaleLowerCase("ru-RU").includes(normalized),
  );
  const canCreate =
    operation === "add" &&
    name &&
    tags.isSuccess &&
    !tags.isFetching &&
    !tags.data.some((tag) => tag.name.toLocaleLowerCase("ru-RU") === normalized);
  const message =
    error ||
    (create.isError
      ? extractErrorMessage(create.error)
      : tags.isError
        ? extractErrorMessage(tags.error)
        : null);

  return (
    <Box ref={content} aria-busy={loading} sx={{position: "relative"}}>
      <Typography variant="body2" color="text.secondary" sx={{px: 2, pt: 1.5, pb: 1}}>
        {summary}
      </Typography>
      {operation == null ? (
        <MenuList autoFocusItem aria-label="Действия с метками">
          {(["add", "remove"] as const).map((value) => (
            <MenuItem key={value} onClick={() => setOperation(value)}>
              <ListItemIcon>{value === "add" ? <AddIcon /> : <RemoveIcon />}</ListItemIcon>
              <ListItemText>{value === "add" ? "Добавить" : "Снять"}</ListItemText>
              <ChevronRightIcon />
            </MenuItem>
          ))}
        </MenuList>
      ) : (
        <>
          <MenuList aria-label="Навигация по меткам">
            <MenuItem
              disabled={pending}
              onClick={() => {
                setOperation(null);
                setSearch("");
                create.reset();
              }}
            >
              <ListItemIcon>
                <ArrowBackIcon />
              </ListItemIcon>
              <ListItemText>{operation === "add" ? "Добавить метку" : "Снять метку"}</ListItemText>
            </MenuItem>
          </MenuList>
          <Box sx={{px: 2, pb: 1}}>
            <TextField
              autoFocus
              fullWidth
              size="small"
              label="Поиск метки"
              value={search}
              disabled={pending}
              onChange={(event) => {
                setSearch(event.target.value);
                create.reset();
              }}
            />
          </Box>
          <Divider />
          <MenuList aria-label="Метки" sx={{maxHeight: 280, overflowY: "auto"}}>
            {results.map((tag) => (
              <MenuItem
                key={tag.id}
                disabled={pending}
                onClick={() => onConfirm(tag, operation)}
                sx={{whiteSpace: "normal", overflowWrap: "anywhere"}}
              >
                {tag.name}
              </MenuItem>
            ))}
            {canCreate && (
              <MenuItem
                disabled={pending}
                onClick={() => create.mutate(name)}
                sx={{whiteSpace: "normal", overflowWrap: "anywhere"}}
              >
                <ListItemIcon>
                  <AddIcon />
                </ListItemIcon>
                <ListItemText>Создать и добавить «{name}»</ListItemText>
              </MenuItem>
            )}
          </MenuList>
          {tags.isSuccess && !results.length && !canCreate && (
            <Typography color="text.secondary" variant="body2" sx={{px: 2, pb: 2}}>
              Метки не найдены
            </Typography>
          )}
        </>
      )}
      {message && (
        <Alert severity="error" sx={{m: 1}}>
          {message}
        </Alert>
      )}
      {loading && (
        <Box
          role="status"
          aria-label="Загрузка"
          sx={{
            position: "absolute",
            inset: 0,
            zIndex: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: "progress",
            bgcolor: (theme) =>
              `color-mix(in srgb, ${theme.vars!.palette.background.paper}, transparent 25%)`,
          }}
        >
          <CircularProgress size={22} />
        </Box>
      )}
    </Box>
  );
}
