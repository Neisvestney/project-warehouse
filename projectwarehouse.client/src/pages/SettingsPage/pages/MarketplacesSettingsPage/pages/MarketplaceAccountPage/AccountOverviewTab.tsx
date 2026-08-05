import {Alert, Chip, Paper, Stack, Typography} from "@mui/material";
import InfoRow from "@/components/InfoRow";
import SyncErrorAlert from "../../components/SyncErrorAlert";
import {
  MARKETPLACE_TYPE_LABELS,
  formatApiKeyMask,
  formatDateTime,
  MARKETPLACE_TYPE_COLORS
} from "../../marketplaceUtils";
import type {MarketplaceAccountDto} from "@/api/types.gen";

interface AccountOverviewTabProps {
  account: MarketplaceAccountDto;
}

function AccountOverviewTab({account}: AccountOverviewTabProps) {
  return (
    <Stack spacing={2}>
      {account.credentialsUnreadable && (
        <Alert severity="error">
          Сохранённый Api-Key не расшифровывается — кольцо ключей Data Protection потеряно. Введите
          ключ заново через «Изменить».
        </Alert>
      )}
      <SyncErrorAlert error={account.lastSyncError} />

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          <Typography variant="subtitle2" color="text.secondary">
            Подключение
          </Typography>
          <InfoRow
            label="Площадка"
            value={
              <Chip
                label={MARKETPLACE_TYPE_LABELS[account.type]}
                color={MARKETPLACE_TYPE_COLORS[account.type]}
                size="small"
              />
            }
          />
          <InfoRow label="Client-Id" value={account.externalClientId ?? "—"} />
          <InfoRow label="Ключ обновлён" value={formatDateTime(account.apiKeyUpdatedAt)} />
          <InfoRow label="Интервал, мин" value={String(account.syncIntervalMinutes)} />
          <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
            <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
              Авто-синхронизация
            </Typography>
            <Chip
              label={account.isActive ? "Включена" : "Отключена"}
              color={account.isActive ? "success" : "default"}
              size="small"
            />
          </Stack>
          <InfoRow label="Последняя синхронизация" value={formatDateTime(account.lastSyncAt)} />
          <InfoRow label="Подключён" value={formatDateTime(account.createdAt)} />
          <InfoRow label="Кем подключён" value={account.createdByName ?? "—"} />
        </Stack>
      </Paper>

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          <Typography variant="subtitle2" color="text.secondary">
            Реквизиты продавца
          </Typography>
          <InfoRow label="Юридическое лицо" value={account.companyLegalName ?? "—"} />
          <InfoRow label="ИНН" value={account.inn ?? "—"} />
          <InfoRow label="ОГРН" value={account.ogrn ?? "—"} />
          <InfoRow
            label="Форма собственности"
            value={<Chip label={account.ownershipForm ?? "—"} size="small" variant={"outlined"} />}
          />
        </Stack>
      </Paper>

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          <Typography variant="subtitle2" color="text.secondary">
            Данные синхронизации
          </Typography>
          <InfoRow label="Складов" value={String(account.warehouseCount)} />
          <InfoRow label="Складов без привязки" value={String(account.unmappedWarehouseCount)} />
          <InfoRow label="Карточек" value={String(account.cardCount)} />
          <InfoRow label="Не сопоставлено" value={String(account.unmappedCardCount)} />
        </Stack>
      </Paper>
    </Stack>
  );
}

export default AccountOverviewTab;
