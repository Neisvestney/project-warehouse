import {useEffect, useRef, useState} from "react";
import {Chip, Divider, Popover, Skeleton, Stack, Typography} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {ordersGetCompositionPreviewOptions} from "@/api/@tanstack/react-query.gen";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {formatBoxLabel} from "./orderUtils";
import type {OrderCompositionPreviewComponentDto, OrderSummaryDto} from "@/api/types.gen";

const OPEN_DELAY_MS = 250;
/** Long enough to travel the gap between the chip and the paper without the popover snapping shut. */
const CLOSE_DELAY_MS = 150;

interface OrderCompositionPreviewProps {
  order: OrderSummaryDto;
}

function OrderCompositionPreview({order}: OrderCompositionPreviewProps) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [requested, setRequested] = useState(false);
  const openTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const closeTimer = useRef<ReturnType<typeof setTimeout>>(undefined);

  const {data, isPending, isError} = useQuery({
    ...ordersGetCompositionPreviewOptions({path: {id: order.id}}),
    // the preview is a read-only snapshot, so a minute of staleness beats refetching on every hover
    staleTime: 60_000,
    enabled: requested,
  });

  useEffect(
    () => () => {
      clearTimeout(openTimer.current);
      clearTimeout(closeTimer.current);
    },
    [],
  );

  function scheduleOpen(target: HTMLElement) {
    clearTimeout(openTimer.current);
    clearTimeout(closeTimer.current);
    openTimer.current = setTimeout(() => {
      setRequested(true);
      setAnchorEl(target);
    }, OPEN_DELAY_MS);
  }

  function scheduleClose() {
    clearTimeout(openTimer.current);
    closeTimer.current = setTimeout(() => setAnchorEl(null), CLOSE_DELAY_MS);
  }

  function cancelClose() {
    clearTimeout(closeTimer.current);
  }

  if (!order.boxCount) return <>—</>;

  const boxes = data?.boxes ?? [];
  const showBoxLabels = (data?.boxCount ?? 0) > 1;

  return (
    <>
      <Chip
        variant="outlined"
        size="small"
        label={order.componentCount}
        tabIndex={0}
        aria-label={`Состав заказа, ${order.componentCount.toLocaleString("ru-RU")} шт`}
        onMouseEnter={(e) => scheduleOpen(e.currentTarget)}
        onMouseLeave={scheduleClose}
        onFocus={(e) => scheduleOpen(e.currentTarget)}
        onBlur={scheduleClose}
        onKeyDown={(e) => e.key === "Escape" && setAnchorEl(null)}
        // the row-wide link overlay sits underneath and would swallow the hover otherwise
        sx={{position: "relative", zIndex: 1, cursor: "help"}}
      />
      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{vertical: "bottom", horizontal: "left"}}
        transformOrigin={{vertical: "top", horizontal: "left"}}
        disableScrollLock
        disableAutoFocus
        disableEnforceFocus
        disableRestoreFocus
        slotProps={{
          // the invisible backdrop must not eat hover on the rest of the table
          root: {sx: {pointerEvents: "none"}},
          paper: {
            onMouseEnter: cancelClose,
            onMouseLeave: scheduleClose,
            sx: {pointerEvents: "auto", minWidth: 280, maxWidth: 400},
          },
        }}
      >
        <Stack sx={{p: 1.5}} spacing={1}>
          <Stack
            direction="row"
            spacing={1}
            sx={{justifyContent: "space-between", alignItems: "baseline"}}
          >
            <Typography variant="subtitle2">Состав заказа</Typography>
            {data && (
              <Typography variant="caption" color="text.secondary" noWrap>
                {pluralCount(data.positionCount, NOUNS.position)}
              </Typography>
            )}
          </Stack>
          <Divider />

          {isError ? (
            <Typography variant="body2" color="error">
              Не удалось загрузить состав
            </Typography>
          ) : isPending || !data ? (
            <Stack spacing={0.5}>
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} variant="text" height={20} />
              ))}
            </Stack>
          ) : (
            <Stack spacing={0.75}>
              {boxes.map(
                (box) =>
                  box.components.length > 0 && (
                    <Stack key={box.id} spacing={0.25}>
                      {showBoxLabels && (
                        <Typography variant="caption" color="text.secondary">
                          {formatBoxLabel(box, boxes)}
                        </Typography>
                      )}
                      {box.components.map((component) => (
                        <ComponentRow key={component.catalogItemId} component={component} />
                      ))}
                    </Stack>
                  ),
              )}
              {data.hiddenPositionCount > 0 && (
                <Typography variant="caption" color="text.secondary">
                  … ещё {pluralCount(data.hiddenPositionCount, NOUNS.position)}
                </Typography>
              )}
            </Stack>
          )}

          {data && (
            <>
              <Divider />
              <Typography variant="caption" color="text.secondary">
                {pluralCount(data.boxCount, NOUNS.box)} ·{" "}
                {data.totalQuantity.toLocaleString("ru-RU")} шт
              </Typography>
            </>
          )}
        </Stack>
      </Popover>
    </>
  );
}

interface ComponentRowProps {
  component: OrderCompositionPreviewComponentDto;
}

function ComponentRow({component}: ComponentRowProps) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <Typography variant="body2" noWrap sx={{flexGrow: 1, minWidth: 0}}>
        {component.catalogItemName}
      </Typography>
      <Typography
        variant="body2"
        color="text.secondary"
        sx={{fontVariantNumeric: "tabular-nums", flexShrink: 0}}
      >
        ×{component.quantity.toLocaleString("ru-RU")}
      </Typography>
    </Stack>
  );
}

export default OrderCompositionPreview;
