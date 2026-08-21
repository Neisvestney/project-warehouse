import {useMemo, useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Checkbox,
  Chip,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {Link} from "react-router";
import type {OrderDetailsDto} from "@/api/types.gen";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
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
        <Stack direction="row" sx={{alignItems: "center", gap: 1.5, flex: 1, pr: 1}}>
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

          <OrderTypeChip type={order.type} />
          <Chip label={order.warehouseName} size="small" variant="outlined" />
          {order.marketplaceOrder && (
            <MarketplaceAccountChip
              accountId={order.marketplaceOrder.marketplaceAccountId}
              name={order.marketplaceOrder.marketplaceAccountName}
              type={order.marketplaceOrder.marketplaceType}
            />
          )}
          {soleTask && <AssemblyTaskStatusChip status={soleTask.task.status} />}

          <Typography variant="caption" color="text.secondary" sx={{ml: "auto"}}>
            {soleTaskProgress
              ? `${soleTaskProgress.fulfilled}/${soleTaskProgress.total} ${plural(soleTaskProgress.total, NOUNS.position)}`
              : pluralCount(tasks.length, NOUNS.task)}
          </Typography>
          {order.marketplaceOrder && (
            <Typography variant={"body2"}>
              {formatPostingNumber(order.marketplaceOrder.postingNumber)}
            </Typography>
          )}
        </Stack>
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
