import {Alert, Box, ButtonBase, Stack, Typography, alpha, useTheme} from "@mui/material";
import type {AbcClass, AbcDto, XyzClass} from "@/api/types.gen";
import {formatNumber} from "@/components/analytics/analyticsFormat";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {ABC_CLASSES, XYZ_CLASSES, formatBoundary} from "@/components/analytics/abc/abcClasses";

const STEP_UNITS = {
  week: {one: "полная неделя", few: "полные недели", many: "полных недель"},
  month: {one: "полный месяц", few: "полных месяца", many: "полных месяцев"},
};

interface AbcMatrixProps {
  data: AbcDto;
  abcClass: AbcClass | null;
  xyzClass: XyzClass | null;
  onSelect: (abc: AbcClass | null, xyz: XyzClass | null) => void;
}

/** A cell narrows the table to its pair; the classes themselves never change from a click. */
function AbcMatrix({data, abcClass, xyzClass, onSelect}: AbcMatrixProps) {
  const theme = useTheme();
  const {xyzBoundaryX, xyzBoundaryY, xyzStep, xyzMinIntervals} = data.settings;
  const xyzAvailable = data.xyzIntervals >= xyzMinIntervals;

  const count = (abc: AbcClass, xyz: XyzClass | null) =>
    data.matrix.find((c) => c.abcClass === abc && (c.xyzClass ?? null) === xyz)?.items ?? 0;
  const max = Math.max(1, ...data.matrix.filter((c) => c.xyzClass).map((c) => c.items));
  const unclassified = data.matrix.filter((c) => !c.xyzClass).reduce((s, c) => s + c.items, 0);

  if (!xyzAvailable) {
    return (
      <Alert severity="info">
        Для XYZ нужно минимум {pluralCount(xyzMinIntervals, STEP_UNITS[xyzStep])} до сегодняшнего
        дня, а в периоде их {data.xyzIntervals}. Выберите период длиннее — например, прошлый
        квартал.
      </Alert>
    );
  }

  return (
    <Stack spacing={1.5}>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: "28px repeat(3, minmax(0, 1fr))",
          gap: 0.5,
          textAlign: "center",
        }}
      >
        <Box />
        {XYZ_CLASSES.map((x) => (
          <Typography key={x} variant="subtitle2" color="text.secondary">
            {x.toUpperCase()}
          </Typography>
        ))}
        {ABC_CLASSES.map((a) => (
          <Box key={a} sx={{display: "contents"}}>
            <Typography
              variant="subtitle2"
              color="text.secondary"
              sx={{alignSelf: "center", textAlign: "center"}}
            >
              {a.toUpperCase()}
            </Typography>
            {XYZ_CLASSES.map((x) => {
              const items = count(a, x);
              const selected = abcClass === a && xyzClass === x;
              return (
                <ButtonBase
                  key={x}
                  onClick={() => (selected ? onSelect(null, null) : onSelect(a, x))}
                  aria-pressed={selected}
                  aria-label={`${a.toUpperCase()}${x.toUpperCase()}: ${pluralCount(items, NOUNS.position)}`}
                  sx={{
                    py: 1.5,
                    borderRadius: 1,
                    border: 1,
                    borderColor: selected ? "primary.main" : "transparent",
                    bgcolor: alpha(theme.palette.primary.main, 0.06 + (items / max) * 0.44),
                    typography: "body1",
                    fontVariantNumeric: "tabular-nums",
                  }}
                >
                  {formatNumber(items)}
                </ButtonBase>
              );
            })}
          </Box>
        ))}
      </Box>

      <Typography variant="caption" color="text.secondary">
        X ≤ {formatBoundary(xyzBoundaryX)}, Y ≤ {formatBoundary(xyzBoundaryY)}, Z — больше:
        коэффициент вариации продаж в штуках по полным {xyzStep === "week" ? "неделям" : "месяцам"}{" "}
        периода, их {data.xyzIntervals}.
        {unclassified > 0 &&
          ` Без класса XYZ — ${pluralCount(unclassified, NOUNS.position)}: ${data.xyzFromFirstSale ? "слишком новые, меньше " + xyzMinIntervals + " полных интервалов с первой продажи, или " : ""}продажи пришлись только на неполные края периода.`}
      </Typography>
    </Stack>
  );
}

export default AbcMatrix;
