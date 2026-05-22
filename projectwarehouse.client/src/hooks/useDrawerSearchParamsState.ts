import {useNavigate, useSearchParams} from "react-router";

export function useDrawerSearchParamsState(name: string) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const selectedItemId = searchParams.get(name);

  const openDrawer = (id: string) => {
    const next = new URLSearchParams(location.search);
    next.set(name, id);
    navigate(`?${next.toString()}`);
  };

  const closeDrawer = () => {
    console.log("close");
    const next = new URLSearchParams(location.search);
    if (!searchParams.has(name)) return;
    next.delete(name);
    navigate(`?${next.toString()}`, {replace: true});
  };

  return [selectedItemId, openDrawer, closeDrawer] as const;
}
