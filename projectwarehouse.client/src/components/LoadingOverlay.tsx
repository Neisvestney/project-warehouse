import {useEffect, useRef, useState} from "react";
import {
  Box,
  CircularProgress,
  Fade,
  Typography,
  alpha,
  type SxProps,
  type Theme,
} from "@mui/material";

interface LoadingOverlayProps {
  open: boolean;
  /** Спиннер появляется, только если загрузка длится дольше, мс */
  delay?: number;
  /** Минимальное время показа подложки после появления, мс */
  minDuration?: number;
  label?: string;
  blur?: number;
  size?: number;
  sx?: SxProps<Theme>;
}

function LoadingOverlay({
  open,
  delay = 300,
  minDuration = 200,
  label,
  blur = 1,
  size = 40,
  sx,
}: LoadingOverlayProps) {
  // holding — только «хвост» после open=false; сама подложка идёт от open, без задержки
  const [holding, setHolding] = useState(false);
  const [spinner, setSpinner] = useState(false);
  const shownAtRef = useRef(0);
  const visible = open || holding;

  useEffect(() => {
    if (open) {
      shownAtRef.current = Date.now();
      const hold = setTimeout(() => setHolding(true), 0);
      const spin = setTimeout(() => setSpinner(true), delay);
      return () => {
        clearTimeout(hold);
        clearTimeout(spin);
      };
    }

    const remaining = Math.max(0, minDuration - (Date.now() - shownAtRef.current));
    shownAtRef.current = 0;
    const timer = setTimeout(() => {
      setHolding(false);
      setSpinner(false);
    }, remaining);
    return () => clearTimeout(timer);
  }, [open, delay, minDuration]);

  return (
    // appear only affects the first render: a page restored from cache opens with the backdrop
    // already there instead of fading it in over content that is visibly not fresh.
    <Fade in={visible} appear={false} timeout={{enter: 150, exit: 250}} unmountOnExit>
      <Box
        sx={[
          (theme) => ({
            position: "absolute",
            inset: 0,
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            zIndex: 3,
            borderRadius: "inherit",
            cursor: "progress",
            backgroundColor: alpha(theme.palette.background.default, 0.55),
            backdropFilter: `blur(${blur}px)`,
          }),
          ...(Array.isArray(sx) ? sx : [sx]),
        ]}
      >
        {/* по центру, а на длинных страницах sticky не даёт уехать за пределы экрана */}
        <Fade in={spinner} timeout={{enter: 200, exit: 150}}>
          <Box
            sx={{
              position: "sticky",
              top: 24,
              bottom: 24,
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: 1,
            }}
          >
            <CircularProgress size={size} />
            {label && (
              <Typography variant="body2" color="text.secondary">
                {label}
              </Typography>
            )}
          </Box>
        </Fade>
      </Box>
    </Fade>
  );
}

export default LoadingOverlay;
