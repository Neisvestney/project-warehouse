import {DependencyList, useEffect, useState} from "react";

export function useParamsState<D extends object, I extends object>(
  debouncedParams: D,
  debouncedDeps: DependencyList,
  immediateParams: I,
  debounceDelay = 300,
): D & I {
  const [settled, setSettled] = useState<D>(debouncedParams);

  useEffect(() => {
    const timer = setTimeout(() => setSettled(debouncedParams), debounceDelay);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, debouncedDeps);

  return {...settled, ...immediateParams} as D & I;
}
