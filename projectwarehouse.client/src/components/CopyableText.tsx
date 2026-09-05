import type {CSSProperties, MouseEvent} from "react";
import type {SxProps, Theme} from "@mui/material";
import {IconButton, Stack, Tooltip} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import {useSnackbar} from "notistack";
import {copyToClipboard} from "@/utils/clipboardUtils";

interface CopyableTextProps {
  value: string;
  successMessage?: string;
  sx?: SxProps<Theme>;
  textStyle?: CSSProperties;
}

function CopyableText({value, successMessage = "Скопировано", sx, textStyle}: CopyableTextProps) {
  const {enqueueSnackbar} = useSnackbar();

  const handleCopy = async (e: MouseEvent) => {
    e.stopPropagation();
    const copied = await copyToClipboard(value);
    enqueueSnackbar(copied ? successMessage : "Не удалось скопировать", {
      variant: copied ? "success" : "error",
    });
  };

  return (
    <Stack direction="row" spacing={0.25} sx={{alignItems: "center", minWidth: 0, ...sx}}>
      <span style={{whiteSpace: "pre-wrap", overflowWrap: "anywhere", ...textStyle}}>{value}</span>
      <Tooltip title="Скопировать">
        <IconButton size="small" onClick={handleCopy} sx={{p: 0.25, flexShrink: 0}}>
          <ContentCopyIcon sx={{fontSize: 14}} />
        </IconButton>
      </Tooltip>
    </Stack>
  );
}

export default CopyableText;
