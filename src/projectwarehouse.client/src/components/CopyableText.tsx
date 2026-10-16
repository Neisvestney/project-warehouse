import type {CSSProperties, ReactNode} from "react";
import type {SxProps, Theme} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import {useSnackbar} from "notistack";
import {copyToClipboard} from "@/utils/clipboardUtils";
import HoverActionLink from "@/components/HoverActionLink";

interface CopyableTextProps {
  value: string;
  successMessage?: string;
  sx?: SxProps<Theme>;
  textStyle?: CSSProperties;
  /** Rendered in place of `value`, e.g. a highlighted posting number. */
  children?: ReactNode;
}

function CopyableText({
  value,
  successMessage = "Скопировано",
  sx,
  textStyle,
  children,
}: CopyableTextProps) {
  const {enqueueSnackbar} = useSnackbar();

  const handleCopy = async () => {
    const copied = await copyToClipboard(value);
    enqueueSnackbar(copied ? successMessage : "Не удалось скопировать", {
      variant: copied ? "success" : "error",
    });
  };

  return (
    <HoverActionLink
      icon={ContentCopyIcon}
      onClick={handleCopy}
      ariaLabel={`Скопировать ${value}`}
      spacing={0.5}
      sx={[{minWidth: 0}, ...(Array.isArray(sx) ? sx : [sx])]}
    >
      <span style={{whiteSpace: "pre-wrap", overflowWrap: "anywhere", ...textStyle}}>
        {children ?? value}
      </span>
    </HoverActionLink>
  );
}

export default CopyableText;
