import React from "react";
import CategoryIcon from "@mui/icons-material/Category";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const catalogSections: SectionConfig[] = [
  {
    label: "Каталог",
    path: "catalog",
    icon: <CategoryIcon fontSize="small" />,
    component: React.lazy(() => import("./CatalogPage.tsx")),
    requiredPermission: "catalog.view",
  },
];
