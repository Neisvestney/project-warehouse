import {useCallback, useState} from "react";
import {
  Button,
  Chip,
  IconButton,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {enqueueSnackbar} from "notistack";
import {
  marketplaceAutoMapRulesDeleteRuleMutation,
  marketplaceAutoMapRulesGetRulesOptions,
  marketplaceAutoMapRulesGetRulesQueryKey,
  marketplaceAutoMapRulesUpdateRuleMutation,
} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {useEditLock} from "@/hooks/useEditLock";
import {useHasPermission} from "@/hooks/usePermission";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageTitle from "@/components/PageTitle.tsx";
import ConfirmDialog from "@/components/ConfirmDialog";
import EditLockBanner from "@/components/EditLockBanner";
import PageGenericHeader from "@/components/PageGenericHeader";
import SearchInput from "@/components/SearchInput";
import StaleDataBanner from "@/components/StaleDataBanner";
import TableInfoBar from "@/components/TableInfoBar";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {extractErrorMessage} from "@/utils/errorUtils";
import AutoMapRuleDialog from "./AutoMapRuleDialog";
import AutoMapCardsDialog from "../../components/AutoMapCardsDialog";
import {
  CARD_FIELD_LABELS,
  RULE_OPERATOR_LABELS,
} from "@/components/marketplace/marketplaceUtils.ts";
import type {MarketplaceAutoMapRuleDto} from "@/api/types.gen";

/** The rules are watched as one set, and the backend keys the event by an empty guid. */
const RULES_ENTITY_ID = "00000000-0000-0000-0000-000000000000";

function AutoMapRulesPage() {
  const canEdit = useHasPermission("integrations.map");
  const queryClient = useQueryClient();

  const [editing, setEditing] = useState<MarketplaceAutoMapRuleDto | null>(null);
  const [isDialogOpen, setDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState<MarketplaceAutoMapRuleDto | null>(null);
  const [isAutoMapOpen, setAutoMapOpen] = useState(false);

  // Claiming the set on a plain read would lock colleagues out of a page most people only look at, so
  // the claim waits for intent to edit and then sticks for the rest of the visit.
  const [hasEditIntent, setEditIntent] = useState(false);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {data, isLoading, isFetching, dataUpdatedAt} = useQuery(
    marketplaceAutoMapRulesGetRulesOptions({query: {searchString: searchString || undefined}}),
  );

  const invalidate = useCallback(
    () => queryClient.invalidateQueries({queryKey: marketplaceAutoMapRulesGetRulesQueryKey()}),
    [queryClient],
  );

  const refresh = useCallback(() => void invalidate(), [invalidate]);

  const lock = useEditLock("marketplaceAutoMapRules", RULES_ENTITY_ID, {
    isDirty: isDialogOpen,
    dataUpdatedAt,
    isFetching,
    isLoading,
    onRefresh: refresh,
    enabled: canEdit && hasEditIntent,
  });

  const toggle = useMutation({
    ...marketplaceAutoMapRulesUpdateRuleMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: invalidate,
    onError: (err) => enqueueSnackbar(extractErrorMessage(err), {variant: "error"}),
  });

  const remove = useMutation({
    ...marketplaceAutoMapRulesDeleteRuleMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await invalidate();
      setDeleting(null);
      enqueueSnackbar("Правило удалено", {variant: "success"});
    },
    onError: (err) => enqueueSnackbar(extractErrorMessage(err), {variant: "error"}),
  });

  const openDialog = (rule: MarketplaceAutoMapRuleDto | null) => {
    setEditIntent(true);
    setEditing(rule);
    setDialogOpen(true);
  };

  const confirmDelete = (rule: MarketplaceAutoMapRuleDto) => {
    setEditIntent(true);
    setDeleting(rule);
  };

  const setEnabled = (rule: MarketplaceAutoMapRuleDto, isEnabled: boolean) => {
    setEditIntent(true);
    toggle.mutate({
      path: {id: rule.id},
      body: {
        field: rule.field,
        operator: rule.operator,
        value: rule.value,
        catalogItemId: rule.catalogItemId,
        priority: rule.priority,
        isEnabled,
      },
    });
  };

  return (
    <Stack spacing={2}>
      <PageTitle title="Правила автосопоставления" />
      <AppBreadcrumbs
        path={[{name: "Маркетплейсы", link: "/marketplaces"}, {name: "Правила автосопоставления"}]}
        viewersOf={{entityType: "marketplaceAutoMapRules", entityId: RULES_ENTITY_ID}}
      />
      <PageGenericHeader
        title="Правила автосопоставления"
        actions={
          canEdit && (
            <Stack direction="row" spacing={1}>
              <Button
                variant="outlined"
                startIcon={<AutoFixHighIcon />}
                size="small"
                onClick={() => setAutoMapOpen(true)}
              >
                Сопоставить карточки
              </Button>
              <Button
                variant="outlined"
                endIcon={<AddIcon />}
                size="small"
                onClick={() => openDialog(null)}
              >
                Добавить правило
              </Button>
            </Stack>
          )
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <EditLockBanner heldBy={lock.heldBy} />
      <StaleDataBanner
        isStale={!lock.heldBy && lock.isStale}
        staleBy={lock.staleBy}
        onRefresh={lock.refresh}
        onDismiss={lock.dismissStale}
      />
      <Typography variant="body2" color="text.secondary">
        Правила общие для всех магазинов и применяются к несопоставленным карточкам раньше подбора
        по артикулу и штрихкоду. Побеждает первое подошедшее правило.
      </Typography>
      <TableInfoBar
        loading={isLoading}
        stats={[
          {
            key: "count",
            label: searchString ? "Найдено:" : "Всего правил:",
            value: data?.length ?? 0,
          },
        ]}
      />
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell width={100}>Приоритет</TableCell>
              <TableCell>Поле</TableCell>
              <TableCell>Условие</TableCell>
              <TableCell>Значение</TableCell>
              <TableCell>Товар каталога</TableCell>
              <TableCell width={90}>Активно</TableCell>
              <TableCell width={100} />
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={7} />
            ) : data?.length === 0 ? (
              <TableRowEmpty
                colSpan={7}
                message={searchString ? "Ничего не найдено" : "Правил пока нет"}
              />
            ) : (
              data?.map((rule) => (
                <TableRow
                  key={rule.id}
                  hover
                  sx={{opacity: isFetching && !isLoading ? 0.5 : 1, transition: "opacity 0.2s"}}
                >
                  <TableCell>{rule.priority}</TableCell>
                  <TableCell>{CARD_FIELD_LABELS[rule.field]}</TableCell>
                  <TableCell>{RULE_OPERATOR_LABELS[rule.operator]}</TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                      {rule.value}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                      <span>
                        {rule.catalogItemFullName}
                        <Typography component="span" variant="body2" color="text.secondary">
                          {` (${rule.catalogItemArticle})`}
                        </Typography>
                      </span>
                      {rule.isTargetArchived && (
                        <Tooltip title="Товар в архиве — правило не применяется">
                          <Chip label="Архив" color="warning" size="small" />
                        </Tooltip>
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Switch
                      size="small"
                      checked={rule.isEnabled}
                      disabled={!canEdit || toggle.isPending}
                      onChange={(e) => setEnabled(rule, e.target.checked)}
                    />
                  </TableCell>
                  <TableCell align="right">
                    {canEdit && (
                      <>
                        <IconButton size="small" onClick={() => openDialog(rule)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton size="small" onClick={() => confirmDelete(rule)}>
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

      <AutoMapRuleDialog
        open={isDialogOpen}
        rule={editing}
        onClose={() => setDialogOpen(false)}
        onSaved={invalidate}
      />

      <AutoMapCardsDialog open={isAutoMapOpen} onClose={() => setAutoMapOpen(false)} />

      <ConfirmDialog
        open={!!deleting}
        onClose={() => setDeleting(null)}
        title="Удалить правило?"
        confirmText="Удалить"
        confirmColor="error"
        isPending={remove.isPending}
        onConfirm={() => deleting && remove.mutate({path: {id: deleting.id}})}
      >
        <Typography variant="body2">
          Уже сопоставленные этим правилом карточки сохранят свою привязку.
        </Typography>
      </ConfirmDialog>
    </Stack>
  );
}

export default AutoMapRulesPage;
