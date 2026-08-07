import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import type {AppFieldError, MarketplaceSyncRunDto} from "@/api/types.gen";
import {resolveErrorMessage} from "@/utils/errorUtils";
import SkippedOrdersList from "./SkippedOrdersList";
import {RUN_STATUS_COLORS, RUN_STATUS_LABELS} from "./marketplaceOrderUtils";

interface SyncOrdersAccountAccordionProps {
  accountName: string;
  run?: MarketplaceSyncRunDto;
  /** Set when the account was rejected outright and no run was ever started. */
  rejection?: AppFieldError;
}

function SyncOrdersAccountAccordion({
  accountName,
  run,
  rejection,
}: SyncOrdersAccountAccordionProps) {
  if (rejection) {
    return (
      <Accordion disabled expanded={false} disableGutters>
        <AccordionSummary>
          <Stack direction="row" spacing={1} sx={{width: "100%", alignItems: "center"}}>
            <Typography variant="body2" sx={{flexGrow: 1}}>
              {accountName}
            </Typography>
            <Typography variant="caption" color="error">
              {resolveErrorMessage(rejection)}
            </Typography>
          </Stack>
        </AccordionSummary>
      </Accordion>
    );
  }

  const summary = run
    ? `создано ${run.ordersCreated}, обновлено ${run.ordersUpdated}, пропущено ${run.ordersSkipped}`
    : "ожидание…";

  return (
    <Accordion disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" spacing={1} sx={{width: "100%", pr: 1, alignItems: "center"}}>
          <Typography variant="body2" sx={{flexGrow: 1}}>
            {accountName}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {summary}
          </Typography>
          {run && (
            <Chip
              size="small"
              label={RUN_STATUS_LABELS[run.status]}
              color={RUN_STATUS_COLORS[run.status]}
            />
          )}
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        {run ? (
          <Stack spacing={1}>
            <Typography variant="caption" color="text.secondary">
              Обработано отправлений: {run.ordersProcessed}
            </Typography>
            {run.error && (
              <Typography variant="caption" color="error">
                {resolveErrorMessage(run.error)}
              </Typography>
            )}
            <SkippedOrdersList items={run.skippedOrders ?? []} total={run.ordersSkipped} />
          </Stack>
        ) : (
          <Typography variant="caption" color="text.secondary">
            Прогон ещё не начался.
          </Typography>
        )}
      </AccordionDetails>
    </Accordion>
  );
}

export default SyncOrdersAccountAccordion;
