import {useState} from "react";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import {useMutation} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {ordersGetAssemblyCheckPreviewMutation} from "@/api/@tanstack/react-query.gen";
import type {AssemblyCheckPreviewDto} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import {extractErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import BatchAssemblyDialog from "./assembly/BatchAssemblyDialog";
import {checkBatchEligibility, hasRemainingWork} from "./assembly/batchEligibility";
import type {SelectedTaskInfo} from "./assembly/batchGroups";

interface CheckBatch {
  tasks: SelectedTaskInfo[];
  notice?: string;
}

/** Splits the preview into what the dialog checks and a line on every order it had to leave out. */
function toCheckBatch(orderIds: string[], preview: AssemblyCheckPreviewDto): CheckBatch {
  const tasks: SelectedTaskInfo[] = [];
  const withUnit = new Set<string>();
  const withWork = new Set<string>();
  const previewed = new Set<string>();

  for (const {orderId, orderNumber, postingNumber, warehouseId, task} of preview.tasks) {
    previewed.add(orderId);
    if (!checkBatchEligibility(task)) {
      withUnit.add(orderId);
      continue;
    }
    if (!hasRemainingWork(task)) continue;
    withWork.add(orderId);
    tasks.push({
      orderId,
      taskId: task.id,
      task,
      warehouseId,
      orderNumber: `#${orderNumber}`,
      postingNumber,
    });
  }

  // an order goes into the dialog whole or not at all — a Unit task would leave it half-checked
  const kept = tasks.filter((t) => !withUnit.has(t.orderId));
  const otherStatus = orderIds.filter((id) => !previewed.has(id)).length;
  const assembled = [...previewed].filter((id) => !withUnit.has(id) && !withWork.has(id)).length;

  const parts = [
    otherStatus > 0 &&
      `не черновик, не подтверждён и не на сборке — ${pluralCount(otherStatus, NOUNS.order)}`,
    withUnit.size > 0 && `с единичными товарами — ${pluralCount(withUnit.size, NOUNS.order)}`,
    assembled > 0 && `уже собраны полностью — ${pluralCount(assembled, NOUNS.order)}`,
  ].filter((part) => part !== false);

  return {
    tasks: kept,
    notice: parts.length > 0 ? `Не проверяются: ${parts.join("; ")}.` : undefined,
  };
}

/** Bulk «Получится ли собрать?» action; `dialogs` must be rendered by the caller. */
export function useAssemblyCheckAction() {
  const {enqueueSnackbar} = useSnackbar();
  const [batch, setBatch] = useState<CheckBatch | null>(null);
  const [open, setOpen] = useState(false);

  const preview = useMutation({
    ...ordersGetAssemblyCheckPreviewMutation(),
    meta: {suppressGlobalError: true},
    onError: (error) => enqueueSnackbar(extractErrorMessage(error), {variant: "error"}),
  });

  function getAction(orderIds: string[]): BulkAction {
    return {
      key: "assemblyCheck",
      label: "Получится ли собрать?",
      icon: <FactCheckIcon />,
      disabled: orderIds.length === 0 || preview.isPending,
      pending: preview.isPending,
      // captured on click, so the dialog checks what was selected when it opened
      onClick: () =>
        preview.mutate(
          {body: {orderIds}},
          {
            onSuccess: (data) => {
              setBatch(toCheckBatch(orderIds, data));
              setOpen(true);
            },
          },
        ),
    };
  }

  const dialogs = (
    <BatchAssemblyDialog
      open={open}
      onClose={() => setOpen(false)}
      mode="check"
      selectedTasks={batch?.tasks ?? []}
      notice={batch?.notice}
    />
  );

  return {getAction, dialogs};
}
