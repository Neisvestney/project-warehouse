import type {AppFieldError, AppProblemDetails, ErrorCode} from "@/api/types.gen";

export const httpStatusMessages: Partial<Record<number, string>> = {
  400: "Некорректный запрос",
  401: "Необходима авторизация",
  403: "Доступ запрещён",
  404: "Ресурс не найден",
  409: "Конфликт данных",
  429: "Слишком много запросов",
  500: "Внутренняя ошибка сервера",
  502: "Сервис временно недоступен",
  503: "Сервис недоступен",
  504: "Превышено время ожидания",
};

export const errorCodeMessages: Record<ErrorCode, string> = {
  invalidCredentials: "Неверный логин или пароль",
  tokenOutdated: "Токен устарел",
  tokenInvalid: "Токен недействителен",
  refreshTokenInvalid: "Токен обновления недействителен",
  refreshTokenExpired: "Токен обновления истёк",
  refreshTokenRevoked: "Токен обновления отозван",
  permissionDenied: "Недостаточно прав",
  roleProtected: "Роль защищена от изменений",
  userNotFound: "Пользователь не найден",
  roleNotFound: "Роль не найдена",
  permissionNotFound: "Право не найдено",
  userAlreadyExists: "Пользователь уже существует",
  roleAlreadyExists: "Роль уже существует",
  permissionAlreadyAssigned: "Право уже назначено",
  required: "Обязательное поле",
  tooShort: "Значение слишком короткое (мин. {minimalLength} симв.)",
  tooLong: "Значение слишком длинное (макс. {maximalLength} симв.)",
  invalidFormat: "Неверный формат",
  outOfRange: "Значение вне допустимого диапазона",
  invalidJson: "Неверный формат JSON",
  validationError: "Ошибка валидации",
};

function interpolateArgs(template: string, args?: AppFieldError["args"]): string {
  if (!args) return template;
  return template.replace(/\{(\w+)\}/g, (_, key: string) =>
    key in args ? String(args[key]) : `{${key}}`,
  );
}

export function resolveErrorMessage(error: AppFieldError): string {
  const template = errorCodeMessages[error.code] ?? error.detail;
  return interpolateArgs(template, error.args);
}

export function isAppProblemDetails(error: unknown): error is AppProblemDetails {
  return (
    typeof error === "object" &&
    error !== null &&
    "errors" in error &&
    typeof (error as AppProblemDetails).errors === "object"
  );
}

const NETWORK_ERROR_PATTERNS = [
  "failed to fetch",
  "network request failed",
  "load failed",
  "networkerror",
];

function isNetworkError(error: Error): boolean {
  return (
    error instanceof TypeError &&
    NETWORK_ERROR_PATTERNS.some((p) => error.message.toLowerCase().includes(p))
  );
}

/** Returns the best human-readable message from an unknown API error. */
export function extractErrorMessage(error: unknown): string {
  if (isAppProblemDetails(error)) {
    const rootErrors = error.errors["root"];
    if (rootErrors?.length) {
      return resolveErrorMessage(rootErrors[0]);
    }
    return error.title ?? error.detail ?? "Неизвестная ошибка";
  }

  if (typeof error === "string") {
    const statusMatch = error.match(/\b([45]\d{2})\b/);
    if (statusMatch) {
      const status = parseInt(statusMatch[1]);
      return httpStatusMessages[status] ?? `Ошибка сервера (${status})`;
    }
    // Don't show raw HTML to the user
    if (/<[a-z]/i.test(error)) return "Ошибка сервера";
    return error.trim() || "Неизвестная ошибка";
  }

  if (error instanceof Error) {
    if (isNetworkError(error)) return "Ошибка сети. Проверьте подключение к серверу";
    return error.message || "Неизвестная ошибка";
  }

  return "Неизвестная ошибка";
}
