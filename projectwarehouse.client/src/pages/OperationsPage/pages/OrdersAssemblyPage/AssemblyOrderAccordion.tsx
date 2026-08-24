import {useMemo, useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Checkbox,
  css,
  styled,
  Tooltip,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {Link} from "react-router";
import type {OrderDetailsDto} from "@/api/types.gen";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import WarehouseChip from "@/components/shared/WarehouseChip";
import AssemblyOrderBoxesSection from "./AssemblyOrderBoxesSection";
import AssemblyTaskAccordion, {AssemblyTaskStatusChip} from "./AssemblyTaskAccordion";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import {checkBatchEligibility, getBatchDisabledReason} from "./batchEligibility";
import {getTaskProgress} from "@/components/orders/orderAssemblyUtils";
import {NOUNS, plural, pluralCount} from "@/utils/pluralUtils";
import {formatPostingNumber} from "@/utils/postingNumberUtils.tsx";

interface AssemblyOrderAccordionProps {
  order: OrderDetailsDto;
  canFulfill: boolean;
  selectedTaskIds: Set<string>;
  onTaskCheckChange: (orderId: string, taskId: string, checked: boolean) => void;
  eligibilityMap: Map<string, boolean>;
}

function AssemblyOrderAccordion({
  order,
  canFulfill,
  selectedTaskIds,
  onTaskCheckChange,
  eligibilityMap,
}: AssemblyOrderAccordionProps) {
  const tasks = order.assemblyTasks;

  const [expanded, setExpanded] = useState(false);

  const taskStates = useMemo(
    () =>
      tasks.map((task) => {
        const eligible = eligibilityMap.get(task.id) ?? checkBatchEligibility(task);
        return {task, eligible, disabledReason: getBatchDisabledReason(task, eligible)};
      }),
    [tasks, eligibilityMap],
  );

  // A lone task has no accordion of its own: its checkbox and status live in this summary and its
  // body is rendered inline below.
  const soleTask = tasks.length === 1 ? taskStates[0] : null;
  const soleTaskProgress = soleTask ? getTaskProgress(soleTask.task) : null;

  const selectable = taskStates.filter((s) => s.disabledReason === "");
  const selectedCount = selectable.filter((s) => selectedTaskIds.has(s.task.id)).length;
  const allSelected = selectable.length > 0 && selectedCount === selectable.length;

  const checkboxTitle = selectable.length
    ? soleTask
      ? ""
      : "Выбрать все задания заказа"
    : (soleTask?.disabledReason ?? "Нет заданий, доступных для массовой сборки");

  function handleToggleAll(checked: boolean) {
    for (const {task} of selectable) onTaskCheckChange(order.id, task.id, checked);
  }

  return (
    <Accordion expanded={expanded} onChange={(_, v) => setExpanded(v)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <SummaryUi>
          <SummaryHead>
            <Tooltip title={checkboxTitle}>
              <span>
                <Checkbox
                  size="small"
                  checked={allSelected}
                  indeterminate={selectedCount > 0 && !allSelected}
                  disabled={selectable.length === 0}
                  onChange={(e) => handleToggleAll(e.target.checked)}
                  onClick={(e) => e.stopPropagation()}
                  sx={{p: 0.5}}
                />
              </span>
            </Tooltip>

            <Typography
              variant="subtitle2"
              component={Link}
              to={`/operations/orders/${order.id}`}
              sx={{
                textDecoration: "none",
                color: "inherit",
                "&:hover": {textDecoration: "underline"},
              }}
              onClick={(event) => event.stopPropagation()}
            >
              {formatOrderNumber(order.number)}
            </Typography>

            <SummaryProgress variant="caption" color="text.secondary">
              {soleTaskProgress
                ? `${soleTaskProgress.fulfilled}/${soleTaskProgress.total} ${plural(soleTaskProgress.total, NOUNS.position)}`
                : pluralCount(tasks.length, NOUNS.task)}
            </SummaryProgress>
          </SummaryHead>

          <SummaryChips>
            <OrderTypeChip type={order.type} />
            <WarehouseChip warehouseId={order.warehouseId} name={order.warehouseName} />
            {order.marketplaceOrder && (
              <MarketplaceAccountChip
                accountId={order.marketplaceOrder.marketplaceAccountId}
                name={order.marketplaceOrder.marketplaceAccountName}
                type={order.marketplaceOrder.marketplaceType}
              />
            )}
            {soleTask && <AssemblyTaskStatusChip status={soleTask.task.status} />}
            {order.marketplaceOrder && (
              <SummaryPosting variant={"body2"}>
                {formatPostingNumber(order.marketplaceOrder.postingNumber)}
              </SummaryPosting>
            )}
          </SummaryChips>
        </SummaryUi>
      </AccordionSummary>

      <AccordionDetails sx={{pl: 2, pb: 2}}>
        <AssemblyOrderBoxesSection order={order} canManage={canFulfill} />
        {soleTask ? (
          <AssemblyTaskAccordion
            task={soleTask.task}
            orderId={order.id}
            orderBoxes={order.boxes}
            warehouseId={order.warehouseId}
            canFulfill={canFulfill}
            batchEligible={soleTask.eligible}
            inline
          />
        ) : (
          taskStates.map(({task, eligible}) => (
            <AssemblyTaskAccordion
              key={task.id}
              task={task}
              orderId={order.id}
              orderBoxes={order.boxes}
              warehouseId={order.warehouseId}
              canFulfill={canFulfill}
              checked={selectedTaskIds.has(task.id)}
              onCheckChange={(checked) => onTaskCheckChange(order.id, task.id, checked)}
              batchEligible={eligible}
            />
          ))
        )}
      </AccordionDetails>
    </Accordion>
  );
}

export default AssemblyOrderAccordion;

// The two groups collapse into the parent row from `md` up, so the summary stays a single line there;
// `order` keeps the progress and the posting number last in that line.
const SummaryUi = styled("div")(
  ({theme}) => css`
    display: flex;
    align-items: center;
    gap: ${theme.spacing(1.5)};
    flex: 1;
    padding-right: ${theme.spacing(1)};

    ${theme.breakpoints.down("md")} {
      flex-direction: column;
      align-items: stretch;
      gap: ${theme.spacing(0.75)};
    }
  `,
);

const SummaryHead = styled("div")(
  ({theme}) => css`
    display: contents;

    ${theme.breakpoints.down("md")} {
      display: flex;
      align-items: center;
      gap: ${theme.spacing(1.5)};
      min-width: 0;
    }
  `,
);

const SummaryChips = styled("div")(
  ({theme}) => css`
    display: contents;

    ${theme.breakpoints.down("md")} {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: ${theme.spacing(0.75)};
    }
  `,
);

const SummaryProgress = styled(Typography)(
  ({theme}) => css`
    margin-left: auto;
    white-space: nowrap;

    ${theme.breakpoints.up("md")} {
      order: 1;
    }
  `,
);

const SummaryPosting = styled(Typography)(
  ({theme}) => css`
    ${theme.breakpoints.up("md")} {
      order: 2;
    }
  `,
);
