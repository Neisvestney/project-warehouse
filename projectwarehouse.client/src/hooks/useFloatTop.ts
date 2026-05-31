import {useEffect, useState} from "react";

const NAVBAR_HEIGHT = 50;
const FLOAT_GAP = 8;

export function useFloatTop() {
  const [scrollY, setScrollY] = useState(() => window.scrollY);
  useEffect(() => {
    const handler = () => setScrollY(window.scrollY);
    window.addEventListener("scroll", handler, {passive: true});
    return () => window.removeEventListener("scroll", handler);
  }, []);
  return Math.max(FLOAT_GAP, NAVBAR_HEIGHT + FLOAT_GAP - scrollY);
}
