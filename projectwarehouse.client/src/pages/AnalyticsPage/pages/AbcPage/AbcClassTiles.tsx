import {Box, ButtonBase, Paper, Stack, Typography} from "@mui/material";
import type {AbcClass, AbcDto} from "@/api/types.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {formatBoundary} from "@/components/analytics/abc/abcClasses";
import {AbcChip} from "@/components/analytics/abc/ClassChips";

const LINES = {one: "строка", few: "строки", many: "строк"};

interface AbcClassTilesProps {
  data: AbcDto;
  selected: AbcClass | null;
  onSelect: (value: AbcClass | null) => void;
}

/** A tile narrows the table to its class, as a matrix row would; a second click lifts the filter. */
function AbcClassTiles({data, selected, onSelect}: AbcClassTilesProps) {
  const {abcBoundaryA, abcBoundaryB} = data.settings;
  const captions: Record<AbcClass, string> = {
    a: `до ${formatBoundary(abcBoundaryA)} объёма`,
    b: `следующие ${formatBoundary(abcBoundaryB - abcBoundaryA)}`,
    c: `последние ${formatBoundary(100 - abcBoundaryB)}`,
  };

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {xs: "repeat(2, minmax(0, 1fr))", md: "repeat(4, minmax(0, 1fr))"},
        gap: 2,
      }}
    >
      {data.classes.map((c) => (
        <Paper
          key={c.class}
          variant="outlined"
          sx={{borderColor: selected === c.class ? "primary.main" : undefined}}
        >
          <ButtonBase
            onClick={() => onSelect(selected === c.class ? null : c.class)}
            aria-pressed={selected === c.class}
            sx={{display: "block", width: "100%", height: "100%", textAlign: "left", p: 2}}
          >
            <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
              <AbcChip value={c.class} />
              <Typography variant="body2" color="text.secondary">
                {captions[c.class]}
              </Typography>
            </Stack>
            <Typography variant="h5" sx={{mt: 1}}>
              {pluralCount(c.items, NOUNS.position)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatPercent(c.itemsShare)} ассортимента · {formatPercent(c.valueShare)} объёма
            </Typography>
          </ButtonBase>
        </Paper>
      ))}
      <Paper variant="outlined" sx={{p: 2}}>
        <Typography variant="body2" color="text.secondary" sx={{lineHeight: "24px"}}>
          Не попали в анализ
        </Typography>
        <Typography variant="h5" sx={{mt: 1}}>
          {pluralCount(data.unlinkedLines, LINES)}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          товар площадки не привязан к каталогу
        </Typography>
      </Paper>
    </Box>
  );
}

export default AbcClassTiles;
