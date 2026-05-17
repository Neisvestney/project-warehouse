import {useNavigate, useSearchParams} from "react-router";

export function useDrawerSearchParamsState(name: string) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const selectedItemId = searchParams.get(name);

  const openDrawer = (id: string) => {
    const next = new URLSearchParams(searchParams);
    next.set(name, id);
    navigate(`?${next.toString()}`);
  };

  const closeDrawer = () => {
    if (!searchParams.has(name)) return;
    const next = new URLSearchParams(searchParams);
    next.delete(name);
    navigate(`?${next.toString()}`, {replace: true});
  };

  return [selectedItemId, openDrawer, closeDrawer] as const;
}
