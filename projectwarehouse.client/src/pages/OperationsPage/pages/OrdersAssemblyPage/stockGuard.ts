import {createContext, useContext} from "react";
import {isShort} from "./fulfillmentStatus";

/** Dev escape hatch: while set, a cell short of stock no longer holds the row back. */
const IgnoreStockContext = createContext(false);

/** Same verdict as {@link isShort}, unless the surrounding dialog asked to ignore stock. */
function useIsShort(available: number | null, needQty?: number, needTimes = 1): boolean {
  const ignore = useContext(IgnoreStockContext);
  return !ignore && isShort(available, needQty, needTimes);
}

export {IgnoreStockContext, useIsShort};
