/** Helpers over the `yyyy-MM-dd` strings the API uses for `DateOnly`, kept in the local time zone. */

export function toDateOnly(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function todayDateOnly(): string {
  return toDateOnly(new Date());
}

export function parseDateOnly(value: string): Date {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

export function addDays(value: string, days: number): string {
  const date = parseDateOnly(value);
  date.setDate(date.getDate() + days);
  return toDateOnly(date);
}

export function formatDateOnly(value: string): string {
  return parseDateOnly(value).toLocaleDateString("ru-RU", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

export function formatWeekday(value: string): string {
  return parseDateOnly(value).toLocaleDateString("ru-RU", {weekday: "short"});
}

export function isWeekend(value: string): boolean {
  const day = parseDateOnly(value).getDay();
  return day === 0 || day === 6;
}
