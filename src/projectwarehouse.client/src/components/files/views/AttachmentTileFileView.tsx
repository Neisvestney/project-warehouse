import Box from "@mui/material/Box";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import CircularProgress from "@mui/material/CircularProgress";
import CloseIcon from "@mui/icons-material/Close";
import FileImage from "../FileImage";
import FileTypeIcon from "./FileTypeIcon";
import type {FileViewProps} from "./fileViewProps";

const SIZE = 120;

/**
 * Grid tile for an attachment list: square preview (or a type icon for non-images) with the
 * filename underneath, so a scan or an invoice stays identifiable without opening it.
 */
export default function AttachmentTileFileView({
  file,
  loading,
  disabled,
  onDelete,
  onOpen,
}: FileViewProps) {
  return (
    <Box sx={{width: SIZE}}>
      <Box
        sx={{
          position: "relative",
          width: SIZE,
          height: SIZE,
          borderRadius: 1,
          overflow: "hidden",
          border: "1px solid",
          borderColor: "divider",
          cursor: onOpen ? "pointer" : "default",
          "&:hover .file-tile-remove": {opacity: 1},
        }}
        onClick={onOpen}
      >
        <FileImage
          source={file}
          previewWidth={SIZE * 2}
          style={{height: "100%"}}
          fallback={
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                width: "100%",
                height: "100%",
                bgcolor: "action.hover",
              }}
            >
              <FileTypeIcon contentType={file.contentType} color="disabled" fontSize="large" />
            </Box>
          }
        />

        {loading && (
          <Box
            sx={{
              position: "absolute",
              inset: 0,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: "rgba(0,0,0,0.35)",
            }}
          >
            <CircularProgress size={20} sx={{color: "common.white"}} />
          </Box>
        )}

        {onDelete && !disabled && (
          <IconButton
            className="file-tile-remove"
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              onDelete();
            }}
            sx={{
              position: "absolute",
              top: 2,
              right: 2,
              opacity: 0,
              transition: "opacity 120ms",
              bgcolor: "rgba(0,0,0,0.5)",
              color: "common.white",
              "&:hover": {bgcolor: "rgba(0,0,0,0.7)"},
            }}
          >
            <CloseIcon sx={{fontSize: 14}} />
          </IconButton>
        )}
      </Box>

      <Tooltip title={file.originalFileName}>
        <Typography variant="caption" noWrap sx={{display: "block", mt: 0.5, textAlign: "center"}}>
          {file.originalFileName}
        </Typography>
      </Tooltip>
    </Box>
  );
}
