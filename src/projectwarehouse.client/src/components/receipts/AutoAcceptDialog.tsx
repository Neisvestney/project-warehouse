import {Alert, CircularProgress, Stack, Typography} from "@mui/material";
import {useMutation} from "@tanstack/react-query";
import {receiptsAutoAcceptMutation} from "@/api/@tanstack/react-query.gen";
import type {ReceiptDto} from "@/api/types.gen";
import ConfirmDialog from "@/components/ConfirmDialog";
import InfoRow from "@/components/InfoRow";
import {buildAutoAcceptPlan} from "@/components/receipts/receiptUtils";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDefaultStorageNodeQuery} from "@/hooks/useDefaultStorageNode";
import {extractErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

interface AutoAcceptDialogProps {
  open: boolean;
  onClose: () => void;
  receipt: ReceiptDto;
  onUpdate: (updated: ReceiptDto) => void;
  /** Lifted so the page can disable its own status actions while the receipt is being rewritten. */
  onPendingChange: (isPending: boolean) => void;
}

function AutoAcceptDialog({
  open,
  onClose,
  receipt,
  onUpdate,
  onPendingChange,
}: AutoAcceptDialogProps) {
  const plan = buildAutoAcceptPlan(receipt.items);
  const defaultNode = useDefaultStorageNodeQuery(receipt.warehouseId, open);

  const mutation = useMutation({
    ...receiptsAutoAcceptMutation(),
    meta: {suppressGlobalError: true},
    onMutate: () => onPendingChange(true),
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onSettled: () => onPendingChange(false),
  });

  const canRun = !!defaultNode.node && plan.affectedCount > 0;

  return (
    <ConfirmDialog
      open={open}
      onClose={() => {
        mutation.reset();
        onClose();
      }}
      title="Авто приёмка"
      confirmText="Принять и разместить"
      maxWidth="sm"
      onConfirm={() => mutation.mutate({path: {id: receipt.id}})}
      confirmDisabled={!canRun}
      isPending={mutation.isPending}
    >
      {defaultNode.isPending ? (
        <Stack sx={{alignItems: "center", py: 2}}>
          <CircularProgress size={28} />
        </Stack>
      ) : (
        <Stack spacing={1.5}>
          <Typography>
            Незаполненным позициям будет проставлено принятое количество, равное запланированному, а
            весь недостающий остаток отправится в дефолтную ячейку склада одной операцией.
          </Typography>

          {defaultNode.isError ? (
            <Alert severity="error">
              Не удалось получить ячейку склада: {extractErrorMessage(defaultNode.error)}
            </Alert>
          ) : !defaultNode.node ? (
            <Alert severity="error">
              У склада не назначена ячейка по умолчанию — авто приёмка недоступна. Назначьте её в
              настройках склада.
            </Alert>
          ) : (
            <>
              <InfoRow
                label="Ячейка"
                value={formatStoragePlaceNodeName(defaultNode.node.nodePath)}
              />
              <InfoRow
                label="Затронуто позиций"
                value={pluralCount(plan.affectedCount, NOUNS.position)}
              />
              <InfoRow
                label="Проставим количество"
                value={pluralCount(plan.filledCount, NOUNS.position)}
              />
              <InfoRow label="Разместим" value={`${plan.placedCount} шт.`} />

              {plan.affectedCount === 0 && (
                <Alert severity="info">
                  Все обычные позиции уже заполнены и размещены — размещать нечего.
                </Alert>
              )}
              {plan.unitCount > 0 && (
                <Alert severity="warning">
                  {pluralCount(plan.unitCount, NOUNS.position)} серийного типа авто приёмка не
                  трогает: им нужен инвентарный номер, разместите их вручную.
                </Alert>
              )}
              {plan.overplacedCount > 0 && (
                <Alert severity="warning">
                  {pluralCount(plan.overplacedCount, NOUNS.position)} размещено сверх принятого
                  количества — авто приёмка их пропустит, разберитесь с ними вручную.
                </Alert>
              )}
            </>
          )}

          {mutation.isError && (
            <Alert severity="error">{extractErrorMessage(mutation.error)}</Alert>
          )}
        </Stack>
      )}
    </ConfirmDialog>
  );
}

export default AutoAcceptDialog;
