import {MenuItem, Select} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
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
  onTypeChange: (value: MarketplaceType | "") => void;
  accountId: string | null;
  onAccountChange: (value: string | null) => void;
  status: MarketplaceOrderStatus | "";
  onStatusChange: (value: MarketplaceOrderStatus | "") => void;
}

function MarketplaceOrderFilters({
  type,
  onTypeChange,
  accountId,
  onAccountChange,
  status,
  onStatusChange,
}: MarketplaceOrderFiltersProps) {
  const {data: accounts, isPending} = useQuery({
    // the previous list stays on screen while the new marketplace's accounts load
    ...marketplacesGetAccountsShortOptions({query: {type: type || undefined}}),
    placeholderData: keepPreviousData,
  });

  const isKnownAccount = accounts?.some((a) => a.id === accountId) ?? false;
  // a deep link carries the id before the first list arrives; collapsing to "все" would misreport the filter
  const isUnresolvedAccount = accountId != null && !isKnownAccount && isPending;
  // an id left over from another marketplace would put the Select out of range until the reset lands
  const accountValue = isKnownAccount || isUnresolvedAccount ? accountId! : "";

  return (
    <>
      <Select
        value={type}
        onChange={(e) => onTypeChange(e.target.value as MarketplaceType | "")}
        size="small"
        displayEmpty
        sx={{minWidth: 160}}
      >
        <MenuItem value="">Все маркетплейсы</MenuItem>
        {ALL_MARKETPLACE_TYPES.map((t) => (
          <MenuItem key={t} value={t}>
            {MARKETPLACE_LABELS[t]}
          </MenuItem>
        ))}
      </Select>
      <Select
        value={accountValue}
        onChange={(e) => onAccountChange(e.target.value || null)}
        size="small"
        displayEmpty
        sx={{minWidth: 180}}
      >
        <MenuItem value="">Все аккаунты</MenuItem>
        {isUnresolvedAccount && <MenuItem value={accountId!}>Загрузка…</MenuItem>}
        {accounts?.map((a) => (
          <MenuItem key={a.id} value={a.id}>
            {a.name}
          </MenuItem>
        ))}
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
