import type {ReactNode} from "react";
import CheckIcon from "@mui/icons-material/Check";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import UndoIcon from "@mui/icons-material/Undo";
import BlockIcon from "@mui/icons-material/Block";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import type {OrderStatus, OrderType} from "@/api/types.gen";

export interface OrderBulkTransition {
  key: string;
  from: OrderStatus[];
  to: OrderStatus;
  label: string;
  /** Completes «Часть заказов не удалось …:». */
  failedVerb: string;
  icon: ReactNode;
  /** Primary transitions get their own toolbar button, the rest go into the «Ещё» menu. */
  primary?: boolean;
  danger?: boolean;
  confirm?: {title: string; text: string; confirmText: string};
}

export function getOrderBulkTransitions(type: OrderType): OrderBulkTransition[] {
  return [
    {
      key: "confirm",
      from: ["draft"],
      to: "confirmed",
      label: "Подтвердить",
      failedVerb: "подтвердить",
      icon: <CheckIcon />,
      primary: true,
    },
    {
      key: "startAssembly",
      from: ["confirmed"],
      to: "assembly",
      label: "На сборку",
      failedVerb: "отправить на сборку",
      icon: <PlayArrowIcon />,
      primary: true,
    },
    {
      key: "ship",
      from: ["assembled"],
      to: "shipped",
      label: "Отгрузить",
      failedVerb: "отгрузить",
      icon: <LocalShippingIcon />,
      primary: true,
    },
    // Manual Assembly → Assembled is an escape hatch that fails unless every task is done and fully picked
    {
      key: "assemble",
      from: ["assembly"],
      to: "assembled",
      label: "Собрать",
      failedVerb: "собрать",
      icon: <DoneAllIcon />,
    },
    {
      key: "backToDraft",
      from: ["confirmed"],
      to: "draft",
      label: "Вернуть в черновик",
      failedVerb: "вернуть в черновик",
      icon: <UndoIcon />,
    },
    {
      key: "backToConfirmed",
      from: ["assembly"],
      to: "confirmed",
      label: "Вернуть в Подтверждён",
      failedVerb: "вернуть в Подтверждён",
      icon: <UndoIcon />,
      confirm: {
        title: "Вернуть заказы в Подтверждён?",
        text: "Задания на сборку будут удалены, уже собранные товары вернутся на склад. Заказы с завершёнными заданиями вернуть нельзя.",
        confirmText: "Вернуть",
      },
    },
    {
      key: "backToAssembled",
      from: ["shipped"],
      to: "assembled",
      label: "Вернуть в Собран",
      failedVerb: "вернуть в Собран",
      icon: <UndoIcon />,
    },
    {
      key: "restore",
      from: ["canceled"],
      to: type === "direct" ? "draft" : "confirmed",
      label: type === "direct" ? "Восстановить в черновик" : "Восстановить в Подтверждён",
      failedVerb: "восстановить",
      icon: <UndoIcon />,
    },
    {
      key: "cancel",
      from: ["draft", "confirmed", "assembly"],
      to: "canceled",
      label: "Отменить",
      failedVerb: "отменить",
      icon: <BlockIcon />,
      danger: true,
      confirm: {
        title: "Отменить заказы?",
        text: "Заказы, по которым уже есть собранные товары, отменены не будут.",
        confirmText: "Отменить заказы",
      },
    },
  ];
}
