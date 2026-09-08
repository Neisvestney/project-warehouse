/** How many slots of a composition are filled — the source of every «N из M» in assembly. */
interface SlotStatus {
  filled: number;
  total: number;
}

const EMPTY_STATUS: SlotStatus = {filled: 0, total: 0};

function statusOf(filled: boolean): SlotStatus {
  return {filled: filled ? 1 : 0, total: 1};
}

function sumStatus(parts: Iterable<SlotStatus>): SlotStatus {
  let filled = 0;
  let total = 0;
  for (const part of parts) {
    filled += part.filled;
    total += part.total;
  }
  return {filled, total};
}

function isComplete(status: SlotStatus): boolean {
  return status.total > 0 && status.filled === status.total;
}

/** A known stock below what the row takes — the pick is doomed, so the row stays unfilled. */
function isShort(available: number | null, needQty?: number, needTimes = 1): boolean {
  return available !== null && needQty !== undefined && available < needQty * needTimes;
}

export type {SlotStatus};
export {EMPTY_STATUS, isComplete, isShort, statusOf, sumStatus};
