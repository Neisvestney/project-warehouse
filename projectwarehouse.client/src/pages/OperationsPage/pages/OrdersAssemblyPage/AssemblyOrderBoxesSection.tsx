import {useState} from "react";
import {Alert, Chip, Stack, Typography} from "@mui/material";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersGetAllAssemblyQueryKey,
  ordersRemoveBoxMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {formatBoxLabel} from "@/components/orders/orderUtils";

interface AssemblyOrderBoxesSectionProps {
  order: OrderDetailsDto;
  canManage: boolean;
}

function AssemblyOrderBoxesSection({order, canManage}: AssemblyOrderBoxesSectionProps) {
  const queryClient = useQueryClient();
  const queryKey = ordersGetAllAssemblyQueryKey();

  const [error, setError] = useState<string | null>(null);

  const removeBoxMutation = useMutation({
    ...ordersRemoveBoxMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setError(null);
    },
    onError: () => setError("Не удалось удалить коробку — возможно, она не пуста"),
  });

  if (!canManage) return null;

  return (
    <Stack spacing={1} sx={{mb: 1}}>
      <Stack direction="row" sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
        <Typography variant="caption" color="text.secondary" sx={{fontWeight: 600}}>
          Коробки
        </Typography>
        {order.boxes.map((box) => (
          <Chip
            key={box.id}
            icon={<Inventory2OutlinedIcon fontSize="small" />}
            label={`${formatBoxLabel(box, order.boxes)} · ${box.components.length} поз.`}
            variant="outlined"
            size="small"
            onDelete={
              box.components.length === 0
                ? () => removeBoxMutation.mutate({path: {id: order.id, boxId: box.id}})
                : undefined
            }
            disabled={removeBoxMutation.isPending}
          />
        ))}
      </Stack>
      {error && <Alert severity="error">{error}</Alert>}
    </Stack>
  );
}

export default AssemblyOrderBoxesSection;
