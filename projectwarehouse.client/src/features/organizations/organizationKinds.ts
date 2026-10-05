import type {OrganizationKind} from "@/api/types.gen";

export const ORGANIZATION_KIND_LABELS: Record<OrganizationKind, string> = {
  legalEntity: "Юрлицо",
  soleProprietor: "ИП",
};
