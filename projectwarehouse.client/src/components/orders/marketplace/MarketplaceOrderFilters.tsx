import {MenuItem, Select} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {marketplacesGetAccountsShortOptions} from "@/api/@tanstack/react-query.gen";
import type {MarketplaceOrderStatus, MarketplaceType} from "@/api/types.gen";
import {
  ALL_MARKETPLACE_ORDER_STATUSES,
  ALL_MARKETPLACE_TYPES,
  MARKETPLACE_LABELS,
  MARKETPLACE_ORDER_STATUS_LABELS,
} from "./marketplaceOrderUtils";

interface MarketplaceOrderFiltersProps {
  type: MarketplaceType | "";
  accountId: string | null;
  /** One pick sets either a whole marketplace or a single account; the other half is cleared. */
  onSourceChange: (type: MarketplaceType | "", accountId: string | null) => void;
  status: MarketplaceOrderStatus | "";
  onStatusChange: (value: MarketplaceOrderStatus | "") => void;
}

const TYPE_PREFIX = "type:";

function MarketplaceOrderFilters({
  type,
  accountId,
  onSourceChange,
  status,
  onStatusChange,
}: MarketplaceOrderFiltersProps) {
  const {data: accounts, isPending} = useQuery(marketplacesGetAccountsShortOptions());

  const account = accounts?.find((a) => a.id === accountId);
  // a deep link carries the id before the first list arrives; collapsing to "все" would misreport the filter
  const isUnresolvedAccount = accountId != null && !account && isPending;
  const sourceValue =
    account || isUnresolvedAccount ? accountId! : type ? `${TYPE_PREFIX}${type}` : "";

  function handleSourceChange(value: string) {
    if (!value) onSourceChange("", null);
    else if (value.startsWith(TYPE_PREFIX))
      onSourceChange(value.slice(TYPE_PREFIX.length) as MarketplaceType, null);
    else onSourceChange("", value);
  }

  function renderSource(value: string) {
    if (!value) return "Все маркетплейсы";
    if (value.startsWith(TYPE_PREFIX))
      return MARKETPLACE_LABELS[value.slice(TYPE_PREFIX.length) as MarketplaceType];
    return account ? `${MARKETPLACE_LABELS[account.type]} · ${account.name}` : "Загрузка…";
  }

  return (
    <>
      <Select
        value={sourceValue}
        onChange={(e) => handleSourceChange(e.target.value)}
        renderValue={renderSource}
        size="small"
        displayEmpty
        sx={{minWidth: 200}}
      >
        <MenuItem value="">Все маркетплейсы</MenuItem>
        {isUnresolvedAccount && <MenuItem value={accountId!}>Загрузка…</MenuItem>}
        {ALL_MARKETPLACE_TYPES.flatMap((t) => [
          <MenuItem key={t} value={`${TYPE_PREFIX}${t}`} sx={{fontWeight: 600}}>
            {MARKETPLACE_LABELS[t]}
          </MenuItem>,
          ...(accounts ?? [])
            .filter((a) => a.type === t)
            .map((a) => (
              <MenuItem key={a.id} value={a.id} sx={{pl: 4}}>
                {a.name}
              </MenuItem>
            )),
        ])}
      </Select>
      <Select
        value={status}
        onChange={(e) => onStatusChange(e.target.value as MarketplaceOrderStatus | "")}
        size="small"
        displayEmpty
        sx={{minWidth: 200}}
      >
        <MenuItem value="">Все статусы на площадке</MenuItem>
        {ALL_MARKETPLACE_ORDER_STATUSES.map((s) => (
          <MenuItem key={s} value={s}>
            {MARKETPLACE_ORDER_STATUS_LABELS[s]}
          </MenuItem>
        ))}
      </Select>
    </>
  );
}

export default MarketplaceOrderFilters;
