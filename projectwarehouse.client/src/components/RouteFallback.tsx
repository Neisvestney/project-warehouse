import {useEffect, useState} from "react";
import {Box, CircularProgress, keyframes} from "@mui/material";

export interface RouteFallbackProps {
  /** Spinner starts fading in only if loading outlasts this, ms */
  delay?: number;
}

const FADE_MS = 1000;
const SESSION_GAP_MS = 400;

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

function RouteFallback({delay = 50}: RouteFallbackProps) {
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

  return (
    <Box
      sx={{
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        minHeight: "60vh",
        // The negative delay is part of the shorthand: as a separate longhand it depends on emotion
        // emitting the keys in order, and reordering them would silently kill the handover.
        animation: `${fadeIn} ${FADE_MS}ms linear ${-Math.max(0, elapsed - delay)}ms both`,
      }}
    >
      <CircularProgress />
    </Box>
  );
}

export default RouteFallback;
