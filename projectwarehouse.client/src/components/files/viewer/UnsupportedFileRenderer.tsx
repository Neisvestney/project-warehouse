import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Typography from "@mui/material/Typography";
import DownloadIcon from "@mui/icons-material/Download";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import FileTypeIcon from "../views/FileTypeIcon";
import {formatFileSize} from "../fileUtils";
import type {ResolvedViewable} from "./useViewableSource";

export default function UnsupportedFileRenderer({item}: {item: ResolvedViewable}) {
  const newTab = item.download.mode === "newTab";

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 2,
        height: "100%",
        p: 4,
        color: "common.white",
      }}
    >
      <FileTypeIcon contentType={item.contentType} sx={{fontSize: 72, opacity: 0.7}} />

      <Typography variant="subtitle1" align="center" sx={{wordBreak: "break-all"}}>
        {item.name}
      </Typography>

      {item.meta && (
        <Typography variant="body2" sx={{opacity: 0.7}}>
          {formatFileSize(item.meta.sizeBytes)}
        </Typography>
      )}

      {item.download.url && (
        <Button
          variant="contained"
          startIcon={newTab ? <OpenInNewIcon /> : <DownloadIcon />}
          component="a"
          href={item.download.url}
          download={newTab ? undefined : item.download.fileName}
          target={newTab ? "_blank" : undefined}
          rel={newTab ? "noopener noreferrer" : undefined}
        >
          {newTab ? "Открыть в новой вкладке" : "Скачать"}
        </Button>
      )}
    </Box>
  );
}
