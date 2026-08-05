const pluralRules = new Intl.PluralRules("ru-RU");

export type PluralForms = {
  /** 1, 21, 31... */
  one: string;
  /** 2-4, 22-24... и дробные (1,5) */
  few: string;
  /** 0, 5-20, 25-30... */
  many: string;
};

export function plural(n: number, forms: PluralForms): string {
  const category = pluralRules.select(n);
  // CLDR относит все дробные к "other", хотя по-русски им нужна форма few: «1,5 задания»
  if (category === "one") return forms.one;
  if (category === "many") return forms.many;
  return forms.few;
}

/** "2 задания", "5 заданий" */
export function pluralCount(n: number, forms: PluralForms): string {
  return `${n.toLocaleString("ru-RU")} ${plural(n, forms)}`;
}

export const NOUNS = {
  task: {one: "задание", few: "задания", many: "заданий"},
  item: {one: "товар", few: "товара", many: "товаров"},
  position: {one: "позиция", few: "позиции", many: "позиций"},
  itemType: {one: "тип", few: "типа", many: "типов"},
} as const satisfies Record<string, PluralForms>;
