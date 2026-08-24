import {useEffect, useId, useRef} from "react";

const OVERLAY_KEY = "__overlay";

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

  useEffect(() => {
    onCloseRef.current = onClose;
  });

  useEffect(() => {
    if (!open) return;

    window.history.pushState({...window.history.state, [OVERLAY_KEY]: id}, "");

    const handlePop = () => {
      if (window.history.state?.[OVERLAY_KEY] === id) return;
      onCloseRef.current();
    };
    window.addEventListener("popstate", handlePop);

    return () => {
      window.removeEventListener("popstate", handlePop);
      // Only our own entry is ours to drop — Back or a replace navigation may already have.
      if (window.history.state?.[OVERLAY_KEY] === id) window.history.back();
    };
  }, [open, id]);
}
