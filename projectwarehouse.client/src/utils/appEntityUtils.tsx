import type {
  AppEntity,
  AppEntityType,
  CatalogItemType,
  MarketplaceType,
  ReceiptStatus,
} from "@/api";
import type {SchedulerEventColor} from "@mui/x-scheduler/models";
import {interpolateArgs} from "@/utils/interpolateArgs.ts";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import PersonIcon from "@mui/icons-material/Person";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import InventoryIcon from "@mui/icons-material/Inventory";
import AssignmentIcon from "@mui/icons-material/Assignment";
import QuestionMarkIcon from "@mui/icons-material/QuestionMark";
import StorefrontIcon from "@mui/icons-material/Storefront";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import HistoryIcon from "@mui/icons-material/History";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import SwapVertIcon from "@mui/icons-material/SwapVert";
import React from "react";
import {Chip, Typography} from "@mui/material";
import ReceiptStatusChip from "@/components/receipts/ReceiptStatusChip.tsx";
import {formatReceiptNumber} from "@/components/receipts/receiptUtils.ts";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip.tsx";
import {
  MARKETPLACE_TYPE_COLORS,
  MARKETPLACE_TYPE_LABELS,
} from "@/pages/SettingsPage/pages/MarketplacesSettingsPage/marketplaceUtils.ts";

type EntityTypeConfig = {
  linkTemplate: string;
  typeName: string;
  icon: React.ReactNode;
  renderAdditionalCardContent?: (entity: ResolvedEntity) => React.ReactNode;
  renderAdditionalSearchContent?: (entity: ResolvedEntity) => React.ReactNode;
  getEventCalendarTitle?: (entity: AppEntity) => string;
  getStatusColor?: (entity: AppEntity) => SchedulerEventColor;
};

type ResolvedEntity = {
  link: string;
  typeName: string;
  icon: React.ReactNode;
  eventCalendarTitle: string;
  statusColor: SchedulerEventColor;
  renderAdditionalCardContent?: (entity: ResolvedEntity) => React.ReactNode;
  renderAdditionalSearchContent?: (entity: ResolvedEntity) => React.ReactNode;
} & AppEntity;

