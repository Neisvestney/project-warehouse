import {Alert, Chip, Paper, Stack, Typography} from "@mui/material";
import InfoRow from "@/components/InfoRow";
import UserChip from "@/components/shared/UserChip";
import {formatDateOnly} from "@/utils/dateOnly";
import SyncErrorAlert from "../../components/SyncErrorAlert";
import {
  MARKETPLACE_TYPE_LABELS,
  formatDateTime,
  MARKETPLACE_TYPE_COLORS,
  hasCapability,
} from "../../marketplaceUtils";
import type {MarketplaceAccountDto} from "@/api/types.gen";

interface AccountOverviewTabProps {
  account: MarketplaceAccountDto;
}

function AccountOverviewTab({account}: AccountOverviewTabProps) {
  const showBuyouts = hasCapability(account.capabilities, "buyouts");
  const journalFrom = account.accrualsJournalFrom;
  const loadedFrom = account.buyoutsLoadedFrom;
  // ISO dates compare as strings
  const buyoutsPending = showBuyouts && !!journalFrom && (!loadedFrom || loadedFrom > journalFrom);

  return (
    <Stack spacing={2}>
      {account.credentialsUnreadable && (
        <Alert severity="error">
          Сохранённый Api-Key не расшифровывается — кольцо ключей Data Protection потеряно. Введите
          ключ заново через «Изменить».
        </Alert>
      )}
      <SyncErrorAlert error={account.lastSyncError} />
      {buyoutsPending && journalFrom && (
        <Alert severity="info">
          Выкупы маркетплейсом загружены{" "}
          {loadedFrom ? `с ${formatDateOnly(loadedFrom)}` : "ещё не были"}, а журнал начислений
          начинается {formatDateOnly(journalFrom)}. У отчёта о выкупах жёсткий лимит запросов,
          поэтому история догружается фоновой синхронизацией по месяцу за запуск. До этого
          выкупленные площадкой отправления в аналитике выплат числятся неначисленными.
        </Alert>
      )}

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
          <InfoRow
            label="Авто-синхронизация"
            value={
              <Chip
                label={account.isActive ? "Включена" : "Отключена"}
                color={account.isActive ? "success" : "default"}
                size="small"
              />
            }
          />
          <InfoRow label="Последняя синхронизация" value={formatDateTime(account.lastSyncAt)} />
          <InfoRow label="Подключён" value={formatDateTime(account.createdAt)} />
          <InfoRow
            label="Кем подключён"
            value={
              account.createdByName ? (
                <UserChip userId={account.createdById} name={account.createdByName} />
              ) : (
                "—"
              )
            }
          />
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
          {showBuyouts && (
            <InfoRow
              label="Выкупы загружены с"
              value={loadedFrom ? formatDateOnly(loadedFrom) : "—"}
            />
          )}
        </Stack>
      </Paper>
    </Stack>
  );
}

export default AccountOverviewTab;
