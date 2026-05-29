import type {AppEntity, AppEntityType} from "@/api";
import {interpolateArgs} from "@/utils/interpolateArgs.ts";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import PersonIcon from "@mui/icons-material/Person";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import InventoryIcon from "@mui/icons-material/Inventory";
import AssignmentIcon from "@mui/icons-material/Assignment";
import QuestionMarkIcon from "@mui/icons-material/QuestionMark";

type EntityTypeConfig = {
  linkTemplate: string;
  typeName: string;
  icon: React.ReactNode;
};

type ResolvedEntity = {
  link: string;
  typeName: string;
  icon: React.ReactNode;
} & AppEntity;

export const entitiesTypes: Record<AppEntityType, EntityTypeConfig> = {
  unknown: {
    linkTemplate: "#",
    typeName: "Обновите приложение",
    icon: <QuestionMarkIcon />,
  },
  user: {
    linkTemplate: "/settings/employees/{id}",
    typeName: "Пользователь",
    icon: <PersonIcon />,
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
  },
  receipt: {
    linkTemplate: "/operations/receipts/{id}",
    typeName: "Приемка",
    icon: <AssignmentIcon />,
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
  };
}
