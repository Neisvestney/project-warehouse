import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import EventIcon from "@mui/icons-material/Event";
import EditNoteIcon from "@mui/icons-material/EditNote";
import UndoIcon from "@mui/icons-material/Undo";
import BlockIcon from "@mui/icons-material/Block";
import type {StocktakeStatus, StocktakeTransition} from "@/api/types.gen";
import type {DocumentBulkTransition} from "@/components/useDocumentBulkTransitions";

export const STOCKTAKE_BULK_TRANSITIONS: DocumentBulkTransition<
  StocktakeStatus,
  StocktakeTransition
>[] = [
  {
    transition: "start",
    from: ["draft", "planned"],
    label: "Начать",
    failedVerb: "начать",
    icon: <PlayArrowIcon />,
    primary: true,
  },
  {
    transition: "schedule",
    from: ["draft"],
    label: "Запланировать",
    failedVerb: "запланировать",
    icon: <EventIcon />,
  },
  {
    transition: "toDraft",
    from: ["planned"],
    label: "Вернуть в черновик",
    failedVerb: "вернуть в черновик",
    icon: <EditNoteIcon />,
  },
  {
    transition: "revert",
    from: ["inProgress"],
    label: "В черновик",
    failedVerb: "вернуть в черновик",
    icon: <UndoIcon />,
  },
  {
    transition: "cancel",
    from: ["draft", "planned", "inProgress"],
    label: "Отменить",
    failedVerb: "отменить",
    icon: <BlockIcon />,
    danger: true,
    confirm: {
      title: "Отменить инвентаризации?",
      text: "Документы будут отменены. Остатки не будут затронуты.",
      confirmText: "Отменить инвентаризации",
    },
  },
];
