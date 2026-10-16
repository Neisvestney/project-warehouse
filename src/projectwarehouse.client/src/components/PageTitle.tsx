import {type ReactNode, useContext, useId, useLayoutEffect} from "react";
import {
  PageTitleDepthContext,
  PageTitleRegistryContext,
} from "@/contexts/PageTitle/PageTitleContext";

interface PageTitleProps {
  /** `undefined` or empty registers nothing, so an outer title shows while data is loading. */
  title?: string;
  /** Titles set inside `children` take priority over this one. */
  children?: ReactNode;
}

export default function PageTitle({title, children}: PageTitleProps) {
  const registry = useContext(PageTitleRegistryContext);
  const depth = useContext(PageTitleDepthContext);
  const id = useId();

  useLayoutEffect(() => {
    if (!registry || !title) return;
    return registry.register(id, depth, title);
  }, [registry, id, depth, title]);

  if (children === undefined) return null;

  return (
    <PageTitleDepthContext.Provider value={depth + 1}>{children}</PageTitleDepthContext.Provider>
  );
}