export const entitiesTypes: Record<AppEntityType, EntityTypeConfig> = {
  unknown: {
    linkTemplate: "#",
    typeName: "Обновите приложение",
    icon: <QuestionMarkIcon />,
  },
  user: {
    linkTemplate: "/settings/employees/{id}",
    typeName: "Сотрудник",
    icon: <PersonIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        <Typography>{e.additionalFields?.username as string}</Typography>
        {e.additionalFields?.email && (
          <Typography variant={"caption"}>{e.additionalFields?.email as string}</Typography>
        )}
      </>
    ),
  },
  roles: {
    linkTemplate: "/settings/roles",
    typeName: "Роли",
    icon: <AdminPanelSettingsIcon />,
  },
  warehouse: {
    linkTemplate: "/storage/warehouses/{id}",
    typeName: "Склад",
    icon: <WarehouseIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        <Typography>Товаров: {(e.additionalFields?.totalItemsCount as number) || "—"}</Typography>
      </>
    ),
  },
  storagePlaceNode: {
    linkTemplate: "no-link",
    typeName: "Место хранения",
    icon: <WarehouseIcon />,
  },
  catalogItem: {
    linkTemplate: "/catalog?item={id}",
    typeName: "Товар",
    icon: <InventoryIcon />,
    renderAdditionalSearchContent: (e) => (
      <>
        {e.additionalFields?.article && (
          <Typography sx={{fontFamily: "monospace"}}>
            {e.additionalFields.article as string}
          </Typography>
        )}
        {e.additionalFields?.type && (
          <CatalogItemTypeChip type={e.additionalFields.type as CatalogItemType} />
        )}
      </>
    ),
  },
  writeoff: {
    linkTemplate: "/operations/writeoffs/{id}",
    typeName: "Списание",
    icon: <AssignmentIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        {e.additionalFields?.number && (
          <Typography sx={{fontFamily: "monospace"}}>
            СПС-{String(e.additionalFields.number as number).padStart(5, "0")}
          </Typography>
        )}
      </>
    ),
  },
  stocktake: {
    linkTemplate: "/operations/stocktakes/{id}",
    typeName: "Инвентаризация",
    icon: <AssignmentIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        {e.additionalFields?.number && (
          <Typography sx={{fontFamily: "monospace"}}>
            ИНВ-{String(e.additionalFields.number as number).padStart(5, "0")}
          </Typography>
        )}
      </>
    ),
  },
  order: {
    linkTemplate: "/operations/orders/{id}",
    typeName: "Заказ",
    icon: <AssignmentIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        {e.additionalFields?.number && (
          <Typography sx={{fontFamily: "monospace"}}>
            ЗКЗ-{String(e.additionalFields.number as number).padStart(5, "0")}
          </Typography>
        )}
      </>
    ),
  },
  receipt: {
    linkTemplate: "/operations/receipts/{id}",
    typeName: "Приемка",
    icon: <AssignmentIcon />,
    getStatusColor: (e) => {
      const statusColors: Record<ReceiptStatus, SchedulerEventColor> = {
        draft: "grey",
        planned: "blue",
        processing: "amber",
        finished: "green",
        canceled: "red",
      };
      const status = e.additionalFields?.status as ReceiptStatus | undefined;
      return status != null ? statusColors[status] : "teal";
    },
    getEventCalendarTitle: (e) => {
      const number = e.additionalFields?.number as number | undefined;
      const prefix = number != null ? formatReceiptNumber(number) : null;
      return [prefix, e.name].filter(Boolean).join(" — ");
    },
    renderAdditionalCardContent: (e) => (
      <>
        {e.additionalFields?.number && (
          <Typography sx={{fontFamily: "monospace"}}>
            {formatReceiptNumber(e.additionalFields.number as number)}
          </Typography>
        )}
        {e.additionalFields?.status && (
          <ReceiptStatusChip status={e.additionalFields.status as ReceiptStatus} />
        )}
      </>
    ),
  },
  marketplaceAccount: {
    linkTemplate: "/settings/integrations/{id}",
    typeName: "Аккаунт маркетплейса",
    icon: <StorefrontIcon />,
    renderAdditionalCardContent: (e) => (
      <>
        {e.additionalFields?.type && (
          <Chip
            size={"small"}
            label={MARKETPLACE_TYPE_LABELS[e.additionalFields.type as MarketplaceType]}
            color={MARKETPLACE_TYPE_COLORS[e.additionalFields.type as MarketplaceType]}
          />
        )}
      </>
    ),
  },
  // Карточка не имеет своей страницы, а AppEntity для неё не несёт id аккаунта — ссылку не построить
  marketplaceCard: {
    linkTemplate: "no-link",
    typeName: "Карточка маркетплейса",
    icon: <LocalOfferIcon />,
  },
  // Сам журнал изменений. Не сущность, за которой ходят на страницу — заведён ради статистики хранилища
  changeLog: {
    linkTemplate: "no-link",
    typeName: "История изменений",
    icon: <HistoryIcon />,
  },
  // Тоже только ради статистики: у остатков нет своей страницы, их смотрят через склад или товар
  inventoryItem: {
    linkTemplate: "no-link",
    typeName: "Остатки",
    icon: <Inventory2Icon />,
  },
  stockMovement: {
    linkTemplate: "/storage/stock-movements",
    typeName: "Движения товаров",
    icon: <SwapVertIcon />,
  },
};

export function resolveEntity(entity: AppEntity): ResolvedEntity {
  const config = entitiesTypes[entity.type];
  if (!config) {
    return {
      ...entitiesTypes["unknown"],
      link: "#",
      type: "unknown",
      name: "Обновите приложение",
      eventCalendarTitle: entity.name ?? "Обновите приложение",
      statusColor: "grey" as SchedulerEventColor,
    };
  }

  return {
    ...entity,
    link: interpolateArgs(config.linkTemplate, {
      id: entity.id,
      ...entity.additionalFields,
    }),
    typeName: config.typeName,
    icon: config.icon,
    eventCalendarTitle: config.getEventCalendarTitle?.(entity) ?? entity.name ?? "—",
    statusColor: config.getStatusColor?.(entity) ?? "teal",
    renderAdditionalCardContent: config.renderAdditionalCardContent,
    renderAdditionalSearchContent: config.renderAdditionalSearchContent,
  };
}
