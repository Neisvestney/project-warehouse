import type {SxProps, Theme} from "@mui/material";
import {Box, Tooltip} from "@mui/material";
import type {MarketplaceOrderDto} from "@/api/types.gen";
import CopyableText from "@/components/CopyableText";
import {MARKETPLACE_TYPE_COLORS} from "@/components/marketplace/marketplaceUtils";
import {formatPostingNumber, formatLabelBarcode} from "@/utils/postingNumberUtils";

interface PostingNumberLabelProps {
  marketplaceOrder: Pick<MarketplaceOrderDto, "postingNumber" | "labelBarcode" | "marketplaceType">;
  sx?: SxProps<Theme>;
}

function PostingNumberLabel({marketplaceOrder, sx}: PostingNumberLabelProps) {
  const {postingNumber, labelBarcode} = marketplaceOrder;
  const color = MARKETPLACE_TYPE_COLORS[marketplaceOrder.marketplaceType];

  if (!labelBarcode) {
    return (
      <CopyableText value={postingNumber} successMessage="Номер отправления скопирован" sx={sx}>
        {formatPostingNumber(postingNumber, color)}
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
          {formatPostingNumber(postingNumber, color)}
        </CopyableText>
      }
    >
      {/* Tooltip needs a DOM child to attach its listeners to */}
      <Box component="span" sx={[{display: "inline-flex"}, ...(Array.isArray(sx) ? sx : [sx])]}>
        <CopyableText value={labelBarcode} successMessage="Номер с этикетки скопирован">
          {formatLabelBarcode(labelBarcode, color)}
        </CopyableText>
      </Box>
    </Tooltip>
  );
}

export default PostingNumberLabel;
