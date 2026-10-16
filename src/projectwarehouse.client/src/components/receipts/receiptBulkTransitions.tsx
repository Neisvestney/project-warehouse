import ScheduleSendIcon from "@mui/icons-material/ScheduleSend";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import UndoIcon from "@mui/icons-material/Undo";
import BlockIcon from "@mui/icons-material/Block";
import type {ReceiptStatus, ReceiptTransition} from "@/api/types.gen";
import type {DocumentBulkTransition} from "@/components/useDocumentBulkTransitions";

export const RECEIPT_BULK_TRANSITIONS: DocumentBulkTransition<ReceiptStatus, ReceiptTransition>[] =
  [
    {
      transition: "plan",
      from: ["draft"],
      label: "Запланировать",
      failedVerb: "запланировать",
      icon: <ScheduleSendIcon />,
      primary: true,
    },
    {
      transition: "startProcessing",
      from: ["planned"],
      label: "Начать приёмку",
      failedVerb: "начать",
      icon: <PlayArrowIcon />,
      primary: true,
      confirm: {
        title: "Начать приёмку?",
        text: "После начала приёмки изменить список позиций будет нельзя.",
        confirmText: "Начать приёмку",
      },
    },
    {
      transition: "revert",
      from: ["planned"],
      label: "Вернуть в черновик",
      failedVerb: "вернуть в черновик",
      icon: <UndoIcon />,
    },
    {
      transition: "cancel",
      from: ["planned", "processing"],
      label: "Отменить",
      failedVerb: "отменить",
      icon: <BlockIcon />,
      danger: true,
      confirm: {
        title: "Отменить приемки?",
        text: "Приемки, по которым уже есть размещения, отменены не будут.",
        confirmText: "Отменить приемки",
      },
    },
  ];
