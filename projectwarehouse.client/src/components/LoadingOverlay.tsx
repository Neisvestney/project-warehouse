import {Box, CircularProgress, Fade} from "@mui/material";

interface LoadingOverlayProps {
  open: boolean;
}

function LoadingOverlay({open}: LoadingOverlayProps) {
  if (!open) return null;

  return (
    <Box
      sx={{
        position: "absolute",
        inset: 0,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 1,
        borderRadius: "inherit",
        animation: "loadingOverlayBg 120ms ease forwards",
        "@keyframes loadingOverlayBg": {
          from: {backgroundColor: "rgba(255, 255, 255, 0)"},
          to: {backgroundColor: "rgba(255, 255, 255, 0.75)"},
        },
      }}
    >
      <Fade in timeout={200} style={{transitionDelay: "80ms"}}>
        <CircularProgress />
      </Fade>
    </Box>
  );
}

export default LoadingOverlay;
