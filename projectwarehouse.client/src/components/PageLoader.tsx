import {useEffect, useState} from "react";
import {Box, CircularProgress, keyframes} from "@mui/material";

export interface PageLoaderProps {
  /** Spinner starts fading in only if loading outlasts this, ms */
  delay?: number;
  /**
   * Renders in normal flow (centered on its own container, not the viewport) instead of fixed to the
   * viewport's full width. Only for spots with real content around them, e.g. inline page-body loading
   * states — it reserves layout height, which would flash an empty full-page block for the fixed use
   * sites (auth guard, route Suspense) that render before there is any surrounding content yet.
   */
  inline?: boolean;
}

const FADE_MS = 1000;
const SESSION_GAP_MS = 400;

// A flat viewport offset, not dependent on the app bar: this fallback also renders before the app bar
// mounts (the auth guard's own fallback), so an app-bar-aware offset would put it at a different height
// than the same component rendered once routing has a layout under it. Shared with LoadingOverlay's
// alignTop mode so both land at the same height.
export const SPINNER_TOP = 200;

const fadeIn = keyframes`
  from { opacity: 0; }
  to { opacity: 1; }
`;

// A cold load hands over from the auth guard's fallback to the layout's one, and the two are separate
// instances. They share a start time so the second continues the first instead of restarting.
let liveCount = 0;
let sessionStartedAt = 0;
let sessionEndedAt = 0;

// A render can be discarded before it commits, so a seed is only trusted while an instance is actually
// mounted or briefly after the last one left — an orphaned seed expires instead of poisoning the module.
function joinSession(now: number) {
  if (liveCount === 0 && now - sessionEndedAt > SESSION_GAP_MS) sessionStartedAt = now;
  return sessionStartedAt;
}

function PageLoader({delay = 50, inline = false}: PageLoaderProps) {
  const [elapsed] = useState(() => {
    const now = Date.now();
    return now - joinSession(now);
  });
  const [spinner, setSpinner] = useState(elapsed >= delay);

  useEffect(() => {
    liveCount += 1;
    return () => {
      // Clamped: an HMR re-evaluation resets the counter under instances that still have to unmount.
      liveCount = Math.max(0, liveCount - 1);
      if (liveCount === 0) sessionEndedAt = Date.now();
    };
  }, []);

  useEffect(() => {
    if (spinner) return;
    const timer = setTimeout(() => setSpinner(true), delay - elapsed);
    return () => clearTimeout(timer);
  }, [spinner, delay, elapsed]);

  if (!spinner) return null;

  const fade = {
    animation: `${fadeIn} ${FADE_MS}ms linear ${
      // The negative delay is part of the shorthand: as a separate longhand it depends on emotion
      // emitting the keys in order, and reordering them would silently kill the handover.
      -Math.max(0, elapsed - delay)
    }ms both`,
  };

  if (inline) {
    return (
      <Box
        sx={{
          minHeight: "calc(min(80vh, 400px))",
          display: "flex",
          justifyContent: "center",
          // stretch (the flex default) would size this row's cross axis to the full 100vh, so the
          // sticky child never has to move to reach its offset — it'd render right at the top instead.
          alignItems: "flex-start",
          ...fade,
        }}
      >
        {/* sticky, not a plain top offset: this still has to land at the same height as the fixed
            variant above, which is measured from the viewport, not from wherever this box starts. */}
        <Box sx={{position: "sticky", top: SPINNER_TOP}}>
          <CircularProgress />
        </Box>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        position: "fixed",
        top: SPINNER_TOP,
        left: 0,
        right: 0,
        zIndex: 1050,
        display: "flex",
        justifyContent: "center",
        ...fade,
      }}
    >
      <CircularProgress />
    </Box>
  );
}

export default PageLoader;
