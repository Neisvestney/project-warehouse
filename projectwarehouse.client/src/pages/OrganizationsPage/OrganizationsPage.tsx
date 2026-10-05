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
import type {OrganizationSortBy} from "@/api/types.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import {ORGANIZATION_KIND_LABELS} from "@/features/organizations/organizationKinds";
import OrganizationFormDialog from "./components/OrganizationFormDialog";
import type {OrganizationFormTarget} from "./components/OrganizationFormDialog";

const SORT_COLUMNS: {key: OrganizationSortBy; label: string}[] = [
  {key: "name", label: "Название"},
  {key: "inn", label: "ИНН"},
  {key: "createdAt", label: "Создана"},
];

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
              {sortableHeader("name", "Название")}
              {sortableHeader("inn", "ИНН")}
              <TableCell>Тип</TableCell>
              <TableCell>Полное наименование</TableCell>
              <TableCell>Аккаунтов</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={5} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={5} message="Организаций нет" />
            ) : (
              data?.items.map((organization) => (
                <LinkTableRow
                  key={organization.id}
                  to={`/organizations/${organization.id}`}
                  ariaLabel={`Организация ${organization.name}`}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
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
                  <TableCell>{organization.accountCount}</TableCell>
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
