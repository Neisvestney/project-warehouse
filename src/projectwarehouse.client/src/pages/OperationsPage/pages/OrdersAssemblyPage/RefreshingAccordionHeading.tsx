import type {ComponentProps} from "react";
import {styled} from "@mui/material";
import LoadingOverlay from "@/components/LoadingOverlay";

interface RefreshingAccordionHeadingProps extends ComponentProps<"h3"> {
  refreshing?: boolean;
  ownerState?: unknown;
}

/**
 * Accordion `heading` slot that also hosts a `LoadingOverlay`. The heading is not positioned, so with the
 * Accordion root at `position: relative` the overlay covers summary and details alike — Accordion itself
 * takes no extra children outside its collapsed region.
 */
function RefreshingAccordionHeading({
  refreshing = false,
  ownerState: _ownerState,
  children,
  ...props
}: RefreshingAccordionHeadingProps) {
  return (
    <Heading {...props}>
      {children}
      <LoadingOverlay open={refreshing} size={24} />
    </Heading>
  );
}

export default RefreshingAccordionHeading;

const Heading = styled("h3")({all: "unset"});
