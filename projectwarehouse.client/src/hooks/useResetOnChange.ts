import {useState} from "react";

// State adjustment during render — cheaper than an effect and avoids a cascading re-render.
export function useResetOnChange<T>(value: T, reset: () => void) {
  const [prev, setPrev] = useState(value);

  if (prev !== value) {
    setPrev(value);
    reset();
  }
}
