import Box from "@mui/material/Box";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import CircularProgress from "@mui/material/CircularProgress";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import FileTypeIcon from "./FileTypeIcon";
import {formatFileSize} from "../fileUtils";
import type {FileViewProps} from "./fileViewProps";

/** List row: icon, name, size, actions. */
export default function RowFileView({file, loading, disabled, onDelete, onOpen}: FileViewProps) {
  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        gap: 1.5,
        px: 1,
        py: 0.75,
        borderRadius: 1,
        "&:hover": {bgcolor: "action.hover"},
      }}
    >
      <FileTypeIcon contentType={file.contentType} color="action" />

      <Box sx={{minWidth: 0, flex: 1}}>
        <Typography
          variant="body2"
          noWrap
          onClick={onOpen}
          sx={{cursor: onOpen ? "pointer" : "default"}}
        >
          {file.originalFileName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {formatFileSize(file.sizeBytes)}
        </Typography>
      </Box>

      {loading && <CircularProgress size={18} />}

      {onDelete && (
        <Tooltip title="Удалить">
          <span>
            <IconButton size="small" onClick={onDelete} disabled={disabled || loading}>
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
      )}
    </Box>
  );
}
