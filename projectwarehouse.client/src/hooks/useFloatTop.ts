import {useEffect, useState} from "react";
import {matchPath, useLocation} from "react-router";

const NAVBAR_HEIGHT = 50;
const FLOAT_GAP = 8;

const NO_NAVBAR_PAGES = ["/login"];

export function useFloatTop() {
  const location = useLocation();

  const navbar_height = NO_NAVBAR_PAGES.some((p) => matchPath(p, location.pathname))
    ? 0
    : NAVBAR_HEIGHT;

  const [scrollY, setScrollY] = useState(() => window.scrollY);
  useEffect(() => {
    const handler = () => setScrollY(window.scrollY);
    window.addEventListener("scroll", handler, {passive: true});
    return () => window.removeEventListener("scroll", handler);
  }, []);
  return Math.max(FLOAT_GAP, navbar_height + FLOAT_GAP - scrollY);
}
