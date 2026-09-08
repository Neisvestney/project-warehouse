import {useContext, useEffect, useRef, type ReactNode} from "react";
import {Box, Chip, Stack, Typography} from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import type {CatalogItemType} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {isComplete, type SlotStatus} from "./fulfillmentStatus";
import {TodoRegistryContext} from "./todoRegistry";

/** Marker width plus its gap — what the rows under the header indent by. */
const MARKER_INSET = "26px";

interface ComponentRowProps {
  name: string;
  catalogItemType?: CatalogItemType;
  multiplier?: number;
  status?: SlotStatus;
  /** Choices made on the way down a variation chain, e.g. ["Кожа", "Синий"]. */
  trail?: string[];
  /** Tail of the header line — shown once the row is filled: cell, stock, «Изменить». */
  summary?: ReactNode;
  /** The type-specific input, always under the name. */
  children?: ReactNode;
  /** Nested components, drawn on their own rail. */
  nested?: ReactNode;
  /** Inside a rail the row drops its own frame — the rail already groups it. */
  flat?: boolean;
  /** Highlighted as the row awaiting input. */
  active?: boolean;
}

function ComponentRow({
  name,
  catalogItemType,
  multiplier,
  status,
  trail,
  summary,
  children,
  nested,
  flat,
  active,
}: ComponentRowProps) {
  const registry = useContext(TodoRegistryContext);
  const rowRef = useRef<HTMLDivElement | null>(null);
  const done = status ? isComplete(status) : true;
  const partial = status ? status.filled > 0 && status.filled < status.total : false;

  useEffect(() => {
    const el = rowRef.current;
    if (!registry || !el || done) return;
    registry.add(el);
    return () => {
      registry.delete(el);
    };
  }, [registry, done]);

  const Marker = done
    ? CheckCircleOutlineIcon
    : active
      ? RadioButtonUncheckedIcon
      : ErrorOutlineIcon;

  return (
    <Stack
      ref={rowRef}
      spacing={0.75}
      sx={{
        p: flat ? 0 : 1,
        borderRadius: 1,
        border: flat ? undefined : "1px solid",
        borderLeftWidth: flat ? undefined : done ? 1 : 3,
        borderColor: flat ? undefined : done ? "divider" : active ? "primary.main" : "error.main",
      }}
    >
      {/* The marker sits inside the header row, so it centres on the row itself instead of
          being nudged into place with a margin. */}
      <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap", rowGap: 0.5}}>
        <Marker
          sx={{
            fontSize: 18,
            color: done ? "success.main" : active ? "text.disabled" : "error.main",
          }}
        />
        <Typography variant="body2" sx={{fontWeight: 500}}>
          {name}
        </Typography>
        {multiplier !== undefined && multiplier > 1 && (
          <Typography variant="caption" color="text.secondary">
            × {multiplier}
          </Typography>
        )}
        {catalogItemType && <CatalogItemTypeChip type={catalogItemType} />}
        {trail && trail.length > 0 && (
          <Typography variant="caption" color="text.secondary">
            → {trail.join(" → ")}
          </Typography>
        )}
        {partial && status && (
          <Chip size="small" color="error" label={`${status.filled} из ${status.total}`} />
        )}
        {summary}
      </Stack>
      {/* Everything under the header lines up with the name, not with the marker. */}
      {children && (
        <Stack spacing={1} sx={{pl: MARKER_INSET}}>
          {children}
        </Stack>
      )}
      {nested && (
        <Stack
          spacing={1}
          sx={{ml: MARKER_INSET, pl: 1.5, borderLeft: "2px solid", borderColor: "divider"}}
        >
          {nested}
        </Stack>
      )}
    </Stack>
  );
}

/** Caption naming what a rail holds, e.g. «Макси · комплект». */
function RailCaption({children}: {children: ReactNode}) {
  return (
    <Typography variant="caption" color="text.secondary" sx={{textTransform: "uppercase"}}>
      {children}
    </Typography>
  );
}

function NeedHint({children}: {children: ReactNode}) {
  return (
    <Typography variant="caption" color="error">
      {children}
    </Typography>
  );
}

function TodoRegistryProvider({
  registry,
  children,
}: {
  registry: Set<HTMLElement>;
  children: ReactNode;
}) {
  return <TodoRegistryContext value={registry}>{children}</TodoRegistryContext>;
}

/** Footer counter: how many slots are still empty, click jumps to the first one. */
function UnfilledCounter({status, onJump}: {status: SlotStatus; onJump: () => void}) {
  const left = status.total - status.filled;
  if (left <= 0) return null;
  return (
    <Box
      component="button"
      type="button"
      onClick={onJump}
      sx={{
        border: 0,
        background: "none",
        p: 0,
        cursor: "pointer",
        color: "error.main",
        font: "inherit",
        fontSize: 12,
        textDecoration: "underline dashed",
        textUnderlineOffset: 3,
      }}
    >
      состав не заполнен: {left}
    </Box>
  );
}

export {ComponentRow, NeedHint, RailCaption, TodoRegistryProvider, UnfilledCounter};
