import React from "react";
import BusinessIcon from "@mui/icons-material/Business";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const organizationsSections: SectionConfig[] = [
  {
    label: "Организации",
    path: "organizations",
    icon: <BusinessIcon fontSize="small" />,
    component: React.lazy(() => import("./OrganizationsPage.tsx")),
    requiredPermission: "organizations.view",
    subroutes: [
      {
        path: ":id",
        component: React.lazy(() => import("./pages/OrganizationPage/OrganizationPage.tsx")),
      },
    ],
  },
];
