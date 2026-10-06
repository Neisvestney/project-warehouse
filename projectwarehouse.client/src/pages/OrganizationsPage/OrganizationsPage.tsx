import {useState} from "react";
import {
  Button,
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {organizationsGetAllOptions} from "@/api/@tanstack/react-query.gen";
import type {OrganizationSortBy, OrganizationSummaryDto} from "@/api/types.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import {useSelectedItems} from "@/hooks/useSelectedItems";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import BulkBar from "@/components/BulkBar";
import SelectionTableCell from "@/components/SelectionTableCell";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import {ORGANIZATION_KIND_LABELS} from "@/features/organizations/organizationKinds";
import OrganizationFormDialog from "./components/OrganizationFormDialog";
import type {OrganizationFormTarget} from "./components/OrganizationFormDialog";
import {useOrganizationLabelsPrintAction} from "./components/useOrganizationLabelsPrintAction";

const SORT_COLUMNS: {key: OrganizationSortBy; label: string}[] = [
  {key: "name", label: "Название"},
  {key: "inn", label: "ИНН"},
  {key: "createdAt", label: "Создана"},
];

const COLUMN_COUNT = 6;

const getOrganizationId = (organization: OrganizationSummaryDto) => organization.id;

function OrganizationsPage() {
  const canEdit = useHasPermission("organizations.edit");
  const [formTarget, setFormTarget] = useState<OrganizationFormTarget | null>(null);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "name");

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString: searchString || undefined, sortBy, sortOrder},
    [searchString, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    organizationsGetAllOptions({query: fetchParams}),
  );

  const {selectedItems, isSelected, allPageSelected, somePageSelected, toggle, toggleAll, clear} =
    useSelectedItems(getOrganizationId, data?.items);

  const labelsAction = useOrganizationLabelsPrintAction();

  const selectionActions = selectedItems.length > 0 ? [labelsAction.getAction(selectedItems)] : [];

  const sortableHeader = (key: OrganizationSortBy, label: string) => (
    <TableCell sortDirection={sortBy === key ? sortOrder : false}>
      <TableSortLabel
        active={sortBy === key}
        direction={sortBy === key ? sortOrder : "asc"}
        onClick={() => handleSortClick(key)}
      >
        {label}
      </TableSortLabel>
    </TableCell>
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Организации"}]} />
      <PageGenericHeader
        title="Организации"
        refresh={
          <IconButton color="inherit" onClick={() => refetch()}>
            <RefreshIcon />
          </IconButton>
        }
        actions={
          canEdit && (
            <Button
              variant="outlined"
              endIcon={<AddIcon />}
              size="small"
              onClick={() => setFormTarget("new")}
            >
              Добавить
            </Button>
          )
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <BulkBar
        count={selectedItems.length}
        countLabel={{
          one: "организация выбрана",
          few: "организации выбрано",
          many: "организаций выбрано",
        }}
        onClear={clear}
        actions={selectionActions}
        info={[{key: "total", label: "Всего:", value: (data?.total ?? 0).toLocaleString("ru-RU")}]}
        infoLoading={isLoading}
      />
      {labelsAction.dialogs}
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
              <SelectionTableCell
                checked={allPageSelected}
                indeterminate={!allPageSelected && somePageSelected}
                onCheck={() => toggleAll()}
              />
              {sortableHeader("name", "Название")}
              {sortableHeader("inn", "ИНН")}
              <TableCell>Тип</TableCell>
              <TableCell>Полное наименование</TableCell>
              <TableCell>Аккаунты</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={COLUMN_COUNT} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={COLUMN_COUNT} message="Организаций нет" />
            ) : (
              data?.items.map((organization) => (
                <LinkTableRow
                  key={organization.id}
                  to={`/organizations/${organization.id}`}
                  ariaLabel={`Организация ${organization.name}`}
                  selected={isSelected(organization.id)}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  <SelectionTableCell
                    checked={isSelected(organization.id)}
                    onCheck={(extendRange) => toggle(organization, extendRange)}
                  />
                  <TableCell>{organization.name}</TableCell>
                  <TableCell sx={{fontFamily: "monospace"}}>{organization.inn}</TableCell>
                  <TableCell>
                    <Chip
                      label={ORGANIZATION_KIND_LABELS[organization.kind]}
                      size="small"
                      variant="outlined"
                    />
                  </TableCell>
                  <TableCell>{organization.legalName ?? "—"}</TableCell>
                  <TableCell>
                    {organization.accounts.length === 0 ? (
                      "—"
                    ) : (
                      <Stack direction="row" spacing={0.5} useFlexGap sx={{flexWrap: "wrap"}}>
                        {organization.accounts.map((account) => (
                          <MarketplaceAccountChip
                            key={account.id}
                            accountId={account.id}
                            name={account.name}
                            type={account.type}
                          />
                        ))}
                      </Stack>
                    )}
                  </TableCell>
                </LinkTableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
      <OrganizationFormDialog target={formTarget} onClose={() => setFormTarget(null)} />
    </Stack>
  );
}

export default OrganizationsPage;
