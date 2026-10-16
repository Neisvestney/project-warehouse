import {createContext, useState} from "react";

/** Unfilled rows register themselves here, so the footer counter can jump to the topmost one. */
const TodoRegistryContext = createContext<Set<HTMLElement> | null>(null);

function useTodoRegistry() {
  const [registry] = useState(() => new Set<HTMLElement>());

  const scrollToFirst = () => {
    let topmost: HTMLElement | null = null;
    let topY = Number.POSITIVE_INFINITY;
    for (const el of registry) {
      const y = el.getBoundingClientRect().top;
      if (y < topY) {
        topY = y;
        topmost = el;
      }
    }
    topmost?.scrollIntoView({behavior: "smooth", block: "center"});
  };

  return {registry, scrollToFirst};
}

export {TodoRegistryContext, useTodoRegistry};
