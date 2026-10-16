import {Box} from "@mui/material";
import SkeletonFade from "@/components/SkeletonFade";

interface OptionCountProps {
  /** Nothing is rendered while the count is unknown. */
  count: number | undefined;
  /** Covers the number with a skeleton while the list is fetching, even over a retained count. */
  loading?: boolean;
}

/** A count pushed to the trailing edge of a select option; a zero is greyed out. */
function OptionCount({count, loading = false}: OptionCountProps) {
  if (count === undefined) return null;
  const number = (
    <Box
      component="span"
      sx={{
        typography: "body2",
        fontVariantNumeric: "tabular-nums",
        color: count === 0 ? "text.disabled" : "text.secondary",
      }}
    >
      {count.toLocaleString("ru-RU")}
    </Box>
  );
  return (
    <Box component="span" sx={{ml: "auto", pl: 2, display: "inline-flex"}}>
      <SkeletonFade loading={loading}>{number}</SkeletonFade>
    </Box>
  );
}

export default OptionCount;
