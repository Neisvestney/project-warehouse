import {useState} from "react";
import {useQuery} from "@tanstack/react-query";
import {
  Box,
  Card,
  CardContent,
  CircularProgress,
  Collapse,
  Grid,
  IconButton,
  LinearProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import {systemGetDatabaseStatsOptions} from "@/api/@tanstack/react-query.gen";
import type {EntityTypeStatDto} from "@/api/types.gen";
import QueryError from "@/components/QueryError";
import {formatFileSize} from "@/components/files/fileUtils";
import {entitiesTypes} from "@/utils/appEntityUtils";

const numberFormat = new Intl.NumberFormat("ru-RU");

function EntityTypeRow({group, maxSize}: {group: EntityTypeStatDto; maxSize: number}) {
  const [open, setOpen] = useState(false);
  const config = entitiesTypes[group.entityType];

  return (
    <>
      <TableRow hover sx={{cursor: "pointer"}} onClick={() => setOpen((v) => !v)}>
        <TableCell sx={{width: 48}}>
          <IconButton size="small">
            {open ? (
              <KeyboardArrowDownIcon fontSize="small" />
            ) : (
              <KeyboardArrowRightIcon fontSize="small" />
            )}
          </IconButton>
        </TableCell>
        <TableCell>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <Box sx={{display: "flex", color: "text.secondary"}}>{config.icon}</Box>
            <Typography variant="body2">
              {group.entityType === "unknown" ? "Прочее" : config.typeName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {group.tables.length} табл.
            </Typography>
          </Stack>
        </TableCell>
        <TableCell align="right">
          {group.rowEstimate == null ? "—" : numberFormat.format(group.rowEstimate)}
        </TableCell>
        <TableCell align="right">{formatFileSize(group.indexSizeBytes)}</TableCell>
        <TableCell align="right">{formatFileSize(group.sizeBytes)}</TableCell>
        <TableCell sx={{width: "25%"}}>
          <LinearProgress
            variant="determinate"
            value={(group.sizeBytes / maxSize) * 100}
            sx={{height: 6, borderRadius: 3}}
          />
        </TableCell>
      </TableRow>

      <TableRow>
        <TableCell sx={{py: 0, borderBottom: open ? undefined : "none"}} colSpan={6}>
          <Collapse in={open} unmountOnExit>
            <Table size="small" sx={{my: 1}}>
              <TableBody>
                {group.tables.map((table) => (
                  <TableRow key={table.name}>
                    <TableCell sx={{pl: 6, fontFamily: "monospace"}}>{table.name}</TableCell>
                    <TableCell align="right">
                      {table.rowEstimate == null ? "—" : numberFormat.format(table.rowEstimate)}
                    </TableCell>
                    <TableCell align="right">{formatFileSize(table.indexSizeBytes)}</TableCell>
                    <TableCell align="right">{formatFileSize(table.sizeBytes)}</TableCell>
                    <TableCell sx={{width: "25%"}} />
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

export default function DatabaseTab() {
  const {data, isLoading, isError, error} = useQuery({
    ...systemGetDatabaseStatsOptions(),
    meta: {suppressGlobalError: true},
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 6}}>
        <CircularProgress />
      </Box>
    );
  }
  if (isError) return <QueryError error={error} />;
  if (!data) return null;

  const maxSize = Math.max(1, ...data.byEntityType.map((g) => g.sizeBytes));
  const indexSize = data.byEntityType.reduce((sum, g) => sum + g.indexSizeBytes, 0);
  const rows = data.byEntityType.reduce((sum, g) => sum + (g.rowEstimate ?? 0), 0);

  return (
    <Stack spacing={3}>
      <Grid container spacing={2}>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter label="Размер БД" value={formatFileSize(data.totalSizeBytes)} />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter
            label="Из них таблицы"
            value={formatFileSize(data.tablesSizeBytes)}
            hint="остальное — служебные каталоги Postgres"
          />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter label="Индексы" value={formatFileSize(indexSize)} />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter
            label="Строк"
            value={`≈ ${numberFormat.format(rows)}`}
            hint="оценка планировщика, не точный счёт"
          />
        </Grid>
      </Grid>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="subtitle2" gutterBottom>
            По сущностям
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell />
                <TableCell>Сущность</TableCell>
                <TableCell align="right">Строк ≈</TableCell>
                <TableCell align="right">Индексы</TableCell>
                <TableCell align="right">Всего</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {data.byEntityType.map((group) => (
                <EntityTypeRow key={group.entityType} group={group} maxSize={maxSize} />
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </Stack>
  );
}

function Counter({label, value, hint}: {label: string; value: string; hint?: string}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h6">{value}</Typography>
        {hint && (
          <Typography variant="caption" color="text.secondary">
            {hint}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
