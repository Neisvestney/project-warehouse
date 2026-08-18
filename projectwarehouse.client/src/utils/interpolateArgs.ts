import {pluralCount} from "./pluralUtils";

/**
 * `{key}` — значение как есть.
 * `{key:заказа|заказов|заказов}` — число вместе с существительным в формах one|few|many:
 * падеж задаётся прямо в шаблоне, потому что «для 1 заказа» и «1 заказ» — разные формы одного слова.
 */
const PLACEHOLDER = /\{(\w+)(?::([^}]*))?\}/g;

function formatPlural(value: unknown, forms: string): string {
  const [one, few, many] = forms.split("|");
  const n = Number(value);
  if (many === undefined || !Number.isFinite(n)) return String(value);
  return pluralCount(n, {one, few, many});
}

export function interpolateArgs(
  template: string,
  args?: {
    [key: string]: unknown;
  } | null,
): string {
  if (!args) return template;
  return template.replace(PLACEHOLDER, (placeholder, key: string, forms?: string) => {
    if (!(key in args)) return placeholder;
    return forms === undefined ? String(args[key]) : formatPlural(args[key], forms);
  });
}

/** True when every `{placeholder}` of the template can be filled from args. */
export function hasAllArgs(
  template: string,
  args?: {
    [key: string]: unknown;
  } | null,
): boolean {
  const keys = [...template.matchAll(PLACEHOLDER)].map((m) => m[1]);
  if (keys.length === 0) return true;
  if (!args) return false;
  return keys.every((key) => key in args);
}
