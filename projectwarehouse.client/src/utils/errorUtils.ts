import type {AppFieldError, AppProblemDetails, ErrorCode} from "@/api/types.gen";
import {hasAllArgs, interpolateArgs} from "@/utils/interpolateArgs.ts";

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
  catalogItemCircularDependency: "Обнаружена циклическая зависимость в компонентах комплекта",
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
  writeoffNotFound: "Списание не найдено",
  writeoffNotDraft: "Списание должно быть в статусе Черновик",
  writeoffHasNoItems: "Нет товаров для списания",
  writeoffItemNotFound: "Позиция списания не найдена",
  writeoffInsufficientInventory: "Недостаточно товара в указанной ячейке",
  writeoffNotAssignedToWarehouse: "Списание не привязано к этому складу",
  stocktakeNotFound: "Инвентаризация не найдена",
  stocktakeInvalidStatusTransition: "Действие недоступно в текущем статусе инвентаризации",
  stocktakeNotAssignedToWarehouse: "Инвентаризация не привязана к этому складу",
  stocktakeHasNoNodes: "Не выбрано ни одной ячейки",
  stocktakeNodeNotFound: "Ячейка не входит в эту инвентаризацию",
  stocktakeNodeAlreadyInProgress: "Ячейка уже пересчитывается в другой инвентаризации",
  stocktakeUnitCountedTwice: "Один и тот же экземпляр отмечен найденным в двух ячейках",
  stocktakeUnitItemInAnotherWarehouse: "Экземпляр числится на другом складе",
  stocktakeUnitItemDetached: "Экземпляр удерживается сборкой заказа",
  stocktakeConcurrentModification:
    "Остатки изменились во время проведения. Обновите страницу и попробуйте снова",
  receiptNotFound: "Приемка не найдена",
  receiptInvalidStatusTransition: "Недопустимый переход статуса приемки",
  receiptHasPlacements: "У приемки есть размещения",
  receiptItemNotFound: "Позиция приемки не найдена",
  receiptItemPlacementNotFound: "Размещение позиции не найдено",
  receiptNotAssignedToWarehouse: "Приемка не привязана к этому складу",
  receiptItemsUnderplaced: "Некоторые позиции размещены не полностью",
  receiptItemsOverplaced: "Некоторые позиции размещены сверх принятого количества",
  insufficientInventory: "Недостаточно товара на складе",
  inventoryItemNodeMismatch: "Товар больше не находится в ожидаемой ячейке. Обновите страницу",
  unitInventoryItemNumberDuplicate: "Инвентарный номер уже используется для этого товара",
  transferSameNode: "Источник и назначение не могут быть одной ячейкой",
  transferNotAssignedToWarehouse: "Вы не привязаны к этому складу",
  warehouseNotAssigned: "Вы не привязаны к этому складу",
  storagePlaceNotAssignedToWarehouse: "Место хранения относится к другому складу",
  unitInventoryItemNotFound: "Единичный товар не найден",
  inventoryItemMovedToAnotherNodeAfterPlacementCreated:
    "Товар был перемещён в другую ячейку после создания размещения",
  orderNotFound: "Заказ не найден",
  orderNotDraft: "Заказ должен быть в статусе Черновик",
  orderNotConfirmed: "Заказ должен быть в статусе Подтверждён",
  orderNotAssembly: "Заказ должен быть в статусе Сборка",
  orderInvalidStatusTransition: "Недопустимый переход статуса заказа",
  orderHasFulfillments: "У заказа есть фулфилменты",
  orderNotAssignedToWarehouse: "Вы не назначены на склад заказа",
  orderBoxNotFound: "Коробка заказа не найдена",
  orderBoxComponentNotFound: "Компонент коробки не найден",
  assemblyTaskNotFound: "Задание на сборку не найдено",
  assemblyTaskNotDeletable: "Задание нельзя удалить",
  assemblyTaskBoxNotFound: "Коробка задания не найдена",
  assemblyTaskBoxComponentNotFound: "Компонент задания не найден",
  assemblyFulfillmentNotFound: "Фулфилмент не найден",
  assemblyFulfillmentInvalidType: "Неверный тип фулфилмента",
  assemblyTaskAlreadyDone: "Задание уже завершено",
  assemblyTaskMoveTargetInvalid: "Недопустимая целевая коробка для перемещения",
  assemblyTaskQuantityExceedsAvailable: "Запрошенное количество превышает доступное",
  assemblyComponentAlreadyFulfilled: "Компонент уже полностью укомплектован",
  catalogItemNotVariationMember: "Выбранный вариант не входит в эту вариацию",
  marketplaceAccountNotFound: "Аккаунт маркетплейса не найден",
  marketplaceCredentialsInvalid: "Маркетплейс отклонил Client-Id или Api-Key",
  marketplaceCredentialsUnreadable:
    "Не удалось расшифровать сохранённый Api-Key — введите ключ заново",
  marketplaceClientIdRequired: "Для этой площадки требуется Client-Id",
  marketplaceApiError: "Маркетплейс вернул ошибку или недоступен",
  marketplaceSyncAlreadyRunning: "По этому аккаунту уже идёт синхронизация",
  marketplaceSyncInterrupted: "Синхронизация прервана остановкой приложения",
  marketplaceCardMappingTypeNotAllowed: "К карточке нельзя привязать позицию этого типа",
  marketplaceCardMappingArchivedItem: "Нельзя привязать карточку к архивной позиции каталога",
  marketplaceWarehouseNotFound: "Склад маркетплейса не найден",
  marketplaceCardNotFound: "Карточка маркетплейса не найдена",
  marketplaceAutoMapRuleNotFound: "Правило автосопоставления не найдено",
  marketplaceAutoMapRuleInvalidRegex: "Некорректное регулярное выражение",
  dataFileNotFound: "Файл не найден — возможно, форма была открыта слишком долго",
  dataFileEmpty: "Файл пустой",
  dataFileTooLarge: "Файл слишком большой",
  dataFileTypeNotAllowed: "Этот тип файла загружать нельзя",
  dataFileNotAnImage: "Превью доступно только для изображений",
  dataFileWidthNotAllowed: "Недопустимый размер превью",
  dataFileStorageError: "Не удалось сохранить файл",
  marketplaceOrdersNotSupported: "Эта площадка не поддерживает синхронизацию заказов",
  marketplaceAccountHasOrders: "По аккаунту импортированы заказы — сначала удалите их",
  marketplaceAccountInactive: "Аккаунт отключён",
  marketplaceLabelNotReady: "Маркетплейс ещё не сформировал этикетки — попробуйте через минуту",
  marketplaceOrderNotFromMarketplace: "Среди выбранных есть заказ не с маркетплейса",
  marketplaceOrderNotAwaitingDeliver:
    "Этикетка ещё не скачана, а отправление уже не ожидает отгрузки",
  marketplaceOrderCardNotMapped: "Товар заказа не привязан к позиции каталога",
  marketplaceOrderWarehouseNotMapped: "Склад отправления не привязан к складу WMS",
  realtimeConnectionUnknown: "Соединение обновлений разорвано — переподключаемся",
  editLockHeld: "Объект сейчас редактирует другой пользователь",
  editLockNotHeld: "Блокировка редактирования уже снята",
};

