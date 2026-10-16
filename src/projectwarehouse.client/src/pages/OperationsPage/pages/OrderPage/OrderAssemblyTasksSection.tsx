import {useState} from "react";
import {Button, Stack, Typography} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import type {OrderDetailsDto} from "@/api/types.gen";
import CreateAssemblyTaskDialog from "./CreateAssemblyTaskDialog";
import AssemblyTaskAccordionItem from "./AssemblyTaskAccordionItem";

interface OrderAssemblyTasksSectionProps {
  order: OrderDetailsDto;
  /** Creating, reassigning and deleting tasks. Needs orders.edit / orders.edit_assigned. */
  canEditTask: boolean;
  /** Moving task status and undoing fulfillments. Warehouse-bound for everyone. */
  canTransitionStatus: boolean;
}

function OrderAssemblyTasksSection({
  order,
  canEditTask,
  canTransitionStatus,
}: OrderAssemblyTasksSectionProps) {
  const [createOpen, setCreateOpen] = useState(false);

  const showSection = order.status === "assembly" || order.status === "assembled";
  if (!showSection) return null;

  return (
    <Stack spacing={1}>
      <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
        <Typography variant="subtitle1" sx={{fontWeight: 600}}>
          Задания на сборку
        </Typography>
        {canEditTask && order.status === "assembly" && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={() => setCreateOpen(true)}
          >
            Создать задание
          </Button>
        )}
      </Stack>

      {order.assemblyTasks.length === 0 && (
        <Typography variant="body2" color="text.secondary">
          Нет заданий
        </Typography>
      )}

      {order.assemblyTasks.map((task) => (
        <AssemblyTaskAccordionItem
          key={task.id}
          task={task}
          order={order}
          canEditTask={canEditTask}
          canTransitionStatus={canTransitionStatus}
        />
      ))}

      <CreateAssemblyTaskDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        order={order}
      />
    </Stack>
  );
}

export default OrderAssemblyTasksSection;
