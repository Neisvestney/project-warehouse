import {keyframes, styled} from "@mui/material/styles";

const snowfall = keyframes({
  to: {transform: "translate(var(--snow-drift), calc(100% + 24px))"},
});

const glow = keyframes({
  "50%": {opacity: 0.5, boxShadow: "0 1px 3px currentColor"},
});

export const NewYearSnowRoot = styled("div")(({theme}) => [
  {
    position: "absolute",
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    zIndex: -1,
    overflow: "hidden",
    pointerEvents: "none",
    "& span": {
      position: "absolute",
      top: -12,
      width: "var(--snow-size)",
      height: "100%",
      opacity: 0.85,
      animation: `${snowfall} calc(var(--snow-duration) * var(--snow-duration-scale, 1)) calc(var(--snow-delay) * var(--snow-duration-scale, 1)) linear infinite`,
      "&::before": {
        content: '""',
        display: "block",
        width: "100%",
        height: "var(--snow-size)",
        borderRadius: "50%",
        background: "#79a9ca",
        boxShadow: "0 0 3px rgba(60, 113, 150, 0.45)",
      },
    },
    [theme.breakpoints.down("sm")]: {"& span:nth-child(2n)": {display: "none"}},
    "@media (prefers-reduced-motion: reduce)": {display: "none"},
    "@media print": {display: "none"},
  },
  theme.applyStyles("dark", {
    "& span": {
      opacity: 0.55,
      "&::before": {background: "#fff", boxShadow: "0 0 2px #6695b8"},
    },
  }),
]);

export const NewYearGarlandRoot = styled("div")(({theme}) => ({
  position: "absolute",
  bottom: -9,
  left: 0,
  right: 0,
  height: 12,
  display: "flex",
  justifyContent: "space-around",
  borderTop: "2px solid #315b48",
  pointerEvents: "none",
  "& span": {
    position: "relative",
    width: 6,
    height: 9,
    borderRadius: "2px 2px 50% 50%",
    background: "currentColor",
    color: "#ffcf64",
    boxShadow: "0 2px 8px 1px currentColor",
    animation: `${glow} 4s var(--bulb-delay) ease-in-out infinite`,
    "&::before": {
      content: '""',
      position: "absolute",
      top: -3,
      left: 1,
      width: 4,
      height: 3,
      background: "#315b48",
    },
    "&:nth-child(4n + 2)": {color: "#ff7389"},
    "&:nth-child(4n + 3)": {color: "#7ee6bc"},
    "&:nth-child(4n)": {color: "#91caff"},
  },
  [theme.breakpoints.down("sm")]: {"& span:nth-child(2n)": {display: "none"}},
  "@media (prefers-reduced-motion: reduce)": {"& span": {animation: "none"}},
  "@media print": {display: "none"},
}));

export const NewYearHatRoot = styled("svg")({
  position: "absolute",
  width: "1.05em",
  height: "0.79em",
  top: "-0.36em",
  left: "0.35em",
  transformOrigin: "43% 80%",
  transform: "rotate(28deg)",
  pointerEvents: "none",
  filter: "drop-shadow(0 1px 1px rgba(0, 0, 0, 0.2))",
  "@media print": {display: "none"},
});
