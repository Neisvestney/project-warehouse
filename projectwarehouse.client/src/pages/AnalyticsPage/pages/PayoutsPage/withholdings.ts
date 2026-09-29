/** Withheld amounts are negative in the journal; the share reads as a positive part of the sales. */
export function withheldShare(
  withheld: number | null | undefined,
  sales: number | null | undefined,
) {
  return withheld == null || sales == null || sales <= 0 ? null : -withheld / sales;
}
