import type {SxProps, Theme} from "@mui/material";
import {Box, Tooltip} from "@mui/material";
import type {MarketplaceOrderDto} from "@/api/types.gen";
import CopyableText from "@/components/CopyableText";
import {formatPostingNumber, formatScanitBarcode} from "@/utils/postingNumberUtils";

interface PostingNumberLabelProps {
  marketplaceOrder: Pick<MarketplaceOrderDto, "postingNumber" | "scanitBarcode">;
  sx?: SxProps<Theme>;
}

function PostingNumberLabel({marketplaceOrder, sx}: PostingNumberLabelProps) {
  const {postingNumber, scanitBarcode} = marketplaceOrder;

  if (!scanitBarcode) {
    return (
      <CopyableText value={postingNumber} successMessage="Номер отправления скопирован" sx={sx}>
        {formatPostingNumber(postingNumber)}
      </CopyableText>
    );
  }

  return (
    <Tooltip
      title={
        <CopyableText
          value={postingNumber}
          successMessage="Номер отправления скопирован"
          // mirrors the spacing + 14px icon on the right, so the number sits centred
          sx={{pl: 2.25, "& .hover-action-icon": {color: "inherit"}}}
        >
          {formatPostingNumber(postingNumber)}
        </CopyableText>
      }
    >
      {/* Tooltip needs a DOM child to attach its listeners to */}
      <Box component="span" sx={[{display: "inline-flex"}, ...(Array.isArray(sx) ? sx : [sx])]}>
        <CopyableText value={scanitBarcode} successMessage="Штрихкод отправления скопирован">
          {formatScanitBarcode(scanitBarcode)}
        </CopyableText>
      </Box>
    </Tooltip>
  );
}

export default PostingNumberLabel;
