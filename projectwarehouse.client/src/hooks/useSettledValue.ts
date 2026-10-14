 import {useRetainedValue} from "@/hooks/useRetainedValue";

// Holds the last value of a finished request while the next one is in flight, so a cached entry served on a
// key change never shows up before the fresh response.
export function useSettledValue<T>(value: T | null | undefined, fetching: boolean) {
  const [settled] = useRetainedValue(fetching ? null : value);
  // outside a request the raw value wins, so a disabled or failed query does not keep the last numbers
  return fetching ? (settled ?? undefined) : (value ?? undefined);
}
