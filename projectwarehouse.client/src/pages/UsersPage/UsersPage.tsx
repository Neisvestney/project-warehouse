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
  Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink, useNavigate} from "react-router";
import {usersGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import {getPermissionLabel} from "@/utils/permissionLabels.ts";
import type {UserDetailDto} from "@/api";
import RolesSelect from "@/components/RolesSelect.tsx";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState.ts";
import RefreshIcon from "@mui/icons-material/Refresh";
import SearchInput from "@/components/SearchInput.tsx";
import FiltersBar from "@/components/FiltersBar.tsx";
import DataTableContainer from "@/components/DataTableContainer.tsx";
import TableRowLoader from "@/components/TableRowLoader.tsx";
import TableRowEmpty from "@/components/TableRowEmpty.tsx";

function renderDirectPermissions(user: UserDetailDto) {
  const overflowing = user.directPermissions.length > 3;
  const directPermissions = overflowing
    ? user.directPermissions.slice(1, 3)
    : user.directPermissions;

  return (
    <>
      {directPermissions.map((p) => (
        <Tooltip key={p} title={p} arrow>
          <Chip label={getPermissionLabel(p)} size="small" />
        </Tooltip>
      ))}
      {overflowing && <Chip label={`+${user.directPermissions.length - 2}`} size="small" />}
    </>
  );
}

function UsersPage() {
  const navigate = useNavigate();
  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [roleId, setRoleId] = useSyncedWithQueryState(
    "role",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString, role: roleId ?? undefined},
    [searchString, roleId],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(usersGetAllOptions({query: fetchParams}));

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Пользователи", link: "/users"}, {name: "Список"}]} />
      <PageGenericHeader
        title={"Пользователи"}
        right={
          <>
            <IconButton color={"inherit"} onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
            <Button variant="outlined" endIcon={<AddIcon />} component={RouterLink} to="/users/new">
              Создать
            </Button>
          </>
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <FiltersBar>
        <RolesSelect value={roleId} onChange={setRoleId} sx={{flexBasis: 150}} size={"small"} />
      </FiltersBar>
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
              <TableCell>Имя пользователя</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Имя</TableCell>
              <TableCell>Фамилия</TableCell>
              <TableCell>Роли и права</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={5} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={5} message="Пользователи не найдены" />
            ) : (
              data?.items.map((user) => (
                <TableRow
                  key={user.id}
                  hover
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/users/${user.id}`)}
                >
                  <TableCell>{user.username}</TableCell>
                  <TableCell>{user.email ?? "—"}</TableCell>
                  <TableCell>{user.firstName ?? "—"}</TableCell>
                  <TableCell>{user.lastName ?? "—"}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap"}} useFlexGap>
                      {user.roles.map((role) => (
                        <Chip key={role.id} label={role.name} size="small" />
                      ))}
                      {renderDirectPermissions(user)}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default UsersPage;
