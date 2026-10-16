import {useContext} from "react";
import RealtimeContext, {type RealtimeContextValue} from "@/contexts/Realtime/RealtimeContext";

export function useRealtime(): RealtimeContextValue {
  const ctx = useContext(RealtimeContext);
  if (!ctx) throw new Error("useRealtime must be used inside RealtimeProvider");
  return ctx;
}
