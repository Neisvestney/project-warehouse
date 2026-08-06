import Box from "@mui/material/Box";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import CircularProgress from "@mui/material/CircularProgress";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import FileImage from "../FileImage";
import FileTypeIcon from "./FileTypeIcon";
import type {FileViewProps} from "./fileViewProps";

const SIZE = 160;

/** Larger card for a single main image, with replace and delete actions on hover. */
export default function ImageCardFileView({
  file,
  loading,
  disabled,
  onDelete,
  onReplace,
  onOpen,
}: FileViewProps) {
  return (
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
        "&:hover .file-card-actions": {opacity: 1},
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
          <CircularProgress size={24} sx={{color: "common.white"}} />
        </Box>
      )}

      {!disabled && (onDelete || onReplace) && (
        <Box
          className="file-card-actions"
          sx={{
            position: "absolute",
            bottom: 0,
            left: 0,
            right: 0,
            display: "flex",
            justifyContent: "flex-end",
            gap: 0.5,
            p: 0.5,
            opacity: 0,
            transition: "opacity 120ms",
            bgcolor: "rgba(0,0,0,0.5)",
          }}
        >
          {onReplace && (
            <Tooltip title="Заменить">
              <IconButton
                size="small"
                sx={{color: "common.white"}}
                onClick={(e) => {
                  e.stopPropagation();
                  onReplace();
                }}
              >
                <SwapHorizIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
          {onDelete && (
            <Tooltip title="Удалить">
              <IconButton
                size="small"
                sx={{color: "common.white"}}
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete();
                }}
              >
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Box>
      )}
    </Box>
  );
}
