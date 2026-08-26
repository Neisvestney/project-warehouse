import {useEffect, useState} from "react";
import type {ComponentType} from "react";
import Box from "@mui/material/Box";
import Dialog from "@mui/material/Dialog";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import useMediaQuery from "@mui/material/useMediaQuery";
import {useTheme} from "@mui/material/styles";
import CloseIcon from "@mui/icons-material/Close";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import DownloadIcon from "@mui/icons-material/Download";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import type {DataFileDto} from "@/api";
import type {ModalComponentProps} from "@/contexts/Modal/ModalContext";
import {useBackClosable} from "@/hooks/useBackClosable.ts";
import FileImage from "../FileImage";
import {formatFileSize, isImageContentType, isPdfContentType} from "../fileUtils";
import ImageFileRenderer from "./ImageFileRenderer";
import PdfFileRenderer from "./PdfFileRenderer";
import UnsupportedFileRenderer from "./UnsupportedFileRenderer";
import {useViewableSource} from "./useViewableSource";
import type {ResolvedViewable} from "./useViewableSource";
import type {ViewableFile} from "./viewableFile";

export interface FileViewerModalProps extends ModalComponentProps<null> {
  files: ViewableFile[];
  initialIndex?: number;
  /** Called only for `dataFile` sources; the button is hidden for external ones. */
  onDelete?: (file: DataFileDto) => void;
}

/** First match wins. */
const renderers: {
  match: (v: ResolvedViewable) => boolean;
  Component: ComponentType<{item: ResolvedViewable}>;
}[] = [
  {
    match: (v) => isImageContentType(v.contentType) || v.contentType === "image/*",
    Component: ImageFileRenderer,
  },
  {match: (v) => isPdfContentType(v.contentType), Component: PdfFileRenderer},
  {match: () => true, Component: UnsupportedFileRenderer},
];

export default function FileViewerModal({
  open,
  onClose,
  files,
  initialIndex = 0,
  onDelete,
}: FileViewerModalProps) {
  const theme = useTheme();
  const fullScreen = useMediaQuery(theme.breakpoints.down("sm"));
  const [index, setIndex] = useState(initialIndex);

  const current = files[index];
  const item = useViewableSource(current ?? {kind: "external", url: ""});
  const hasMany = files.length > 1;

  useEffect(() => {
    if (!open || !hasMany) return;

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "ArrowLeft") setIndex((i) => (i - 1 + files.length) % files.length);
      if (e.key === "ArrowRight") setIndex((i) => (i + 1) % files.length);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, hasMany, files.length]);

  useBackClosable(open, () => onClose(null));

  if (!current) return null;

  const Renderer = renderers.find((r) => r.match(item))!.Component;
  const newTab = item.download.mode === "newTab";
  const deletable = onDelete && current.kind === "dataFile";

  return (
    <Dialog
      open={open}
      onClose={() => onClose(null)}
      fullScreen={fullScreen}
      maxWidth="xl"
      fullWidth
      slotProps={{
        paper: {
          sx: fullScreen
            ? {bgcolor: "grey.900", height: "100%"}
            : {
                bgcolor: "grey.900",
                width: "96vw",
                maxWidth: "96vw",
                height: "94vh",
                maxHeight: "94vh",
                m: 0,
              },
        },
      }}
    >
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 1,
          px: 1.5,
          py: 1,
          color: "common.white",
          borderBottom: "1px solid",
          borderColor: "rgba(255,255,255,0.12)",
        }}
      >
        <Box sx={{minWidth: 0, flex: 1}}>
          <Typography variant="subtitle2" noWrap>
            {item.name}
          </Typography>
          {/* the metadata line keeps its height for external sources so the toolbar does not jump */}
          <Typography variant="caption" sx={{opacity: 0.7}} noWrap component="div">
            {item.meta
              ? [
                  formatFileSize(item.meta.sizeBytes),
                  new Date(item.meta.createdAt).toLocaleDateString("ru-RU"),
                  item.meta.createdByUserName,
                ]
                  .filter(Boolean)
                  .join(" · ")
              : " "}
          </Typography>
        </Box>

        {hasMany && (
          <Typography variant="caption" sx={{opacity: 0.7, whiteSpace: "nowrap"}}>
            {index + 1} из {files.length}
          </Typography>
        )}

        {item.download.url && (
          <Tooltip title={newTab ? "Открыть в новой вкладке" : "Скачать"}>
            <IconButton
              sx={{color: "common.white"}}
              component="a"
              href={item.download.url}
              download={newTab ? undefined : item.download.fileName}
              target={newTab ? "_blank" : undefined}
              rel={newTab ? "noopener noreferrer" : undefined}
            >
              {newTab ? <OpenInNewIcon /> : <DownloadIcon />}
            </IconButton>
          </Tooltip>
        )}

        {deletable && (
          <Tooltip title="Удалить">
            <IconButton
              sx={{color: "common.white"}}
              onClick={() => {
                onDelete!((current as {kind: "dataFile"; file: DataFileDto}).file);
                onClose(null);
              }}
            >
              <DeleteOutlineIcon />
            </IconButton>
          </Tooltip>
        )}

        <IconButton sx={{color: "common.white"}} onClick={() => onClose(null)}>
          <CloseIcon />
        </IconButton>
      </Box>

      <Box sx={{position: "relative", flex: 1, minHeight: 0}}>
        {/* key remounts the renderer on navigation, resetting its per-file zoom and error state */}
        <Renderer key={item.key} item={item} />

        {hasMany && (
          <>
            <NavButton
              side="left"
              onClick={() => setIndex((i) => (i - 1 + files.length) % files.length)}
            />
            <NavButton side="right" onClick={() => setIndex((i) => (i + 1) % files.length)} />
          </>
        )}
      </Box>

      {hasMany && (
        <Box
          sx={{
            display: "flex",
            gap: 1,
            p: 1,
            overflowX: "auto",
            borderTop: "1px solid",
            borderColor: "rgba(255,255,255,0.12)",
          }}
        >
          {files.map((file, i) => (
            <Box
              key={file.kind === "dataFile" ? file.file.id : file.url}
              onClick={() => setIndex(i)}
              sx={{
                flex: "0 0 auto",
                width: 56,
                height: 56,
                borderRadius: 0.5,
                overflow: "hidden",
                cursor: "pointer",
                outline: i === index ? "2px solid" : "none",
                outlineColor: "primary.main",
                opacity: i === index ? 1 : 0.6,
              }}
            >
              <FileImage source={file} previewWidth={128} lazy={false} style={{height: "100%"}} />
            </Box>
          ))}
        </Box>
      )}
    </Dialog>
  );
}

function NavButton({side, onClick}: {side: "left" | "right"; onClick: () => void}) {
  return (
    <IconButton
      onClick={onClick}
      sx={{
        position: "absolute",
        top: "50%",
        transform: "translateY(-50%)",
        [side]: 8,
        color: "common.white",
        bgcolor: "rgba(0,0,0,0.4)",
        "&:hover": {bgcolor: "rgba(0,0,0,0.6)"},
      }}
    >
      {side === "left" ? <ChevronLeftIcon /> : <ChevronRightIcon />}
    </IconButton>
  );
}
