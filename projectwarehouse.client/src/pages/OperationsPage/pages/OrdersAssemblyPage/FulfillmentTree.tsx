import {useCallback, useEffect, useMemo, useRef, useState, type ReactNode} from "react";
import {Alert, CircularProgress, Stack} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {catalogGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import type {
  AddFulfillmentBundleComponentRequest,
  CatalogItemType,
  UnitInventoryItemDto,
} from "@/api/types.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDefaultStorageNode} from "@/hooks/useDefaultStorageNode";
import {useNodeItemCount} from "@/hooks/useNodeItemCount";
import {ComponentRow, RailCaption} from "./ComponentRow";
import {EMPTY_STATUS, isShort, statusOf, sumStatus, type SlotStatus} from "./fulfillmentStatus";
import {useVariationOptions} from "./variationOptions";
import {NodeControl, UnitPicker, VariantPicker, type NodePick} from "./FulfillmentControls";

type SlotChange = (entries: AddFulfillmentBundleComponentRequest[], status: SlotStatus) => void;

interface SlotProps {
  warehouseId: string;
  catalogItemId: string;
  catalogItemType: CatalogItemType;
  name: string;
  /** Pieces of this component in one assembled bundle. */
  multiplier: number;
  /** How many copies of the whole composition will be taken — batch assembly copies it per task. */
  needTimes?: number;
  onChange: SlotChange;
  flat?: boolean;
  /** Variant choices made on the way down to this row. */
  trail?: string[];
}

/** Cell state of a standard leaf, plus the entry it reports upward. */
function useStandardLeaf(
  warehouseId: string,
  catalogItemId: string,
  multiplier: number,
  onChange: SlotChange,
  needTimes = 1,
) {
  const [override, setOverride] = useState<NodePick | null>(null);
  const defaultNode = useDefaultStorageNode(warehouseId);
  // A fresh object every render would re-run the reporting effect forever.
  const node = useMemo(
    () =>
      override ??
      (defaultNode
        ? {nodeId: defaultNode.nodeId, nodePath: formatStoragePlaceNodeName(defaultNode.nodePath)}
        : null),
    [override, defaultNode],
  );

  const available = useNodeItemCount(node?.nodeId, catalogItemId);
  // A cell that cannot cover the row is not a filled slot — the server would reject it anyway.
  const ready = !!node && !isShort(available, multiplier, needTimes);

  useEffect(() => {
    onChange(
      node
        ? [
            {
              catalogItemId,
              sourceNodeId: node.nodeId,
              quantity: multiplier,
              unitInventoryItemId: null,
            },
          ]
        : [],
      statusOf(ready),
    );
  }, [catalogItemId, multiplier, node, ready, onChange]);

  return {
    node,
    setOverride,
    available,
    isDefault: !override && !!defaultNode,
    status: statusOf(ready),
  };
}

/** Instance state of a unit leaf, plus the entry it reports upward. */
function useUnitLeaf(onChange: SlotChange) {
  const [item, setItem] = useState<UnitInventoryItemDto | null>(null);

  useEffect(() => {
    onChange(
      item
        ? [
            {
              catalogItemId: item.catalogItem.id,
              sourceNodeId: item.nodeId,
              quantity: 0,
              unitInventoryItemId: item.id,
            },
          ]
        : [],
      statusOf(!!item),
    );
  }, [item, onChange]);

  return {item, setItem, status: statusOf(!!item)};
}

function StandardSlot({
  warehouseId,
  catalogItemId,
  name,
  multiplier,
  needTimes,
  onChange,
  flat,
  trail,
}: SlotProps) {
  const leaf = useStandardLeaf(warehouseId, catalogItemId, multiplier, onChange, needTimes);

  return (
    <ComponentRow
      name={name}
      catalogItemType="standard"
      multiplier={multiplier}
      status={leaf.status}
      flat={flat}
      trail={trail}
      summary={
        <NodeControl
          warehouseId={warehouseId}
          catalogItemId={catalogItemId}
          node={leaf.node}
          isDefault={leaf.isDefault}
          available={leaf.available}
          needQty={multiplier}
          needTimes={needTimes}
          onSelect={leaf.setOverride}
        />
      }
    />
  );
}

function UnitSlot({warehouseId, catalogItemId, name, onChange, flat, trail}: SlotProps) {
  const leaf = useUnitLeaf(onChange);

  return (
    <ComponentRow
      name={name}
      catalogItemType="unit"
      status={leaf.status}
      active={!leaf.item}
      flat={flat}
      trail={trail}
    >
      <UnitPicker
        catalogItemId={catalogItemId}
        warehouseId={warehouseId}
        value={leaf.item}
        onChange={leaf.setItem}
      />
    </ComponentRow>
  );
}

/**
 * Variation slot: pick a variant, then fill whatever it turned out to be. A leaf variant stays on
 * this row — the choice joins the trail, its cell becomes the tail; a variant that is itself a
 * bundle or a variation opens a rail below.
 */
function VariationSlot({
  warehouseId,
  catalogItemId,
  name,
  multiplier,
  needTimes,
  onChange,
  flat,
  trail,
}: SlotProps) {
  const [variantId, setVariantId] = useState<string | null>(null);
  const [variantType, setVariantType] = useState<CatalogItemType | null>(null);
  const [nestedStatus, setNestedStatus] = useState<SlotStatus>(EMPTY_STATUS);

  const options = useVariationOptions(catalogItemId);

  const handleNested = useCallback<SlotChange>(
    (entries, status) => {
      setNestedStatus(status);
      onChange(entries, status);
    },
    [onChange],
  );

  useEffect(() => {
    if (!variantId) onChange([], statusOf(false));
  }, [variantId, onChange]);

  if (options.isLoading) return <CircularProgress size={20} />;

  const variantName = options.items.find((o) => o.id === variantId)?.fullName;
  const chosenTrail = variantName ? [...(trail ?? []), variantName] : trail;

  const picker = (
    <VariantPicker
      label={name}
      options={options.items}
      value={variantId}
      onChange={(id) => {
        setVariantId(id);
        setVariantType(options.items.find((o) => o.id === id)?.type ?? null);
        setNestedStatus(EMPTY_STATUS);
        onChange([], statusOf(false));
      }}
    />
  );

  if (variantId && (variantType === "standard" || variantType === "unit")) {
    const leafProps = {
      key: variantId,
      warehouseId,
      catalogItemId: variantId,
      name,
      multiplier,
      needTimes,
      onChange,
      flat,
      trail: chosenTrail,
      picker,
    };
    return variantType === "standard" ? (
      <VariationStandardRow {...leafProps} />
    ) : (
      <VariationUnitRow {...leafProps} />
    );
  }

  return (
    <ComponentRow
      name={name}
      catalogItemType="variation"
      multiplier={multiplier}
      status={variantId ? nestedStatus : statusOf(false)}
      trail={chosenTrail}
      flat={flat}
      nested={
        variantId && variantType ? (
          <NestedVariant
            key={variantId}
            warehouseId={warehouseId}
            variantId={variantId}
            variantType={variantType}
            variantName={variantName ?? name}
            multiplier={multiplier}
            needTimes={needTimes}
            onChange={handleNested}
          />
        ) : undefined
      }
    >
      {picker}
    </ComponentRow>
  );
}

interface VariationLeafRowProps {
  warehouseId: string;
  catalogItemId: string;
  name: string;
  multiplier: number;
  needTimes?: number;
  onChange: SlotChange;
  flat?: boolean;
  trail?: string[];
  picker: ReactNode;
}

/** Chosen variant is a plain item: the choice and its cell share the variation's own row. */
function VariationStandardRow({
  warehouseId,
  catalogItemId,
  name,
  multiplier,
  needTimes,
  onChange,
  flat,
  trail,
  picker,
}: VariationLeafRowProps) {
  const leaf = useStandardLeaf(warehouseId, catalogItemId, multiplier, onChange, needTimes);

  return (
    <ComponentRow
      name={name}
      catalogItemType="variation"
      multiplier={multiplier}
      status={leaf.status}
      trail={trail}
      flat={flat}
    >
      {/* The variant decides which item the cell is even picked for, so it comes first. */}
      {picker}
      <NodeControl
        warehouseId={warehouseId}
        catalogItemId={catalogItemId}
        node={leaf.node}
        isDefault={leaf.isDefault}
        available={leaf.available}
        needQty={multiplier}
        needTimes={needTimes}
        onSelect={leaf.setOverride}
      />
    </ComponentRow>
  );
}

/** Chosen variant is a unit item: the instance picker sits under the choice. */
function VariationUnitRow({
  warehouseId,
  catalogItemId,
  name,
  multiplier,
  onChange,
  flat,
  trail,
  picker,
}: VariationLeafRowProps) {
  const leaf = useUnitLeaf(onChange);

  return (
    <ComponentRow
      name={name}
      catalogItemType="variation"
      multiplier={multiplier}
      status={leaf.status}
      trail={trail}
      flat={flat}
      active={!leaf.item}
    >
      {picker}
      <UnitPicker
        catalogItemId={catalogItemId}
        warehouseId={warehouseId}
        value={leaf.item}
        onChange={leaf.setItem}
      />
    </ComponentRow>
  );
}

interface NestedVariantProps {
  warehouseId: string;
  variantId: string;
  variantType: CatalogItemType;
  variantName: string;
  multiplier: number;
  needTimes?: number;
  onChange: SlotChange;
}

function NestedVariant({
  warehouseId,
  variantId,
  variantType,
  variantName,
  multiplier,
  needTimes,
  onChange,
}: NestedVariantProps) {
  return (
    <>
      <RailCaption>
        {variantName} · {variantType === "bundle" ? "комплект" : "вариация"}
      </RailCaption>
      {variantType === "bundle" ? (
        <BundleTree
          catalogItemId={variantId}
          warehouseId={warehouseId}
          multiplier={multiplier}
          needTimes={needTimes}
          onChange={onChange}
          flatRows
        />
      ) : (
        <VariationSlot
          warehouseId={warehouseId}
          catalogItemId={variantId}
          catalogItemType="variation"
          name={variantName}
          multiplier={multiplier}
          needTimes={needTimes}
          onChange={onChange}
          flat
        />
      )}
    </>
  );
}

function BundleSlot({
  warehouseId,
  catalogItemId,
  name,
  multiplier,
  needTimes,
  onChange,
  flat,
  trail,
}: SlotProps) {
  const [status, setStatus] = useState<SlotStatus>(EMPTY_STATUS);

  const handleChange = useCallback<SlotChange>(
    (entries, nested) => {
      setStatus(nested);
      onChange(entries, nested);
    },
    [onChange],
  );

  return (
    <ComponentRow
      name={name}
      catalogItemType="bundle"
      multiplier={multiplier}
      status={status}
      flat={flat}
      trail={trail}
      nested={
        <BundleTree
          catalogItemId={catalogItemId}
          warehouseId={warehouseId}
          multiplier={multiplier}
          needTimes={needTimes}
          onChange={handleChange}
          flatRows
        />
      }
    />
  );
}

function ComponentSlot(props: SlotProps) {
  switch (props.catalogItemType) {
    case "standard":
      return <StandardSlot {...props} />;
    case "unit":
      return <UnitSlot {...props} />;
    case "bundle":
      return <BundleSlot {...props} />;
    case "variation":
      return <VariationSlot {...props} />;
    default:
      return (
        <Alert severity="warning">
          Тип компонента «{props.name}» не поддерживается в составе комплекта
        </Alert>
      );
  }
}

interface BundleTreeProps {
  catalogItemId: string;
  warehouseId: string;
  multiplier?: number;
  needTimes?: number;
  onChange: SlotChange;
  /** Rows on a rail drop their own frame — the rail already groups them. */
  flatRows?: boolean;
  empty?: ReactNode;
}

/** Every component of one bundle, each reporting its own entries and progress upward. */
function BundleTree({
  catalogItemId,
  warehouseId,
  multiplier = 1,
  needTimes,
  onChange,
  flatRows,
}: BundleTreeProps) {
  const catalogQuery = useQuery(catalogGetByIdOptions({path: {id: catalogItemId}}));
  const catalogComponents = catalogQuery.data?.components;
  const components = useMemo(() => catalogComponents ?? [], [catalogComponents]);
  const entriesRef = useRef<Map<string, AddFulfillmentBundleComponentRequest[]>>(new Map());
  const statusRef = useRef<Map<string, SlotStatus>>(new Map());
  const slotKeys = useMemo(() => components.map((c) => c.componentId), [components]);

  const updateSlot = useCallback(
    (slotKey: string, entries: AddFulfillmentBundleComponentRequest[], status: SlotStatus) => {
      entriesRef.current.set(slotKey, entries);
      statusRef.current.set(slotKey, status);
      const rolled = sumStatus(
        slotKeys.map((key) => statusRef.current.get(key) ?? {filled: 0, total: 1}),
      );
      onChange(Array.from(entriesRef.current.values()).flat(), rolled);
    },
    [onChange, slotKeys],
  );

  if (catalogQuery.isLoading) return <CircularProgress size={20} />;

  return (
    <Stack spacing={1}>
      {components.map((comp) => (
        <SlotBinding
          key={comp.componentId}
          slotKey={comp.componentId}
          warehouseId={warehouseId}
          catalogItemId={comp.componentId}
          catalogItemType={comp.componentType}
          name={comp.componentName}
          multiplier={comp.quantity * multiplier}
          needTimes={needTimes}
          flat={flatRows}
          onSlotChange={updateSlot}
        />
      ))}
    </Stack>
  );
}

interface SlotBindingProps extends Omit<SlotProps, "onChange"> {
  slotKey: string;
  onSlotChange: (
    slotKey: string,
    entries: AddFulfillmentBundleComponentRequest[],
    status: SlotStatus,
  ) => void;
}

/** Keeps the per-slot callback identity stable — slots report from effects. */
function SlotBinding({slotKey, onSlotChange, ...slot}: SlotBindingProps) {
  const handleChange = useCallback<SlotChange>(
    (entries, status) => onSlotChange(slotKey, entries, status),
    [onSlotChange, slotKey],
  );
  return <ComponentSlot {...slot} onChange={handleChange} />;
}

export {BundleTree};
