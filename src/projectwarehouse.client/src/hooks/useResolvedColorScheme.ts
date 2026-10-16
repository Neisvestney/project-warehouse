import {useColorScheme} from "@mui/material/styles";

// `mode` stays "system" until the user picks a scheme, so `scheme` — the one actually rendered — comes
// from `systemMode` in that case. Both are typed optional for SSR, which `noSsr` on ThemeProvider rules out.
export function useResolvedColorScheme() {
  const colorScheme = useColorScheme();
  const {mode, systemMode} = colorScheme;

  return {...colorScheme, scheme: (mode === "system" ? systemMode : mode) ?? "light"};
}
