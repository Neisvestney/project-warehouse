import {useState} from "react";

// Keeps the last non-null value alive so closing UI can finish its exit animation
// with content still rendered; call `release` once the animation has ended.
export function useRetainedValue<T>(value: T | null | undefined) {
  const [retained, setRetained] = useState<T | null>(value ?? null);

  // Adjusting state during render: the only trigger is the incoming prop, so React re-runs the body
  // before committing and no effect round-trip is needed.
  if (value != null && value !== retained) {
    setRetained(value);
  }

  const release = () => setRetained(null);

  return [value ?? retained, release] as const;
}
