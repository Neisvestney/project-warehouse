import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {Link} from "react-router";
import type {OrderDetailsDto} from "@/api/types.gen";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import AssemblyOrderBoxesSection from "./AssemblyOrderBoxesSection";
import AssemblyTaskAccordion from "./AssemblyTaskAccordion";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import {checkBatchEligibility} from "./batchEligibility";

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
  const [expanded, setExpanded] = useState(false);

  const tasks = order.assemblyTasks;

  return (
    <Accordion expanded={expanded} onChange={(_, v) => setExpanded(v)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" sx={{alignItems: "center", gap: 1.5, flex: 1, pr: 1}}>
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

          <Typography variant="caption" color="text.secondary" sx={{ml: "auto"}}>
            {tasks.length} заданий
          </Typography>
        </Stack>
      </AccordionSummary>

      <AccordionDetails sx={{pl: 2, pb: 2}}>
        <AssemblyOrderBoxesSection order={order} canManage={canFulfill} />
        {tasks.map((task) => {
          const eligible = eligibilityMap.get(task.id) ?? checkBatchEligibility(task);
          return (
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
          );
        })}
      </AccordionDetails>
    </Accordion>
  );
}

export default AssemblyOrderAccordion;
