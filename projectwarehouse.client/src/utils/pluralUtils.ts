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
  order: {one: "заказ", few: "заказа", many: "заказов"},
  receipt: {one: "приемка", few: "приемки", many: "приемок"},
  writeoff: {one: "списание", few: "списания", many: "списаний"},
  stocktake: {one: "инвентаризация", few: "инвентаризации", many: "инвентаризаций"},
  posting: {one: "отправление", few: "отправления", many: "отправлений"},
  item: {one: "товар", few: "товара", many: "товаров"},
  position: {one: "позиция", few: "позиции", many: "позиций"},
  card: {one: "карточка", few: "карточки", many: "карточек"},
  article: {one: "артикул", few: "артикула", many: "артикулов"},
  box: {one: "коробка", few: "коробки", many: "коробок"},
  itemType: {one: "тип", few: "типа", many: "типов"},
  fullWeek: {one: "полной неделе", few: "полным неделям", many: "полным неделям"},
} as const satisfies Record<string, PluralForms>;
