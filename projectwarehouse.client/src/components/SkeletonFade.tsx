import type {ReactNode} from "react";
import {Box, Fade, Skeleton, type SkeletonProps} from "@mui/material";

interface SkeletonFadeProps {
  loading: boolean;
  children: ReactNode;
  sx?: SkeletonProps["sx"];
}

/** Crossfades a skeleton over its content; the content stays laid out, so the box keeps its size. */
function SkeletonFade({loading, children, sx}: SkeletonFadeProps) {
  return (
    <Box component="span" sx={{position: "relative", display: "inline-flex"}}>
      <Box
        component="span"
        aria-hidden={loading}
        sx={(theme) => ({
          display: "inline-flex",
          opacity: loading ? 0 : 1,
          transition: theme.transitions.create("opacity", {
            duration: loading
              ? theme.transitions.duration.enteringScreen
              : theme.transitions.duration.leavingScreen,
          }),
        })}
      >
        {children}
      </Box>
      {/* fade on a wrapper since the pulse owns the skeleton's opacity; no appear, so a mid-fetch mount isn't blank */}
      <Fade in={loading} appear={false} unmountOnExit>
        <Box component="span" sx={{position: "absolute", inset: 0, pointerEvents: "none"}}>
          <Skeleton variant="rounded" width="100%" height="100%" sx={sx} />
        </Box>
      </Fade>
    </Box>
  );
}

export default SkeletonFade;
