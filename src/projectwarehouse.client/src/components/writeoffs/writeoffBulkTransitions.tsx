import BlockIcon from "@mui/icons-material/Block";
import type {WriteoffStatus, WriteoffTransition} from "@/api/types.gen";
import type {DocumentBulkTransition} from "@/components/useDocumentBulkTransitions";

export const WRITEOFF_BULK_TRANSITIONS: DocumentBulkTransition<
  WriteoffStatus,
  WriteoffTransition
>[] = [
  {
    transition: "cancel",
    from: ["draft"],
    label: "Отменить",
    failedVerb: "отменить",
    icon: <BlockIcon />,
    danger: true,
    confirm: {
      title: "Отменить списания?",
      text: "Документы будут отменены. Товары не будут затронуты.",
      confirmText: "Отменить списания",
    },
  },
];
