import {CircularProgress, Stack} from "@mui/material";
import {VariantPicker} from "./FulfillmentControls";
import {useVariationOptions, type VariantStep} from "./variationOptions";

interface VariationChainProps {
  rootCatalogItemId: string;
  chain: VariantStep[];
  onChange: (chain: VariantStep[]) => void;
  label?: string;
}

/**
 * A variation whose variant may again be a variation: one picker per level, each level cutting the
 * chain below it. The chain ends on a `standard`, `unit` or `bundle` step — that is what moves stock.
 */
function VariationChain({
  rootCatalogItemId,
  chain,
  onChange,
  label = "Вариант",
}: VariationChainProps) {
  return (
    <VariationLevel
      catalogItemId={rootCatalogItemId}
      level={0}
      chain={chain}
      onChange={onChange}
      label={label}
    />
  );
}

interface VariationLevelProps extends Omit<VariationChainProps, "rootCatalogItemId"> {
  catalogItemId: string;
  level: number;
}

function VariationLevel({catalogItemId, level, chain, onChange, label}: VariationLevelProps) {
  const options = useVariationOptions(catalogItemId);
  const step = chain[level] ?? null;

  if (options.isLoading) return <CircularProgress size={20} />;

  return (
    <Stack spacing={1}>
      <VariantPicker
        label={label ?? "Вариант"}
        options={options.items}
        value={step?.id ?? null}
        onChange={(id) => {
          const variant = options.items.find((o) => o.id === id);
          if (!variant) return;
          onChange([...chain.slice(0, level), {id, type: variant.type}]);
        }}
      />
      {step?.type === "variation" && (
        <Stack spacing={1} sx={{pl: 1.5, borderLeft: "2px solid", borderColor: "divider"}}>
          <VariationLevel
            catalogItemId={step.id}
            level={level + 1}
            chain={chain}
            onChange={onChange}
            label={label}
          />
        </Stack>
      )}
    </Stack>
  );
}

export {VariationChain};
