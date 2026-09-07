import {useCallback, useState} from "react";
import {
  Button,
  IconButton,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {enqueueSnackbar} from "notistack";
import {tagsDeleteMutation, tagsGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useEntityWatch} from "@/hooks/useEntityWatch";
import {useHasPermission} from "@/hooks/usePermission";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import ConfirmDialog from "@/components/ConfirmDialog";
import PageGenericHeader from "@/components/PageGenericHeader";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {byOperation} from "@/utils/queryKeys";
import {extractErrorMessage} from "@/utils/errorUtils";
import TagDialog from "./TagDialog";
import {ALL_TAG_KINDS, TAG_KIND_LABELS, TAG_KIND_OBJECT_LABELS} from "./tagKinds";
import type {TagDto, TagKind} from "@/api/types.gen";

/** The tag list is watched as one object, and the backend keys the event by an empty guid. */
const TAGS_ENTITY_ID = "00000000-0000-0000-0000-000000000000";

const DEFAULT_KIND: TagKind = "receipt";

function isTagKind(value: string | null): value is TagKind {
  return ALL_TAG_KINDS.includes(value as TagKind);
}

export default function TagsSettingsPage() {
  const canManage = useHasPermission("tags.manage");
  const queryClient = useQueryClient();

  const [kind, setKind] = useSyncedWithQueryState<TagKind>(
    "kind",
    (q) => (isTagKind(q) ? q : DEFAULT_KIND),
    (v) => (v === DEFAULT_KIND ? null : v),
  );

  const [editing, setEditing] = useState<TagDto | null>(null);
  const [isDialogOpen, setDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState<TagDto | null>(null);

  const {data, isLoading, isFetching} = useQuery(tagsGetAllOptions({query: {kind}}));

  // The per-module pickers read their own endpoints, so a rename here leaves their caches showing the
  // old name and a delete leaves a tag that 404s on save.
  const invalidate = useCallback(async () => {
    await Promise.all(
      ["tagsGetAll", "catalogGetTags", "receiptsGetTags"].map((operation) =>
        queryClient.invalidateQueries({queryKey: byOperation(operation)}),
      ),
    );
  }, [queryClient]);

  useEntityWatch("tags", TAGS_ENTITY_ID, () => void invalidate());

  useRealtimeEvent("entityChanged", (_event, payload) => {
    if (payload.entityType === "tags") void invalidate();
  });

  const remove = useMutation({
    ...tagsDeleteMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await invalidate();
      setDeleting(null);
      enqueueSnackbar("Тег удалён", {variant: "success"});
    },
    onError: (err) => enqueueSnackbar(extractErrorMessage(err), {variant: "error"}),
  });

  const openDialog = (tag: TagDto | null) => {
    setEditing(tag);
    setDialogOpen(true);
  };

  return (
    <Stack spacing={2} sx={{p: 2}}>
      <PageGenericHeader
        title="Теги"
        actions={
          canManage && (
            <Button
              variant="outlined"
              endIcon={<AddIcon />}
              size="small"
              onClick={() => openDialog(null)}
            >
              Создать тег
            </Button>
          )
        }
      />

      <Typography variant="body2" color="text.secondary">
        Теги общие для всех сотрудников. Переименование сохраняет все привязки, удаление снимает тег
        со всех объектов, но сами объекты остаются на месте.
      </Typography>

      <Paper>
        <Tabs value={kind} onChange={(_, v: TagKind) => setKind(v)}>
          {ALL_TAG_KINDS.map((k) => (
            <Tab key={k} value={k} label={TAG_KIND_LABELS[k]} />
          ))}
        </Tabs>
      </Paper>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Название</TableCell>
              <TableCell width={160}>Объектов</TableCell>
              <TableCell width={100} />
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={3} />
            ) : data?.length === 0 ? (
              <TableRowEmpty colSpan={3} message="Тегов пока нет" />
            ) : (
              data?.map((tag) => (
                <TableRow
                  key={tag.id}
                  hover
                  sx={{opacity: isFetching && !isLoading ? 0.5 : 1, transition: "opacity 0.2s"}}
                >
                  <TableCell>{tag.name}</TableCell>
                  <TableCell>{tag.usageCount}</TableCell>
                  <TableCell align="right">
                    {canManage && (
                      <>
                        <IconButton size="small" onClick={() => openDialog(tag)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton size="small" onClick={() => setDeleting(tag)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <TagDialog
        open={isDialogOpen}
        tag={editing}
        kind={kind}
        onClose={() => setDialogOpen(false)}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        title={`Удалить тег «${deleting?.name ?? ""}»?`}
        confirmText="Удалить"
        confirmColor="error"
        isPending={remove.isPending}
        onConfirm={() => deleting && remove.mutate({path: {id: deleting.id}})}
      >
        <Typography variant="body2">
          {deleting && deleting.usageCount > 0
            ? `Тег привязан к ${deleting.usageCount} ${TAG_KIND_OBJECT_LABELS[deleting.kind]} — привязка будет снята со всех. Сами объекты останутся без изменений.`
            : "К тегу ничего не привязано."}
        </Typography>
      </ConfirmDialog>
    </Stack>
  );
}
