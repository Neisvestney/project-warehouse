import type {ReactNode} from "react";
import {Box} from "@mui/material";

// Выделяем последние 4 цифры первого сегмента: 0132298262-0184-1 -> 8262
const HIGHLIGHTED_PART_REGEX = /^(\d*)(\d{4})(?=-|$)/;

export function formatPostingNumber(postingNumber: string | null | undefined): ReactNode {
  if (!postingNumber) return null;

  const match = HIGHLIGHTED_PART_REGEX.exec(postingNumber);
  if (!match) return postingNumber;

  return (
    <>
      {match[1]}
      <Box
        component="span"
        sx={{
          display: "inline-block",
          px: 0.5,
          borderRadius: 1,
          bgcolor: "common.black",
          color: "common.white",
          fontWeight: "bold",
          lineHeight: 1.4,
        }}
      >
        {match[2]}
      </Box>
      {postingNumber.slice(match[0].length)}
    </>
  );
}
