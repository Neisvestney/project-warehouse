import type {ReactNode} from "react";
import {Box} from "@mui/material";

// Highlights the last 4 digits of the first segment: 0132298262-0184-1 -> 8262
const HIGHLIGHTED_PART_REGEX = /^(\d*)(\d{4})(?=-|$)/;
const SCANIT_HIGHLIGHTED_PART_REGEX = /^(.*?)(\d{4})$/;

// Expects a match with exactly two groups: [1] prefix, [2] highlighted digits
function highlight(value: string, match: RegExpExecArray): ReactNode {
  return (
    <>
      {match[1]}
      <Box
        component="span"
        sx={{
          display: "inline-block",
          px: 0.5,
          borderRadius: 1,
          bgcolor: "text.primary",
          color: "background.paper",
          fontWeight: "bold",
          lineHeight: 1.4,
        }}
      >
        {match[2]}
      </Box>
      {value.slice(match[0].length)}
    </>
  );
}

export function formatPostingNumber(postingNumber: string | null | undefined): ReactNode {
  if (!postingNumber) return null;

  const match = HIGHLIGHTED_PART_REGEX.exec(postingNumber);
  if (!match) return postingNumber;

  return highlight(postingNumber, match);
}

export function formatScanitBarcode(scanitBarcode: string | null | undefined): ReactNode {
  if (!scanitBarcode) return null;

  const match = SCANIT_HIGHLIGHTED_PART_REGEX.exec(scanitBarcode);
  if (!match) return scanitBarcode;

  return highlight(scanitBarcode, match);
}
