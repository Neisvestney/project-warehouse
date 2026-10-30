import type {MarketplaceAccrualCategory} from "@/api/types.gen";

export const ACCRUAL_CATEGORY_LABELS: Record<MarketplaceAccrualCategory, string> = {
  sale: "Продажи",
  deliveryCharge: "Доставка от покупателя",
  compensation: "Компенсации",
  commission: "Комиссия",
  acquiring: "Эквайринг",
  logistics: "Логистика",
  returnLogistics: "Обратная логистика",
  processing: "Обработка",
  storage: "Хранение",
  advertising: "Реклама",
  services: "Доп. услуги",
  bonus: "Баллы и кэшбэк",
  penalty: "Штрафы",
  other: "Прочее",
  unknown: "Не распознано",
};
