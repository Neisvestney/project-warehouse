import {
  type PointerEvent as ReactPointerEvent,
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";

const TOUCH_RESTORE_MS = 2000;

/**
 * Lets a floating element step aside while the pointer is over it. Pure CSS cannot do this: the
 * moment `pointer-events` goes off the element stops matching `:hover`, the rule unapplies and the
 * two states flicker against each other.
 */
export function useFadeOnHover<T extends HTMLElement>() {
  const ref = useRef<T | null>(null);
  const [faded, setFaded] = useState(false);
  const restoreTimer = useRef<number | null>(null);

  useEffect(() => {
    if (!faded) return;

    // Out of hit testing means no `pointerleave` of its own, so the pointer is followed on the
    // document and tested against the rect instead.
    const onMove = (e: PointerEvent) => {
      const rect = ref.current?.getBoundingClientRect();
      if (!rect) return;
      const inside =
        e.clientX >= rect.left &&
        e.clientX <= rect.right &&
        e.clientY >= rect.top &&
        e.clientY <= rect.bottom;
      if (!inside) setFaded(false);
    };

    // A pointer that leaves the window stops reporting moves, so the rect test never runs again.
    const onLeave = () => setFaded(false);

    document.addEventListener("pointermove", onMove);
    document.addEventListener("pointerleave", onLeave);
    return () => {
      document.removeEventListener("pointermove", onMove);
      document.removeEventListener("pointerleave", onLeave);
    };
  }, [faded]);

  useEffect(
    () => () => {
      if (restoreTimer.current !== null) clearTimeout(restoreTimer.current);
    },
    [],
  );

  const onPointerEnter = useCallback((e: ReactPointerEvent<T>) => {
    setFaded(true);

    if (restoreTimer.current !== null) clearTimeout(restoreTimer.current);
    // A touch delivers the enter and then nothing, so a timer is the only way back. A mouse gets no
    // timer on purpose: it would restore the element under a pointer that can no longer enter it.
    if (e.pointerType === "mouse") return;
    restoreTimer.current = window.setTimeout(() => setFaded(false), TOUCH_RESTORE_MS);
  }, []);

  return {ref, faded, onPointerEnter};
}
