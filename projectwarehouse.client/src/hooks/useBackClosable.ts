import {useEffect, useId, useRef} from "react";

const OVERLAY_KEY = "__overlay";

const isOverlayEntry = (state: unknown) =>
  !!state && typeof state === "object" && OVERLAY_KEY in state;

/**
 * Walks back off the entries a reload froze into the stack — the overlays that held them are gone
 * after a cold start, so the entries would otherwise eat one Back press each before the user
 * actually leaves the page. Each entry is stripped of its marker before being left behind, so a
 * Forward press cannot land on one still claiming to be held. Call before React mounts, alongside
 * `stripEphemeralSearchParams()`.
 */
export function dropOverlayHistoryEntries() {
  if (!isOverlayEntry(window.history.state)) return;

  const step = () => {
    const state = window.history.state;
    if (!isOverlayEntry(state)) {
      window.removeEventListener("popstate", step);
      return;
    }

    const next = {...state};
    delete next[OVERLAY_KEY];
    window.history.replaceState(next, "");
    window.history.back();
  };

  // Stacked overlays leave one entry each; every `back()` lands through `popstate`.
  window.addEventListener("popstate", step);
  step();
}

/**
 * An open overlay occupies its own history entry, so Back — the hardware button on the handheld
 * included — closes it instead of leaving the page.
 *
 * The entry is stamped with the hook instance's own id, so overlays stacked on top of each other
 * stay independent: a `popstate` reaches every listener, but only the one whose entry actually
 * went away closes. Router state is carried over, `idx` included — react-router reads it back on
 * `popstate`, and leaving it untouched keeps the extra entry invisible to the router.
 *
 * Two invariants the callers owe this hook:
 * - links inside the overlay navigate with `replace` (`<Link replace>` /
 *   `navigate(to, {replace: true})`), so the destination takes over the held entry instead of
 *   leaving a duplicate that swallows one Back press;
 * - that navigation is synchronous, which holds for the declarative router used in `main.tsx`.
 *   Under a data router with async loaders `onClose` would run before the entry is replaced, and
 *   the cleanup would back over a navigation still in flight.
 */
export function useBackClosable(open: boolean, onClose: () => void) {
  const id = useId();
  const onCloseRef = useRef(onClose);
  const pendingBackRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    onCloseRef.current = onClose;
  });

  useEffect(() => {
    if (!open) return;

    if (pendingBackRef.current !== null) {
      // The cleanup's Back has not gone out yet, so the entry it was about to drop is still ours.
      clearTimeout(pendingBackRef.current);
      pendingBackRef.current = null;
    } else {
      window.history.pushState({...window.history.state, [OVERLAY_KEY]: id}, "");
    }

    const handlePop = () => {
      if (window.history.state?.[OVERLAY_KEY] === id) return;
      onCloseRef.current();
    };
    window.addEventListener("popstate", handlePop);

    return () => {
      window.removeEventListener("popstate", handlePop);
      // Only our own entry is ours to drop — Back or a replace navigation may already have.
      if (window.history.state?.[OVERLAY_KEY] !== id) return;

      pendingBackRef.current = setTimeout(() => {
        pendingBackRef.current = null;
        if (window.history.state?.[OVERLAY_KEY] === id) window.history.back();
      });
    };
  }, [open, id]);
}
