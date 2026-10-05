import {useState} from "react";
import {useParams} from "react-router";
import {
  Button,
  Chip,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {organizationsGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import {useHasPermission} from "@/hooks/usePermission";
import {useEntityWatch} from "@/hooks/useEntityWatch";
import PageLoader from "@/components/PageLoader";
import PageTitle from "@/components/PageTitle.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import InfoRow from "@/components/InfoRow";
import UserChip from "@/components/shared/UserChip";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import {
  MARKETPLACE_TYPE_COLORS,
  MARKETPLACE_TYPE_LABELS,
  formatDateTime,
} from "@/components/marketplace/marketplaceUtils.ts";
import {ORGANIZATION_KIND_LABELS} from "@/features/organizations/organizationKinds";
import OrganizationFormDialog from "../../components/OrganizationFormDialog";
import type {OrganizationFormTarget} from "../../components/OrganizationFormDialog";
import DeleteOrganizationDialog from "./DeleteOrganizationDialog";

function OrganizationPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();
  const canEdit = useHasPermission("organizations.edit");
  const canOpenAccounts = useHasPermission("integrations.view");

  const [formTarget, setFormTarget] = useState<OrganizationFormTarget | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);

  useEntityWatch("organization", id, () => {
    void queryClient.invalidateQueries({
      queryKey: byOperation("organizationsGetById", {path: {id: id!}}),
    });
  });

  const {
    data: organization,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...organizationsGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  if (isLoading) return <PageLoader inline />;
  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!organization) return <NotFound />;

  return (
    <Stack spacing={2}>
      <PageTitle title={organization.name} />
      <AppBreadcrumbs
        path={[{name: "Организации", link: "/organizations"}, {name: organization.name}]}
        viewersOf={{entityType: "organization", entityId: organization.id}}
      />
      <PageGenericHeader
        title={organization.name}
        actions={
          canEdit && (
            <>
              <Button
                variant="outlined"
                startIcon={<EditIcon />}
                onClick={() => setFormTarget(organization)}
              >
                Изменить
              </Button>
              <Button
                variant="outlined"
                color="error"
                startIcon={<DeleteIcon />}
                onClick={() => setDeleteOpen(true)}
              >
                Удалить
              </Button>
            </>
          )
        }
      />

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          <Typography variant="subtitle2" color="text.secondary">
            Реквизиты
          </Typography>
          <InfoRow label="Полное наименование" value={organization.legalName ?? "—"} />
          <InfoRow
            label="Тип"
            value={
              <Chip
                label={ORGANIZATION_KIND_LABELS[organization.kind]}
                size="small"
                variant="outlined"
              />
            }
          />
          <InfoRow label="ИНН" value={organization.inn} />
          <InfoRow label="КПП" value={organization.kpp ?? "—"} />
          <InfoRow label="ОГРН / ОГРНИП" value={organization.ogrn ?? "—"} />
          <InfoRow
            label="Форма собственности"
            value={
              <Chip label={organization.ownershipForm ?? "—"} size="small" variant="outlined" />
            }
          />
          <InfoRow label="Создана" value={formatDateTime(organization.createdAt)} />
          <InfoRow
            label="Кем создана"
            value={
              organization.createdByName ? (
                <UserChip userId={organization.createdById} name={organization.createdByName} />
              ) : (
                "синхронизацией"
              )
            }
          />
        </Stack>
      </Paper>

      <Paper>
        <Stack spacing={1} sx={{p: 3, pb: 1}}>
          <Typography variant="subtitle2" color="text.secondary">
            Аккаунты маркетплейсов
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Синхронизация привязывает аккаунт к организации с тем же ИНН. Привязку вручную меняют на
            странице аккаунта.
          </Typography>
        </Stack>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Магазин</TableCell>
              <TableCell>Площадка</TableCell>
              <TableCell>ИНН аккаунта</TableCell>
              <TableCell>Привязка</TableCell>
              <TableCell>Активен</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {organization.accounts.length === 0 ? (
              <TableRowEmpty colSpan={5} message="Аккаунтов нет" />
            ) : (
              organization.accounts.map((account) => {
                // an array, not a fragment: LinkTableRow puts its link overlay into the first child
                const cells = [
                  <TableCell key="name">{account.name}</TableCell>,
                  <TableCell key="type">
                    <Chip
                      size="small"
                      label={MARKETPLACE_TYPE_LABELS[account.type]}
                      color={MARKETPLACE_TYPE_COLORS[account.type]}
                    />
                  </TableCell>,
                  <TableCell key="inn" sx={{fontFamily: "monospace"}}>
                    {account.inn && account.inn !== organization.inn ? (
                      <Tooltip title="ИНН аккаунта не совпадает с ИНН организации">
                        <Chip size="small" color="warning" label={account.inn} />
                      </Tooltip>
                    ) : (
                      (account.inn ?? "—")
                    )}
                  </TableCell>,
                  <TableCell key="link">
                    {account.isOrganizationLinkedManually ? "Вручную" : "По ИНН"}
                  </TableCell>,
                  <TableCell key="active">{account.isActive ? "Да" : "Нет"}</TableCell>,
                ];
                return canOpenAccounts ? (
                  <LinkTableRow
                    key={account.id}
                    to={`/marketplaces/${account.id}`}
                    ariaLabel={`Магазин ${account.name}`}
                  >
                    {cells}
                  </LinkTableRow>
                ) : (
                  <TableRow key={account.id}>{cells}</TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </Paper>

      <OrganizationFormDialog target={formTarget} onClose={() => setFormTarget(null)} />
      <DeleteOrganizationDialog
        open={deleteOpen}
        organizationId={organization.id}
        organizationName={organization.name}
        hasAccounts={organization.accounts.length > 0}
        onClose={() => setDeleteOpen(false)}
      />
    </Stack>
  );
}

export default OrganizationPage;
