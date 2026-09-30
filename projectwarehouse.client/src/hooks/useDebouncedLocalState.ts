import {useEffect, useEffectEvent, useRef, useState} from "react";
import {useDebounce} from "@/hooks/useDebounce";

// T is constrained to primitives: a `committed` value that is a fresh object every render would make the
// external-change check below fire forever.
export function useDebouncedLocalState<T extends string | number | boolean | null | undefined>(
  committed: T,
  commit: (v: T) => void,
  delay = 300,
): [T, (v: T) => void, (v: T) => void] {
  const [localValue, setLocalValue] = useState(committed);
  const debouncedLocal = useDebounce(localValue, delay);

  // Sync local state when the committed value changes externally (browser back/forward, deep link).
  // Our own commits echo back here too, and react-router commits them inside a transition,
  // so the echo can land after the user typed more — ignoring it prevents losing characters.
  const prevCommitted = useRef(committed);
  const lastPushed = useRef<T | undefined>(undefined);
  useEffect(() => {
    if (prevCommitted.current !== committed) {
      prevCommitted.current = committed;
      if (Object.is(committed, lastPushed.current)) lastPushed.current = undefined;
      else setLocalValue(committed);
    }
  }, [committed]);

  function push(v: T) {
    if (Object.is(v, prevCommitted.current) || Object.is(v, lastPushed.current)) return;
    lastPushed.current = v;
    commit(v);
  }

  const onSettled = useEffectEvent(push);
  useEffect(() => {
    onSettled(debouncedLocal);
  }, [debouncedLocal]);

  function commitNow(v: T) {
    setLocalValue(v);
    push(v);
  }

  return [localValue, setLocalValue, commitNow];
}
