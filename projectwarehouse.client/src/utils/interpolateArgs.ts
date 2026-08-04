export function interpolateArgs(
  template: string,
  args?: {
    [key: string]: unknown;
  } | null,
): string {
  if (!args) return template;
  return template.replace(/\{(\w+)\}/g, (_, key: string) =>
    key in args ? String(args[key]) : `{${key}}`,
  );
}

/** True when every `{placeholder}` of the template can be filled from args. */
export function hasAllArgs(
  template: string,
  args?: {
    [key: string]: unknown;
  } | null,
): boolean {
  const keys = [...template.matchAll(/\{(\w+)\}/g)].map((m) => m[1]);
  if (keys.length === 0) return true;
  if (!args) return false;
  return keys.every((key) => key in args);
}
