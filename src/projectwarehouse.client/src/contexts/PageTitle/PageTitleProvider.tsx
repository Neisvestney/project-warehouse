import {type ReactNode, useLayoutEffect, useState} from "react";
import {
  type PageTitleRegistry,
  PageTitleRegistryContext,
} from "@/contexts/PageTitle/PageTitleContext";

const APP_TITLE = import.meta.env.DEV ? "PW DEV" : "Project Warehouse";

interface TitleEntry {
  id: string;
  depth: number;
  title: string;
}

function pickTitle(entries: TitleEntry[]): string | undefined {
  let best: TitleEntry | undefined;
  for (const entry of entries) {
    if (!best || entry.depth >= best.depth) best = entry;
  }
  return best?.title;
}

export function PageTitleProvider({children}: {children: ReactNode}) {
  const [entries, setEntries] = useState<TitleEntry[]>([]);
  // Created once: consumers list the registry in effect deps, a new identity would re-register in a loop.
  const [registry] = useState<PageTitleRegistry>(() => ({
    register: (id, depth, title) => {
      setEntries((prev) => [...prev.filter((e) => e.id !== id), {id, depth, title}]);
      return () => setEntries((prev) => prev.filter((e) => e.id !== id));
    },
  }));

  const title = pickTitle(entries);

  useLayoutEffect(() => {
    document.title = title ? `${title} · ${APP_TITLE}` : APP_TITLE;
  }, [title]);

  return (
    <PageTitleRegistryContext.Provider value={registry}>
      {children}
    </PageTitleRegistryContext.Provider>
  );
}
