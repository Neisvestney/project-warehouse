import {useEffect, useState} from "react";

const FLOAT_GAP = 8;

/** Set by whichever layout renders an app bar; absent means nothing occupies the top of the viewport. */
export const APP_BAR_HEIGHT_VAR = "--app-bar-height";

export function useFloatTop() {
  const [scrollY, setScrollY] = useState(() => window.scrollY);
  useEffect(() => {
    const handler = () => setScrollY(window.scrollY);
    window.addEventListener("scroll", handler, {passive: true});
    return () => window.removeEventListener("scroll", handler);
  }, []);

  return `max(${FLOAT_GAP}px, calc(var(${APP_BAR_HEIGHT_VAR}, 0px) + ${FLOAT_GAP}px - ${scrollY}px))`;
}
