import {useLayoutEffect} from "react";
import {useResolvedColorScheme} from "@/hooks/useResolvedColorScheme";

const SCHEME_ATTRIBUTE = "data-mui-color-scheme";

// Overrides the attribute MUI drives instead of calling setMode, so the user's saved preference is untouched.
export function useForcedLightScheme(): void {
  const {scheme} = useResolvedColorScheme();

  useLayoutEffect(() => {
    const root = document.documentElement;
    const force = () => {
      if (root.getAttribute(SCHEME_ATTRIBUTE) !== "light")
        root.setAttribute(SCHEME_ATTRIBUTE, "light");
    };
    force();
    // MUI rewrites the attribute on mount and whenever its colorScheme changes.
    const observer = new MutationObserver(force);
    observer.observe(root, {attributes: true, attributeFilter: [SCHEME_ATTRIBUTE]});
    return () => {
      observer.disconnect();
      root.setAttribute(SCHEME_ATTRIBUTE, scheme);
    };
  }, [scheme]);
}
