import {useEffect} from "react";
import {useResolvedColorScheme} from "@/hooks/useResolvedColorScheme.ts";
import {APP_BAR_DARK_BG, APP_BAR_LIGHT_BG} from "@/theme.ts";

const SCHEME_COLORS = {light: APP_BAR_LIGHT_BG, dark: APP_BAR_DARK_BG};

// index.html ships one meta per OS scheme so the status bar is right before React mounts; an explicit
// choice no longer follows the OS, so both tags collapse onto the picked color until it goes back to system.
function ThemeColorMeta() {
  const {mode, scheme} = useResolvedColorScheme();

  useEffect(() => {
    const metas = document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]');

    metas.forEach((meta) => {
      const osScheme = meta.media.includes("dark") ? "dark" : "light";
      meta.content = SCHEME_COLORS[mode === "system" ? osScheme : scheme];
    });
  }, [mode, scheme]);

  return null;
}

export default ThemeColorMeta;
