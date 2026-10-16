// Mirrors SearchExtensions on the backend: Cyrillic look-alikes are folded onto Latin ones.
const HOMOGLYPHS_FROM = "АаВвЕеКкМмНнОоРрСсТтУуХх";
const HOMOGLYPHS_TO = "AaBbEeKkMmHhOoPpCcTtYyXx";

export function normalizeSearch(value: string): string {
  let result = "";
  for (const char of value.toLowerCase()) {
    const idx = HOMOGLYPHS_FROM.indexOf(char);
    result += idx >= 0 ? HOMOGLYPHS_TO[idx] : char;
  }
  return result;
}

/** Normalized space-separated tokens of a search query; empty for a blank query. */
export function toSearchTokens(searchString: string): string[] {
  return normalizeSearch(searchString).split(" ").filter(Boolean);
}

/** Every token must be a substring of the haystack, which must already be normalized. */
export function matchesSearchTokens(
  normalizedHaystack: string,
  tokens: readonly string[],
): boolean {
  return tokens.every((token) => normalizedHaystack.includes(token));
}
