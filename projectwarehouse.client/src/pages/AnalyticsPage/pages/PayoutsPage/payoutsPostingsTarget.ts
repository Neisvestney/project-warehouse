import type {PayoutsBucket} from "@/api/types.gen";

export interface PostingsTarget {
  bucket: PayoutsBucket;
  currencyCode: string;
  /** Absent for every selected shop together. */
  accountId?: string;
}

const BUCKETS: PayoutsBucket[] = [
  "inTransit",
  "inTransitOverdue",
  "deliveredNotAccrued",
  "notAccruedByMarketplace",
];

/** Kept in one search param, so the dialog survives a reload and closes with Back. */
export function encodePostingsTarget(target: PostingsTarget): string {
  return [target.bucket, target.currencyCode, target.accountId].filter(Boolean).join(":");
}

export function decodePostingsTarget(value: string | null): PostingsTarget | null {
  if (!value) return null;
  const [bucket, currencyCode, accountId] = value.split(":");
  if (!BUCKETS.includes(bucket as PayoutsBucket) || !currencyCode) return null;
  return {bucket: bucket as PayoutsBucket, currencyCode, accountId: accountId || undefined};
}
