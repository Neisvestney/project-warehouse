import React from "react";
import StorefrontIcon from "@mui/icons-material/Storefront";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const marketplacesSections: SectionConfig[] = [
  {
    label: "Маркетплейсы",
    path: "marketplaces",
    icon: <StorefrontIcon fontSize="small" />,
    component: React.lazy(() => import("./MarketplacesPage.tsx")),
    requiredPermission: "integrations.view",
    subroutes: [
      {
        path: "auto-map-rules",
        component: React.lazy(() => import("./pages/AutoMapRulesPage/AutoMapRulesPage.tsx")),
      },
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/MarketplaceAccountCreatePage/MarketplaceAccountCreatePage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/MarketplaceAccountPage/MarketplaceAccountPage.tsx"),
        ),
      },
    ],
  },
];
