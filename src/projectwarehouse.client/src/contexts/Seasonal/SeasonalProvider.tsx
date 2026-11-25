import {useEffect, useState, type ReactNode} from "react";
import {isWithinSeason, seasons} from "@/features/seasonal/seasons";
import {SeasonalContext} from "./SeasonalContext";

export function SeasonalProvider({children}: {children: ReactNode}) {
  const [date, setDate] = useState(() => new Date());

  useEffect(() => {
    let timer: number | undefined;

    function schedule(now: Date) {
      window.clearTimeout(timer);
      const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
      timer = window.setTimeout(refresh, midnight.getTime() - now.getTime() + 50);
    }

    function refresh() {
      const now = new Date();
      setDate(now);
      schedule(now);
    }

    function handleVisibilityChange() {
      if (document.visibilityState === "visible") refresh();
    }

    schedule(new Date());
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, []);

  const activeSeasons = seasons.filter((season) => isWithinSeason(date, season)).map(({id}) => id);

  return <SeasonalContext.Provider value={activeSeasons}>{children}</SeasonalContext.Provider>;
}
