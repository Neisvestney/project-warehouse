import {useRef} from "react";
import {useNavigate, useSearchParams} from "react-router";

function currentHistoryIndex(): number | null {
  return (window.history.state as {idx?: number} | null)?.idx ?? null;
}

export function useDrawerSearchParamsState(name: string) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const historyIndexOnOpen = useRef<number | null>(null);

  const selectedItemId = searchParams.get(name);

  const openDrawer = (id: string) => {
    historyIndexOnOpen.current = currentHistoryIndex();
    const next = new URLSearchParams(location.search);
    next.set(name, id);
    navigate(`?${next.toString()}`);
  };

  const closeDrawer = () => {
    if (!searchParams.has(name)) return;
    const indexOnOpen = historyIndexOnOpen.current;
    const indexNow = currentHistoryIndex();
    historyIndexOnOpen.current = null;

    // The router's own index, not history.length: the latter counts forward entries and anything
    // an overlay pushed on top (see useBackClosable), and never shrinks on back. A delta that is
    // not positive means the recorded index went stale, so drop the param instead of guessing.
    if (indexOnOpen !== null && indexNow !== null && indexNow > indexOnOpen) {
      navigate(indexOnOpen - indexNow);
      return;
    }

    const next = new URLSearchParams(location.search);
    next.delete(name);
    navigate(`?${next.toString()}`, {replace: true});
  };

  return [selectedItemId, openDrawer, closeDrawer] as const;
}
