import type {AnalyticsAbcSubject} from "@/api/types.gen";
import {NOUNS} from "@/utils/pluralUtils";

export const SUBJECTS: AnalyticsAbcSubject[] = ["catalogItem", "article", "card"];

export const SUBJECT_LABELS: Record<
  AnalyticsAbcSubject,
  {toggle: string; column: string; title: string}
> = {
  catalogItem: {toggle: "Позиции", column: "Позиция", title: "Позиции"},
  article: {toggle: "Артикулы", column: "Артикул", title: "Артикулы"},
  card: {toggle: "Карточки", column: "Карточка", title: "Карточки"},
};

export const SUBJECT_NOUNS = {
  catalogItem: NOUNS.position,
  article: NOUNS.article,
  card: NOUNS.card,
} as const satisfies Record<AnalyticsAbcSubject, unknown>;

export const SUBJECT_TOOLTIPS: Record<AnalyticsAbcSubject, string> = {
  catalogItem: "Позиция каталога, к которой была привязана карточка на момент продажи",
  article:
    "Карточки выбранных магазинов с одинаковым артикулом продавца складываются в одну строку. «Прямые» не участвуют: у них нет карточек",
  card: "Каждая карточка каждого магазина — отдельная строка. «Прямые» не участвуют: у них нет карточек",
};
