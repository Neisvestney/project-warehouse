import type {AppFieldError, AppProblemDetails, ErrorCode} from "@/api/types.gen";
import {interpolateArgs} from "@/utils/interpolateArgs.ts";

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
  roleProtected: "Роль {roleName} защищена от изменений",
  userNotFound: "Пользователь не найден",
  roleNotFound: "Роль не найдена",
  permissionNotFound: "Право не найдено",
  userAlreadyExists: "Пользователь уже существует",
  roleAlreadyExists: "Роль уже существует",
  permissionAlreadyAssigned: "Право уже назначено",
  catalogItemNotFound: "Позиция каталога не найдена",
  catalogItemCharacteristicNotFound: "Характеристика позиции не найдена",
  storagePlaceNodeItemsGroupNotFound: "Группа позиций узла не найдена",
  catalogItemCharacteristicDuplicate: "Товар не может повторяться",
  warehouseNotFound: "Склад не найден",
  storagePlaceNotFound: "Место хранения не найдено",
  storagePlaceNodeNotFound: "Узел места хранения не найден",
  storagePlaceNodeHasChildren: "У узла есть дочерние — нельзя редактировать товары или удалить его",
  storagePlaceNodeHasItems: "Нельзя удалить узел — в нём есть товары",
  storagePlaceNodeParentHasItems: "Нельзя добавить дочерний узел — у родителя есть товары",
  storagePlaceNodeCyclicParent: "Обнаружена циклическая зависимость родителя",
  warehouseHasItems: "Нельзя удалить склад — в нём есть товары",
  storagePlaceHasItems: "Нельзя удалить место хранения — в нём есть товары",
  catalogItemIsInUse: "Нельзя удалить позицию — она хранится на складе",
  catalogItemIsImmutable: "Позиция неизменяема",
  catalogItemArticleDuplicate: "Товар с таким артикулом уже существует",
  catalogItemBarcodeDuplicate: "Товар с таким штрихкодом уже существует",
  catalogItemCharacteristicBarcodeDuplicate: "Штрихкод характеристики уже используется",
  catalogItemGroupInvalid: "Указанная группа не существует или не является группой товаров",
  catalogItemManagedByGroup: "Позиция управляется группой товаров",
  catalogItemVariationInvalid: "Указанная вариация не существует или неверного типа",
  catalogItemComponentInvalid: "Указанная позиция не может быть компонентом комплекта",
  catalogItemComponentNotFound: "Компонент комплекта не найден",
  required: "Обязательное поле",
  tooShort: "Значение слишком короткое (мин. {minimalLength} симв.)",
  tooLong: "Значение слишком длинное (макс. {maximalLength} симв.)",
  invalidFormat: "Неверный формат",
  outOfRange: "Значение вне допустимого диапазона",
  invalidJson: "Неверный формат JSON",
  passwordTooShort: "Пароль должен содержать как минимум {minimalLength} симв.",
  passwordAtLeastOneDigit: "Пароль должен содержать как минимум одну цифру",
  passwordAtLeastOneUppercase: "Пароль должен содержать хотя бы одну заглавную букву",
  passwordAtLeastOneLowercase: "Пароль должен содержать хотя бы одну строчную букву",
  passwordInvalid: "Пароль неверный",
  validationError: "Ошибка валидации",
  routeNotFound: "Маршрут API не найден",
  receiptNotFound: "Приемка не найдена",
  receiptInvalidStatusTransition: "Недопустимый переход статуса приемки",
  receiptHasPlacements: "У приемки есть размещения",
  receiptItemNotFound: "Позиция приемки не найдена",
  receiptItemPlacementNotFound: "Размещение позиции не найдено",
  receiptNotAssignedToWarehouse: "Приемка не привязана к этому складу",
  receiptItemsUnderplaced: "Некоторые позиции размещены не полностью",
  receiptItemsOverplaced: "Некоторые позиции размещены сверх принятого количества",
  insufficientInventory: "Недостаточно товара на складе",
  unitInventoryItemNumberDuplicate: "Инвентарный номер уже используется для этого товара",
};

export function resolveErrorMessage(error: AppFieldError): string {
  const template = errorCodeMessages[error.code] ?? error.detail;
  return interpolateArgs(template, error.args);
}

export function isNotFoundError(error: unknown): boolean {
  if (isAppProblemDetails(error)) return error.status === 404;
  if (typeof error === "string") return error.trim() === "404";
  return false;
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
