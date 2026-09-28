import {alpha, useTheme} from "@mui/material";
import {rainbowSurgePalette} from "@mui/x-charts/colorPalettes";
import {useQuery} from "@tanstack/react-query";
import {marketplacesGetAccountsShortOptions} from "@/api/@tanstack/react-query.gen";
import {useResolvedColorScheme} from "@/hooks/useResolvedColorScheme";

/**
 * Color of a channel, the same on every card. A shop keeps its place in the full list sorted by name, so
 * narrowing the channel filter does not repaint the ones that stay. Direct is a neutral grey that no
 * shop's palette color can be mistaken for.
 */
export function useChannelColor() {
  const theme = useTheme();
  const {scheme} = useResolvedColorScheme();
  const {data: accounts} = useQuery(marketplacesGetAccountsShortOptions());

  const palette = rainbowSurgePalette(scheme);
  const order = [...(accounts ?? [])]
    .sort((a, b) => a.name.localeCompare(b.name, "ru"))
    .map((a) => a.id);

  return (accountId: string | null | undefined) => {
    if (accountId == null) return theme.palette.grey[scheme === "dark" ? 400 : 600];
    // Until the list arrives no shop gets a palette color it would lose a moment later
    if (!accounts)
      return alpha(
        scheme === "dark" ? theme.palette.common.white : theme.palette.common.black,
        theme.palette.action.disabledOpacity,
      );
    const index = order.indexOf(accountId);
    return palette[(index >= 0 ? index : hashIndex(accountId)) % palette.length];
  };
}

function hashIndex(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i++) hash = (hash * 31 + value.charCodeAt(i)) >>> 0;
  return hash;
}
