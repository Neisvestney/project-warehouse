import {type ReactNode, useRef, useState} from "react";
import {
  GlobalSearchContext,
  type GlobalSearchRequest,
} from "@/contexts/GlobalSearch/GlobalSearchContext";
import GlobalSearchModal, {
  type GlobalSearchModalRequest,
} from "@/components/GlobalSearch/GlobalSearchModal";

export function GlobalSearchProvider({children}: {children: ReactNode}) {
  const [request, setRequest] = useState<GlobalSearchModalRequest | null>(null);
  const seqRef = useRef(0);

  // A bare open (double Shift, app bar button) keeps an already open search and what was typed in it
  const openSearch = (next?: GlobalSearchRequest) => {
    seqRef.current += 1;
    const seq = seqRef.current;
    setRequest((prev) => (prev && !next ? prev : {...next, seq}));
  };
  const closeSearch = () => setRequest(null);

  return (
    <GlobalSearchContext.Provider value={{isOpen: !!request, openSearch, closeSearch}}>
      {children}
      <GlobalSearchModal request={request} onClose={closeSearch} />
    </GlobalSearchContext.Provider>
  );
}
