import {useRef} from "react";
import {useNavigate, useSearchParams} from "react-router";

export function useDrawerSearchParamsState(name: string) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const historyLengthOnOpen = useRef<number | null>(null);

  const selectedItemId = searchParams.get(name);

  const openDrawer = (id: string) => {
    historyLengthOnOpen.current = window.history.length;
    const next = new URLSearchParams(location.search);
    next.set(name, id);
    navigate(`?${next.toString()}`);
  };

  const closeDrawer = () => {
    if (!searchParams.has(name)) return;
    if (historyLengthOnOpen.current !== null) {
      const delta = window.history.length - historyLengthOnOpen.current;
      historyLengthOnOpen.current = null;
      navigate(-delta);
    } else {
      const next = new URLSearchParams(location.search);
      next.delete(name);
      navigate(`?${next.toString()}`, {replace: true});
    }
  };

  return [selectedItemId, openDrawer, closeDrawer] as const;
}
