import {useState} from "react";

export function useDrawerLocalState() {
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const openDrawer = (id: string) => setSelectedItemId(id);
  const closeDrawer = () => setSelectedItemId(null);

  return [selectedItemId, openDrawer, closeDrawer] as const;
}
