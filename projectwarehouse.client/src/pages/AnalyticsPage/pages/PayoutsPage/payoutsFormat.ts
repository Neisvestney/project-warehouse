import {formatMoney} from "@/components/analytics/analyticsFormat";

export function formatEstimate(amount: number | null | undefined, currencyCode: string): string {
  return amount == null ? "—" : `≈ ${formatMoney(amount, currencyCode)}`;
}

/** One label per age bucket: a boundary opens its own bucket, so there is one more bucket than boundaries. */
export function ageBucketLabels(boundaries: number[]): string[] {
  if (boundaries.length === 0) return [];
  return [
    `до ${boundaries[0]} дн.`,
    ...boundaries.slice(1).map((b, i) => `${boundaries[i]}–${b} дн.`),
    `${boundaries[boundaries.length - 1]}+ дн.`,
  ];
}

/** Lower bound of each age bucket in days, for telling the overdue ones apart. */
export function ageBucketStarts(boundaries: number[]): number[] {
  return [0, ...boundaries];
}
