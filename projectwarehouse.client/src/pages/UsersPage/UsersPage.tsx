import {
  Breadcrumbs,
  Button,
  Chip,
  CircularProgress,
  InputAdornment,
  LinearProgress,
  Link,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import {useQuery} from "@tanstack/react-query";
import {usersGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {Link as RouterLink} from "react-router";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AddIcon from "@mui/icons-material/Add";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";

function UsersPage() {
  // inputValue: immediate local state for the TextField (no keystroke lag)
  // setInputValue: onChange handler
  // searchString: URL-synced value (written after 300ms debounce) for API params
  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  // searchString (URL) is already debounced — pass as immediateParams to avoid
  // an additional debounce inside usePaginatedParams.
  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString},
    [searchString],
  );

  const {data, isLoading, isFetching} = useQuery(usersGetAllOptions({query: fetchParams}));

  const totalUsers = Number(data?.total ?? 0);

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Пользователи", link: "/users"}, {name: "Список"}]} />
      <PageGenericHeader
        title={"Пользователи"}
        right={
          <Button variant="contained" endIcon={<AddIcon />}>
            Создать
          </Button>
        }
      >
        <TextField
          size="small"
          label="Поиск..."
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon />
                </InputAdornment>
              ),
            },
          }}
        />
      </PageGenericHeader>
      <Paper>
        <LinearProgress
          sx={{visibility: isFetching ? "visible" : "hidden", borderRadius: "4px 4px 0 0"}}
        />
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Имя пользователя</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Имя</TableCell>
                <TableCell>Фамилия</TableCell>
                <TableCell>Роли</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{py: 4}}>
                    <CircularProgress size={32} />
                  </TableCell>
                </TableRow>
              ) : data?.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{py: 4}}>
                    <Typography color="text.secondary">Пользователи не найдены</Typography>
                  </TableCell>
                </TableRow>
              ) : (
                data?.items.map((user) => (
                  <TableRow
                    key={user.id}
                    sx={{
                      opacity: isFetching && !isLoading ? 0.5 : 1,
                      transition: "opacity 0.2s",
                    }}
                  >
                    <TableCell>{user.username}</TableCell>
                    <TableCell>{user.email ?? "—"}</TableCell>
                    <TableCell>{user.firstName ?? "—"}</TableCell>
                    <TableCell>{user.lastName ?? "—"}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap"}}>
                        {user.roles.map((role) => (
                          <Chip key={role.id} label={role.name} size="small" />
                        ))}
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination
          component="div"
          count={totalUsers}
          page={page - 1}
          rowsPerPage={pageSize}
          rowsPerPageOptions={[10, 20, 50]}
          onPageChange={(_, newPage) => setPage(newPage + 1)}
          onRowsPerPageChange={(e) => setPageSize(Number(e.target.value))}
          labelRowsPerPage="Строк на странице:"
          labelDisplayedRows={({from, to, count}) => `${from}–${to} из ${count}`}
        />
      </Paper>
    </Stack>
  );
}

export default UsersPage;
