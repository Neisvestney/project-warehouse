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
