import {createContext, useContext} from "react";
import type {UnitInventoryItemDto} from "@/api/types.gen";
import type {NodePick} from "./FulfillmentControls";
import type {VariantStep} from "./variationOptions";

/** What one slot of a composition remembers — only the user's choice, never the derived entry. */
interface SlotPick {
  node?: NodePick | null;
  unitItem?: UnitInventoryItemDto | null;
  variant?: VariantStep | null;
}

/**
 * Every pick of one composition, keyed by the slot's path down the tree. Flat and owned above the
 * tree, so folding the tree away and mounting it again restores the same choices.
 */
type CompositionPicks = Readonly<Record<string, SlotPick>>;

type PicksUpdate = (prev: CompositionPicks) => CompositionPicks;

const NO_PICKS: CompositionPicks = {};

/** Path segments are component ids, plus a variant id wherever a variation branches. */
function slotPath(parent: string, segment: string): string {
  return parent ? `${parent}/${segment}` : segment;
}

function withPick(picks: CompositionPicks, path: string, pick: SlotPick): CompositionPicks {
  return {...picks, [path]: {...picks[path], ...pick}};
}

/** Replaces a slot's pick and forgets everything filled in below it — a branch that no longer exists. */
function withBranchReplaced(
  picks: CompositionPicks,
  path: string,
  pick: SlotPick,
): CompositionPicks {
  const prefix = `${path}/`;
  const next: Record<string, SlotPick> = {};
  for (const [key, value] of Object.entries(picks)) {
    if (key !== path && !key.startsWith(prefix)) next[key] = value;
  }
  next[path] = pick;
  return next;
}

interface PicksStore {
  picks: CompositionPicks;
  setPick: (path: string, pick: SlotPick) => void;
  replaceBranch: (path: string, pick: SlotPick) => void;
}

const PicksContext = createContext<PicksStore>({
  picks: NO_PICKS,
  setPick: () => {},
  replaceBranch: () => {},
});

interface SlotPickHandle {
  pick: SlotPick | undefined;
  set: (pick: SlotPick) => void;
  /** Writes the pick and drops the subtree under it. */
  replace: (pick: SlotPick) => void;
}

function useSlotPick(path: string): SlotPickHandle {
  const {picks, setPick, replaceBranch} = useContext(PicksContext);
  return {
    pick: picks[path],
    set: (pick) => setPick(path, pick),
    replace: (pick) => replaceBranch(path, pick),
  };
}

export type {CompositionPicks, PicksUpdate, SlotPick};
export {NO_PICKS, PicksContext, slotPath, useSlotPick, withBranchReplaced, withPick};
