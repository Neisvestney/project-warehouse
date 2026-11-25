import type {ComponentType} from "react";
import {useActiveSeasons} from "@/contexts/Seasonal/SeasonalContext";
import type {SeasonId} from "@/features/seasonal/seasons";
import {NewYearGarland, NewYearHat, NewYearSnow} from "./NewYearDecorations";

type DecorationSlot = "background" | "appBar" | "brand";

const decorations: Record<SeasonId, Partial<Record<DecorationSlot, ComponentType>>> = {
  newYear: {background: NewYearSnow, appBar: NewYearGarland, brand: NewYearHat},
};

export function SeasonalDecorationSlot({slot}: {slot: DecorationSlot}) {
  const activeSeasons = useActiveSeasons();

  return activeSeasons.map((season) => {
    const Decoration = decorations[season][slot];
    return Decoration ? <Decoration key={season} /> : null;
  });
}
