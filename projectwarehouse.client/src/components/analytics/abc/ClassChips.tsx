import {Chip} from "@mui/material";
import type {AbcClass, XyzClass} from "@/api/types.gen";
import {ABC_COLORS, XYZ_COLORS} from "./abcClasses";

export function AbcChip({value}: {value: AbcClass}) {
  return <Chip size="small" label={value.toUpperCase()} color={ABC_COLORS[value]} />;
}

export function XyzChip({value}: {value: XyzClass | null | undefined}) {
  return value ? <Chip size="small" label={value.toUpperCase()} color={XYZ_COLORS[value]} /> : "—";
}
