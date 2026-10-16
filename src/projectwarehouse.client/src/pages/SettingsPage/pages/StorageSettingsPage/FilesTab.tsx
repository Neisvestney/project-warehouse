import {useQuery} from "@tanstack/react-query";
import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  Grid,
  LinearProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {systemGetStorageStatsOptions} from "@/api/@tanstack/react-query.gen";
import type {StorageStatsDto} from "@/api/types.gen";
import {useModal} from "@/hooks/useModal";
import QueryError from "@/components/QueryError";
import {formatFileSize} from "@/components/files/fileUtils";
import FileViewerModal from "@/components/files/viewer/FileViewerModal";
import {viewable} from "@/components/files/viewer/viewableFile";

function usageColor(percent: number) {
  if (percent > 90) return "error" as const;
  if (percent > 75) return "warning" as const;
  return "primary" as const;
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

function DiskCard({disk, takenAt}: {disk: StorageStatsDto["disk"]; takenAt?: string | null}) {
  if (!disk) {
    return (
      <Alert severity="info">Не удалось определить точку монтирования хранилища на диске.</Alert>
    );
  }

  const percent = disk.totalBytes > 0 ? (disk.usedBytes / disk.totalBytes) * 100 : 0;

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={1}>
          <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "baseline"}}>
            <Typography variant="subtitle2">Диск {disk.mountPoint}</Typography>
            <Typography variant="caption" color="text.secondary">
              {takenAt && `по состоянию на ${new Date(takenAt).toLocaleTimeString("ru-RU")}`}
            </Typography>
          </Stack>
          <LinearProgress
            variant="determinate"
            value={Math.min(100, percent)}
            color={usageColor(percent)}
            sx={{height: 10, borderRadius: 5}}
          />
          <Typography variant="body2" color="text.secondary">
            занято {formatFileSize(disk.usedBytes)} из {formatFileSize(disk.totalBytes)} (
            {percent.toFixed(0)}%), свободно {formatFileSize(disk.freeBytes)}
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}

export default function FilesTab() {
  const {showModal} = useModal();
  const {data, isLoading, isError, error} = useQuery({
    ...systemGetStorageStatsOptions(),
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

  const maxTypeSize = Math.max(1, ...data.byContentType.map((t) => t.sizeBytes));

  return (
    <Stack spacing={3}>
      <Grid container spacing={2}>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter label="Файлов" value={String(data.fileCount)} />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter label="Общий объём" value={formatFileSize(data.totalSizeBytes)} />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter label="Кэш превью" value={formatFileSize(data.thumbnailCacheSizeBytes)} />
        </Grid>
        <Grid size={{xs: 12, sm: 6, md: 3}}>
          <Counter
            label="Не привязано"
            value={`${data.orphanCount} · ${formatFileSize(data.orphanSizeBytes)}`}
            hint={
              data.orphanDueCount > 0
                ? `из них к удалению: ${data.orphanDueCount}`
                : `удаляются автоматически через ${data.orphanTtlHours} ч после загрузки`
            }
          />
        </Grid>
      </Grid>

      <DiskCard disk={data.disk} takenAt={data.diskStatsAt} />

      {data.byContentType.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="subtitle2" gutterBottom>
              По типам файлов
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Тип</TableCell>
                  <TableCell align="right">Файлов</TableCell>
                  <TableCell align="right">Объём</TableCell>
                  <TableCell sx={{width: "35%"}} />
                </TableRow>
              </TableHead>
              <TableBody>
                {data.byContentType.map((row) => (
                  <TableRow key={row.contentType}>
                    <TableCell>{row.contentType}</TableCell>
                    <TableCell align="right">{row.count}</TableCell>
                    <TableCell align="right">{formatFileSize(row.sizeBytes)}</TableCell>
                    <TableCell>
                      <LinearProgress
                        variant="determinate"
                        value={(row.sizeBytes / maxTypeSize) * 100}
                        sx={{height: 6, borderRadius: 3}}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {data.largestFiles.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="subtitle2" gutterBottom>
              Самые крупные файлы
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Имя</TableCell>
                  <TableCell>Тип</TableCell>
                  <TableCell align="right">Размер</TableCell>
                  <TableCell>Загружен</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.largestFiles.map((file) => (
                  <TableRow key={file.id} hover>
                    <TableCell>
                      <Typography
                        variant="body2"
                        sx={{cursor: "pointer", textDecoration: "underline dotted"}}
                        onClick={() =>
                          showModal(FileViewerModal, {
                            files: [
                              viewable({
                                id: file.id,
                                originalFileName: file.originalFileName,
                                contentType: file.contentType,
                                sizeBytes: file.sizeBytes,
                                imageWidth: null,
                                imageHeight: null,
                                isImage: file.contentType.indexOf("image/") === 0,
                                createdById: null,
                                createdByUserName: null,
                                createdAt: file.createdAt,
                              }),
                            ],
                          })
                        }
                      >
                        {file.originalFileName}
                      </Typography>
                    </TableCell>
                    <TableCell>{file.contentType}</TableCell>
                    <TableCell align="right">{formatFileSize(file.sizeBytes)}</TableCell>
                    <TableCell>{new Date(file.createdAt).toLocaleDateString("ru-RU")}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
}