/** Richer variants used only when the server supplied every placeholder in args. */
const errorCodeArgMessages: Partial<Record<ErrorCode, string>> = {
  insufficientInventory:
    "Недостаточно «{itemName}» в {path}: требуется {requested}, доступно {available} (не хватает {missing})",
  writeoffInsufficientInventory:
    "Недостаточно «{itemName}» в {path}: требуется {requested}, доступно {available} (не хватает {missing})",
  marketplaceApiError: "Маркетплейс вернул ошибку {marketplaceStatus}",
  marketplaceCredentialsInvalid:
    "Маркетплейс отклонил Client-Id или Api-Key (код {marketplaceStatus})",
  dataFileTooLarge: "Файл слишком большой — максимум {maxBytes:байт|байта|байт}",
  dataFileTypeNotAllowed: "Этот тип файла загружать нельзя. Допустимые: {allowed}",
  dataFileWidthNotAllowed: "Недопустимый размер превью. Допустимые: {allowed}",
  editLockHeld: "Объект сейчас редактирует {userName}",
  marketplaceLabelNotReady:
    "Маркетплейс ещё не сформировал этикетки для {count:заказа|заказов|заказов} — попробуйте через минуту",
  marketplaceOrderNotAwaitingDeliver:
    "Для {count:заказа|заказов|заказов} этикетка не скачана, а отправление уже не ожидает отгрузки",
  marketplaceOrderCardNotMapped: "Товары не привязаны к каталогу: {offerIds}",
};

export function resolveErrorMessage(error: AppFieldError): string {
  const detailed = errorCodeArgMessages[error.code];
  if (detailed && hasAllArgs(detailed, error.args)) {
    return interpolateArgs(detailed, error.args);
  }
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

/** Root error first, otherwise any field error — endpoints scope errors to fields like `items`. */
export function firstFieldError(error: AppProblemDetails): AppFieldError | undefined {
  const rootErrors = error.errors["root"];
  if (rootErrors?.length) return rootErrors[0];
  return Object.values(error.errors).find((errors) => errors?.length)?.[0];
}

/** Returns the best human-readable message from an unknown API error. */
export function extractErrorMessage(error: unknown): string {
  if (isAppProblemDetails(error)) {
    const fieldError = firstFieldError(error);
    if (fieldError) return resolveErrorMessage(fieldError);
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
