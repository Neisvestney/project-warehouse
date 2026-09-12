import {useState, type ReactNode} from "react";
import {Box, Checkbox, Collapse, IconButton, Stack, Tooltip, Typography} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import type {OrderDetailsDto} from "@/api/types.gen";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {checkBatchEligibility, getBatchDisabledReason} from "./batchEligibility";

interface AssemblyOrderGroupProps {
  label: string;
  orders: OrderDetailsDto[];
  canFulfill: boolean;
  selectedTaskIds: Set<string>;
  onTaskCheckChange: (orderId: string, taskId: string, checked: boolean) => void;
  eligibilityMap: Map<string, boolean>;
  children: ReactNode;
}

function AssemblyOrderGroup({
  label,
  orders,
  canFulfill,
  selectedTaskIds,
  onTaskCheckChange,
  eligibilityMap,
  children,
}: AssemblyOrderGroupProps) {
  const [expanded, setExpanded] = useState(false);

  const selectable = orders.flatMap((order) =>
    order.assemblyTasks
      .filter((task) => {
        const eligible = eligibilityMap.get(task.id) ?? checkBatchEligibility(task);
        return getBatchDisabledReason(task, eligible) === "";
      })
      .map((task) => ({orderId: order.id, taskId: task.id})),
  );
  const selectedCount = selectable.filter((s) => selectedTaskIds.has(s.taskId)).length;
  const allSelected = selectable.length > 0 && selectedCount === selectable.length;

  return (
    <Box>
      <Stack
        direction="row"
        spacing={1}
        onClick={() => setExpanded((v) => !v)}
        sx={{alignItems: "center", pt: 1, pl: 1, cursor: "pointer", userSelect: "none"}}
      >
        <IconButton
          size="small"
          aria-expanded={expanded}
          aria-label={expanded ? "Свернуть группу" : "Развернуть группу"}
          onClick={(e) => {
            e.stopPropagation();
            setExpanded((v) => !v);
          }}
          sx={{
            transform: expanded ? "rotate(0deg)" : "rotate(-90deg)",
            transition: (theme) => theme.transitions.create("transform"),
          }}
        >
          <ExpandMoreIcon fontSize="small" />
        </IconButton>
        {canFulfill && (
          <Tooltip
            title={
              selectable.length
                ? "Выбрать все задания группы"
                : "Нет заданий, доступных для массовой сборки"
            }
          >
            <span onClick={(e) => e.stopPropagation()}>
              <Checkbox
                size="small"
                checked={allSelected}
                indeterminate={selectedCount > 0 && !allSelected}
                disabled={selectable.length === 0}
                onChange={(e) => {
                  for (const s of selectable)
                    onTaskCheckChange(s.orderId, s.taskId, e.target.checked);
                }}
                sx={{p: 0.5}}
              />
            </span>
          </Tooltip>
        )}
        <Typography variant="subtitle1" sx={{fontWeight: 600, minWidth: 0}}>
          {label}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{whiteSpace: "nowrap"}}>
          {pluralCount(orders.length, NOUNS.order)}
        </Typography>
      </Stack>
      <Collapse in={expanded} unmountOnExit>
        <Stack spacing={2} sx={{pt: 1}}>
          {children}
        </Stack>
      </Collapse>
    </Box>
  );
}

export default AssemblyOrderGroup;
