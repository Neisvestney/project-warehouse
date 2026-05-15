import type {AppEntity, AppEntityType} from "@/api";
import {interpolateArgs} from "@/utils/interpolateArgs.ts";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import PersonIcon from "@mui/icons-material/Person";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";

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
  user: {
    linkTemplate: "/users/{id}",
    typeName: "Пользователь",
    icon: <PersonIcon />,
  },
  roles: {
    linkTemplate: "/settings/roles",
    typeName: "Роли",
    icon: <AdminPanelSettingsIcon />,
  },
  warehouse: {
    linkTemplate: "/warehouses/{id}",
    typeName: "Склад",
    icon: <WarehouseIcon />,
  },
};

export function resolveEntity(entity: AppEntity): ResolvedEntity {
  const config = entitiesTypes[entity.type];
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
