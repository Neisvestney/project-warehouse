# Спецификация интеграции с маркетплейсами

## Обзор

Модуль интеграций подключает к WMS внешние торговые площадки. Первая реализуемая площадка — **Ozon Seller API**; архитектура изначально рассчитана на подключение **Wildberries** вторым провайдером без переделки схемы БД.

Первая версия покрывает четыре задачи:

1. Подключение магазина — хранение учётных данных (`Client-Id` + `Api-Key`) в зашифрованном виде.
2. Синхронизация складов маркетплейса и их привязка к `Warehouse` проекта.
3. Синхронизация карточек товаров маркетплейса и их привязка к `CatalogItem`.
4. Наблюдаемость — история запусков синхронизации, статусы, ошибки.

Синхронизация заказов (FBS/FBO) и обратная выгрузка остатков в маркетплейс в первую версию не входят — см. раздел «Отложено». Схема данных проектируется так, чтобы обе задачи подключались добавлением сущностей, а не переделкой существующих.

> **Контекст:** домен заказов уже содержит швы под маркетплейс — `Order.MarketplaceOrderId` и `OrderMarketplaceItem.MarketplaceCardId` (см. [orders-specification.md](orders-specification.md)). `MarketplaceCardId` — строковый внешний идентификатор без внешнего ключа; сущность `MarketplaceCard`, вводимая этой спецификацией, становится целью этой ссылки.

---

## Ozon Seller API — исходные данные

Всё ниже проверено по актуальному `https://docs.ozon.ru/api/seller/swagger.json` (OpenAPI 3.0.0, `info.version` 2.1).

| Параметр | Значение |
|----------|----------|
| Базовый URL | `https://api-seller.ozon.ru` (в спецификации указан как `//api-seller.ozon.ru` — без схемы) |
| Размер спецификации | ~3.4 МБ, 459 путей, все методы — `POST` |
| Аутентификация | Заголовки `Client-Id` и `Api-Key` |
| `securitySchemes` | **Отсутствуют.** Аутентификация описана как обычные обязательные header-параметры `#/components/parameters/Client-Id` и `#/components/parameters/Api-Key` на каждой операции |
| Пагинация | Двух видов: `last_id` (товары) и `cursor` (склады, остатки) |

### Используемые методы

| Метод | `operationId` | Назначение | Ограничения |
|-------|---------------|------------|-------------|
| `POST /v2/warehouse/list` | `WarehouseListV2` | Список складов FBS/rFBS | Пагинация по `cursor` + `limit` |
| `POST /v3/product/list` | `ProductAPI_GetProductList` | Список товаров (идентификаторы) | `limit` 1…1000, пагинация по `last_id` |
| `POST /v3/product/info/list` | `ProductAPI_GetProductInfoList` | Полные данные карточек по идентификаторам | Пакет до 1000 идентификаторов |
| `POST /v1/seller/info` | `SellerAPI_SellerInfo` | Название магазина и реквизиты продавца | Тела запроса нет |

> **Ограничение:** `POST /v1/warehouse/list` помечен в спецификации как устаревающий с датой отключения 7 апреля 2026 года. Использовать только `/v2/warehouse/list`.

Поля ответа `/v2/warehouse/list` (элемент `warehouses[]`), релевантные WMS:

```
warehouse_id       — int64, внешний идентификатор склада
name               — string, название склада в кабинете продавца
status             — string, статус склада
warehouse_type     — string
is_rfbs            — bool
is_express         — bool
is_kgt             — bool
created_at         — date-time
updated_at         — date-time
address_info       — { address, latitude, longitude, utc }
```

Поля ответа `/v3/product/info/list` (элемент `items[]`), релевантные WMS:

```
id                 — int64, product_id (первичный внешний идентификатор карточки)
sku                — int64, SKU Ozon
offer_id           — string, артикул продавца — ключ автосопоставления
name               — string
barcodes           — string[], второй ключ автосопоставления
primary_image      — string[]
images             — string[]
price / old_price  — string (десятичное число строкой)
currency_code      — string
is_archived        — bool
is_autoarchived    — bool
created_at         — string
updated_at         — string
```

Поля ответа `/v1/seller/info` (объект `company`), релевантные WMS:

```
name               — string, название магазина на Ozon → MarketplaceAccount.Name
legal_name         — string, полное наименование юрлица
inn                — string
ogrn               — string
ownership_form     — string, форма собственности
country / currency / tax_system — не сохраняются
```

Соседние объекты ответа — `ratings` и `subscription` — намеренно игнорируются: это метрики продавца, а не реквизиты, и меняются они постоянно.

`POST /v3/product/list` возвращает только `offer_id`, `product_id`, `sku`, `archived`, `has_fbo_stocks`, `has_fbs_stocks` — этого недостаточно для карточки, поэтому список всегда догружается методом `/v3/product/info/list`.

### Методы для будущих этапов

Не генерируются в первой версии, но зарезервированы в whitelist-е спецификации:

| Метод | Назначение |
|-------|------------|
| `POST /v2/products/stocks` | Обновление остатков в Ozon |
| `POST /v2/product/info/stocks-by-warehouse/fbs` | Остатки FBS по складам |
| `POST /v4/posting/fbs/list`, `POST /v3/posting/fbs/get` | Отправления FBS |
| `POST /v3/posting/fbo/list` | Отправления FBO |

---

## Генерация C#-клиента

### Проблема

Прямая генерация по полной спецификации Ozon неприемлема: 459 путей и несколько тысяч схем дают десятки тысяч строк сгенерированного кода, долгую сборку и нечитаемые диффы при каждом обновлении спецификации. Кроме того, `Client-Id` и `Api-Key` объявлены как обязательные header-параметры каждой операции — NSwag вынесет их в сигнатуру **каждого** метода, что делает клиент неудобным и провоцирует протечку ключей по коду.

### Конвейер

```
tools/marketplaces/ozon/
  ├── ozon-swagger.raw.json        — выгруженная спецификация Ozon (не коммитится)
  ├── paths.whitelist.json         — список используемых путей + зарезервированные
  ├── generate-client.cs           — выгрузка, обрезка, санитайзинг, генерация
  └── ozon-openapi.trimmed.json    — результат обрезки (коммитится)

ProjectWarehouse.Server/Integrations/
  ├── Abstractions/                — провайдер-нейтральные контракты и модели
  ├── Ozon/
  │   ├── Generated/OzonApiClient.g.cs   — вывод NSwag (коммитится)
  │   ├── OzonClient.cs                  — обёртка: пагинация, ошибки, маппинг
  │   ├── OzonAuthHandler.cs             — DelegatingHandler, подстановка заголовков
  │   ├── MarketplaceRequestContext.cs   — ambient-учётка для хендлера
  │   └── OzonMarketplaceProvider.cs     — реализация IMarketplaceProvider
  └── Sync/
      ├── MarketplaceSyncQueue.cs        — bounded Channel заявок
      ├── MarketplaceSyncWorker.cs       — BackgroundService + реконсиляция при старте
      ├── PostgresAdvisoryLock.cs        — лок на выделенном соединении
      └── MarketplaceSyncScanJob.cs      — Quartz-джоб

Services/MarketplaceSyncService.cs      — склады, карточки, автосопоставление
```

Весь конвейер — один **file-based-скрипт C#** (`dotnet run generate-client.cs`), возможность .NET 10 запускать одиночный `.cs`-файл без проекта и без `.csproj`. Зависимости объявляются директивами прямо в файле:

```csharp
#!/usr/bin/env dotnet
#:package NSwag.CodeGeneration.CSharp@14.*

using System.Text.Json.Nodes;
// ...
```

Подключается библиотека `NSwag.CodeGeneration.CSharp`, а не консольный `NSwag.ConsoleCore`: последний — это CLI-пакет, вызвать его из скрипта можно только запуском отдельного процесса. Генерация выполняется в процессе через `OpenApiDocument.FromFileAsync` + `CSharpClientGenerator`, поэтому отдельного `ozon.nswag` нет — настройки живут прямо в скрипте, в том же файле, что и логика обрезки.

Почему не PowerShell:

- Обрезка спецификации — это обход графа `$ref` с транзитивным замыканием. `System.Text.Json.Nodes` даёт для этого нормальную мутабельную модель документа; в PowerShell то же самое пишется через `ConvertFrom-Json -AsHashtable` и рекурсию по хеш-таблицам, что заметно многословнее и хуже читается.
- Скрипт живёт в одном языке с остальным репозиторием — правит его тот же человек, что и сервер.
- Дополнительный инструментарий не нужен: .NET 10 SDK и так обязателен, проект собирается под `net10.0`.
- NSwag подключается директивой `#:package`, а не заранее установленным глобальным инструментом, — версия генератора зафиксирована в том же файле, что и логика.

Если скрипт со временем разрастётся, `dotnet project convert` превращает его в обычный проект без переписывания.

**Шаг 1 — выгрузка** (`--fetch`). `docs.ozon.ru` отдаёт `swagger.json` только браузероподобным клиентам: `curl` без корректных заголовков получает петлю редиректов (`?__rr=N`) либо `403`. Скрипт задаёт `User-Agent`, `Accept` и `Referer` на `HttpClient`; при неудаче спецификация выгружается вручную из браузера в `ozon-swagger.raw.json`. Именно поэтому **обрезанная спецификация коммитится в репозиторий** — сборка не должна зависеть ни от доступности `docs.ozon.ru`, ни от того, пропустит ли он запрос.

> **Проверено на практике:** заголовков недостаточно — защита завязана не только на них. `HttpClient` получает `403`, `curl` с полным набором браузерных заголовков — петлю редиректов до исчерпания лимита. Рабочий путь один: открыть URL в браузере и сохранить ответ. `--fetch` оставлен на случай, если Ozon ослабит защиту; при неудаче скрипт печатает инструкцию и выходит с кодом 1, не затирая существующий raw-файл.

**Шаг 2 — обрезка:**

1. Оставить только пути из `paths.whitelist.json`.
2. Удалить из каждой операции параметры `Client-Id` и `Api-Key` (`$ref` на `#/components/parameters/*`).
3. Транзитивно собрать из оставшихся операций все достижимые `components.schemas`, остальное выбросить.
4. Прописать `servers[0].url = "https://api-seller.ozon.ru"` — исходное значение `//api-seller.ozon.ru` без схемы NSwag разбирает некорректно.
5. **Санитайзинг.** Спека объявляет себя как OpenAPI 3.0.0, но содержит наследие Swagger 2.0, которое NJsonSchema не переваривает:
   - `required: true` булевым внутри схемы свойства (в 3.0 это массив имён на объекте-владельце) — падение при разборе;
   - схемы массивов с `items`, но **без** `"type": "array"` — молча генерируются как `object?` вместо типизированной коллекции (задевает `productv3GetProductListResponseResult.items` и фильтры запроса).

   Санитайзер обходит документ **по позициям схем** (`schema`, `schemas`, затем `properties`/`items`/`allOf`/…), а не рекурсией по всем узлам: слепой обход принял бы `properties`-словарь за схему всякий раз, когда у объекта есть свойство с именем `items` или `type` — в спеке Ozon есть и то, и другое.

Результат — файл на ~96 КБ вместо 3.4 МБ (459 путей → 3).

Запуск:

```
dotnet run tools/marketplaces/ozon/generate-client.cs -- --fetch   # выгрузить, обрезать, сгенерировать
dotnet run tools/marketplaces/ozon/generate-client.cs              # обрезать и сгенерировать из raw-файла
```

Разделитель `--` обязателен: без него `dotnet` разберёт `--fetch` как собственный аргумент.

**Шаг 3 — генерация** (`CSharpClientGeneratorSettings` внутри скрипта):

| Настройка | Значение | Причина |
|-----------|----------|---------|
| `namespace` | `ProjectWarehouse.Server.Integrations.Ozon.Generated` | |
| `className` | `OzonApiClient` | |
| `operationGenerationMode` | `SingleClientFromOperationId` | После обрезки методов немного — дробить на клиенты по тегам незачем |
| `injectHttpClient` | `true` | Конструктор принимает `HttpClient` → работает с `IHttpClientFactory` |
| `useBaseUrl` | `false` | `BaseAddress` задаётся при регистрации `HttpClient` |
| `generateClientInterfaces` | `true` | `IOzonApiClient` подменяется в тестах |
| `jsonLibrary` | `SystemTextJson` | В проекте нет Newtonsoft |
| `generateNullableReferenceTypes` | `true` | `<Nullable>enable</Nullable>` в csproj |
| `generateOptionalParameters` | `false` | Явные параметры читаются однозначно |
| `arrayType` / `arrayInstanceType` | `IReadOnlyList` / `List` | |
| `dateTimeType` | `System.DateTimeOffset` | Ozon отдаёт смещения; в домене они конвертируются в UTC |
| `exceptionClass` | `OzonApiException` | Единая точка перехвата |
| `classStyle` | `Poco` | Никакого `INotifyPropertyChanged` |

`generateUpdateJsonSerializerSettingsMethod` оставлен включённым: с `false` NSwag убирает объявление partial-метода, но оставляет его вызов в конструкторе — сгенерированный файл не компилируется. Заодно это единственная точка, куда можно дошить недостающее поведение — рукописный `Integrations/Ozon/OzonApiClientSerialization.cs` реализует два partial-хука:

- `UpdateJsonSerializerSettings` — добавляет конвертер строковых enum'ов. NSwag для enum'а **внутри коллекции** конвертер не вешает (оставляет в коде `TODO(system.text.json): Add ItemConverterType...`), поэтому `working_days: ["MONDAY"]` падал с «The JSON value could not be converted». Конвертер терпимый: неизвестное значение схлопывается в значение по умолчанию, а не роняет страницу — Ozon расширяет свои enum'ы без предупреждения, а обрезанная спека в репозитории это снимок.
- `Initialize` — включает `ReadResponseAsString`. По умолчанию NSwag читает тело потоком и кладёт в `OzonApiException.Response` **пустую строку**; в результате отказ Ozon доезжал до `MarketplaceSyncRun.Error` голым кодом статуса, без причины. Буферизовать только ошибки нельзя: `ReadObjectResponseAsync` объявлен в сгенерированной половине класса, переопределить его из своей partial-половины невозможно.

Генерация запускается **вручную** через `generate-client.cs`, вывод коммитится. Это зеркалит подход фронтенда (`npm run generate-api` + закоммиченный `src/api`) и сохраняет детерминированность и офлайн-собираемость. Сгенерированный файл исключается из анализаторов через `.editorconfig` в папке `Generated/`.

**Шаг 4 — обёртка.** Сгенерированный клиент наружу модуля не выходит. `OzonClient : IOzonClient` предоставляет доменно-осмысленные операции и берёт на себя то, чего в сгенерированном коде нет:

```
IOzonClient
  Task<IReadOnlyList<OzonWarehouse>> GetWarehousesAsync(ct)
      — цикл по cursor до исчерпания, limit 200
  IAsyncEnumerable<IReadOnlyList<OzonProductCard>> GetCardsAsync(ct)
      — цикл по last_id + догрузка /v3/product/info/list теми же пакетами
  Task PingAsync(ct)
      — /v2/warehouse/list с limit=1, проверка учётных данных
```

Размеры страниц ограничены сверху самой спекой, а сгенерированный клиент их не валидирует: `/v2/warehouse/list` отвергает `limit > 200` (`maximum: 200` в схеме запроса), `/v3/product/list` допускает до 1000. Превышение возвращается как `400` от Ozon, а не как ошибка компиляции.

### Аутентификация и устойчивость

Заголовки `Client-Id` / `Api-Key` вырезаны из спецификации, поэтому подставляются транспортом. Один `HttpClient` обслуживает несколько аккаунтов, значит учётные данные нельзя фиксировать в `DefaultRequestHeaders`.

```
services.AddHttpClient<IOzonApiClient, OzonApiClient>(c => c.BaseAddress = new Uri(options.BaseUrl))
    .AddHttpMessageHandler<OzonAuthHandler>()
    .AddStandardResilienceHandler(r =>
    {
        r.AttemptTimeout.Timeout = ozonTimeout;
        r.TotalRequestTimeout.Timeout = ozonTimeout * 3;
        r.CircuitBreaker.SamplingDuration = ozonTimeout * 6;
    });
```

`HttpClient.Timeout` **не задаётся**: при подключённом resilience-хендлере он мёртвый код — таймауты хендлера срабатывают раньше. Настраивать их приходится явно, иначе действуют дефолты (10 с на попытку, 30 с суммарно), которые режут медленные ответы Ozon. Ограничение хендлера: `AttemptTimeout ≤ SamplingDuration / 2`, иначе валидация роняет приложение на старте.

`OzonAuthHandler` читает учётные данные из `MarketplaceRequestContext`. Контекст — **синглтон поверх `AsyncLocal`**, а не scoped-сервис: `IHttpClientFactory` собирает и кеширует цепочки хендлеров в собственном DI-скоупе, поэтому scoped-контекст, внедрённый в хендлер, — **другой экземпляр**, не тот, в который писал провайдер. Ambient-значение долетает независимо от скоупов. Одна оговорка: **через `yield return` скоуп не живёт**. Запись в `AsyncLocal` протекает вниз по `await`'ам, но не наверх к вызывающему, а асинхронный итератор на каждом yield возвращает управление потребителю — следующий `MoveNextAsync` продолжает тело уже в его контексте, не выполняя присваивание повторно. Поэтому в `FetchCardsAsync` скоуп открывается не в начале метода, а вокруг каждого шага энумератора, иначе учётные данные видит только первая страница. Обычные `async`-методы с постраничным циклом (например `FetchWarehousesAsync`) этим не затронуты. Один `HttpClient` обслуживает несколько аккаунтов, значит фиксировать заголовки в `DefaultRequestHeaders` в любом случае нельзя.

`AddStandardResilienceHandler` (`Microsoft.Extensions.Http.Resilience`) даёт таймауты, ретраи с экспоненциальной задержкой и circuit breaker. Ozon отвечает `429` при превышении лимитов метода — обработчик уважает `Retry-After`; сверх этого сервис синхронизации выдерживает настраиваемую паузу между страницами.

Сгенерированный `OzonApiException` наружу модуля тоже не выходит: провайдер заворачивает его в провайдер-нейтральный `MarketplaceApiException` (`StatusCode`, усечённое до 2000 символов тело ответа, готовый набор `Args`). Ни сервис синхронизации, ни контроллер не ссылаются на сгенерированные типы.

### Новые пакеты

| Пакет | Куда | Назначение |
|-------|------|------------|
| `Microsoft.Extensions.Http.Resilience` | `ProjectWarehouse.Server.csproj` | Ретраи, таймауты, circuit breaker |
| `Quartz.Extensions.Hosting` | `ProjectWarehouse.Server.csproj` | Планировщик синхронизации |
| `NSwag.CodeGeneration.CSharp` | директива `#:package` в `generate-client.cs` | Генерация клиента |

`NSwag.CodeGeneration.CSharp` в зависимости сервера **не попадает** — он нужен только скрипту генерации и объявлен внутри него. Серверный проект собирается без него.

`IHttpClientFactory`, фоновых воркеров и Data Protection в проекте до этого не было — модуль вводит всё три.

---

## Хранение учётных данных

### Шифрование

`Api-Key` шифруется через ASP.NET Core Data Protection и хранится в БД шифротекстом.

```
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(options.KeyRingPath))
    .SetApplicationName("ProjectWarehouse");
```

Кольцо ключей монтируется томом в `docker-compose` (`/keys`). В `Dockerfile` каталог создаётся и передаётся `$APP_UID` **до** переключения пользователя — иначе том монтируется root-owned и приложение не может писать кольцо.

Утрата тома означает невозможность расшифровать сохранённые ключи — их придётся ввести заново; на этот случай `MarketplaceAccount.ApiKeyProtected` расшифровывается лениво. Отдельного статуса `CredentialsUnreadable` **нет**: `TryUnprotect` возвращает `false`, вызывающий ставит `LastSyncStatus = Failed` и `LastSyncError` с кодом `marketplaceCredentialsUnreadable`, а эндпоинт детали аккаунта отдаёт вычисляемый флаг `credentialsUnreadable` (дешёвая проба при каждом чтении, в БД не хранится). Наружу это `422`, а не `500`.

Шифрование выполняет отдельный сервис, **не** `ValueConverter`:

```
IMarketplaceCredentialProtector
  string Protect(string plain)
  string Unprotect(string protectedValue)
  bool TryUnprotect(string protectedValue, out string plain)
```

Причина отказа от `ValueConverter`: конвертер расшифровывал бы ключ при каждом чтении сущности, включая листинги, и открытый ключ оказывался бы в памяти там, где он не нужен. Явный сервис даёт единственную точку, где ключ становится открытым, — момент вызова провайдера.

### Правила обращения с ключом

- `MarketplaceAccountDto` **не содержит поля ключа** — физически нечему протечь наружу. Отдаётся только `apiKeyMask` вида `••••1234` и `apiKeyUpdatedAt`.
- В запросах создания/обновления `apiKey` — write-only. Пустое или отсутствующее значение при обновлении означает «оставить текущий».
- Ключ никогда не попадает в логи. `OzonApiException` при логировании чистится от заголовков запроса.
- В changelog пишется только факт ротации ключа (`action: "account.key_rotated"`) — без значений, ни старого, ни нового.

---

## Модель данных

### Аккаунт маркетплейса (`MarketplaceAccount`)

```
MarketplaceAccount : IHasIdentity
├── Id                    — Guid
├── Type                  — MarketplaceType (Ozon | Wildberries)
├── Name                  — string, название магазина по данным маркетплейса (заполняется синхронизацией)
├── IsActive              — bool, выключенный аккаунт не синхронизируется
├── ExternalClientId      — string?, Client-Id Ozon (для WB не заполняется)
├── CompanyLegalName      — string?, наименование юрлица  ─┐
├── Inn                   — string?                        │ реквизиты продавца,
├── Ogrn                  — string?                        │ заполняются синхронизацией
├── OwnershipForm         — string?, форма собственности   ─┘
├── ApiKeyProtected       — string, шифротекст
├── ApiKeyLast4           — string, хвост ключа для маски
├── ApiKeyUpdatedAt       — DateTime?
├── SyncIntervalMinutes   — int, периодичность фоновой синхронизации
├── LastSyncAt            — DateTime?
├── LastSyncStatus        — MarketplaceSyncStatus?
├── LastSyncError         — AppFieldError? (jsonb)
├── CreatedAt             — DateTime
├── CreatedById           — Guid? → ApplicationUser (SetNull)
├── Warehouses            — MarketplaceWarehouse[]
├── Cards                 — MarketplaceCard[]
└── SyncRuns              — MarketplaceSyncRun[]

[Projectable] SearchString => Name + " " + ExternalClientId + " " + CompanyLegalName + " " + Inn
```

**Название аккаунта руками не вводится.** Ни `POST /accounts`, ни `PUT /accounts/{id}` поля `name` не принимают: его источник — `company.name` из `/v1/seller/info`, и каждая синхронизация его перезаписывает. Между созданием аккаунта и первым успешным запуском в поле лежит заглушка вида `Ozon ••••1234` (тип маркетплейса + маска ключа) — аккаунт обязан быть отображаемым в списке сразу. Пустое имя от маркетплейса заглушку не затирает.

### Склад маркетплейса (`MarketplaceWarehouse`)

```
MarketplaceWarehouse : IHasIdentity
├── Id                    — Guid
├── MarketplaceAccountId  — Guid → MarketplaceAccount (Cascade)
├── ExternalId            — string, warehouse_id маркетплейса
├── Name                  — string
├── Kind                  — MarketplaceWarehouseKind (Fbs | Rfbs | Express | Fbo | Unknown)
├── ExternalStatus        — string?, статус в кабинете продавца
├── Address               — string?
├── IsArchived            — bool, склад исчез из выдачи API
├── WarehouseId           — Guid? → Warehouse (Restrict) — привязка к складу WMS
└── SyncedAt              — DateTime

Уникальный индекс: (MarketplaceAccountId, ExternalId)
Индекс: (WarehouseId)
```

`Kind` выводится из флагов Ozon: `is_express` → `Express`, `is_rfbs` → `Rfbs`, иначе `Fbs`.

**Кратность привязки — N:1.** Несколько складов маркетплейса могут указывать на один `Warehouse` WMS: у продавца бывает несколько складов в кабинете Ozon, физически стоящих на одном складе. Обратное направление (один склад маркетплейса на несколько `Warehouse`) запрещено самим полем — оно скалярное.

> **Ограничение:** N:1 безопасна на чтение, но при выгрузке остатков даст оверселлинг — один и тот же остаток уйдёт на каждый привязанный склад Ozon. Разбирается на этапе выгрузки остатков, не сейчас.

### Карточка маркетплейса (`MarketplaceCard`)

```
MarketplaceCard : IHasIdentity
├── Id                    — Guid
├── MarketplaceAccountId  — Guid → MarketplaceAccount (Cascade)
├── ExternalId            — string, product_id
├── Sku                   — string?, SKU маркетплейса
├── OfferId               — string, артикул продавца
├── Name                  — string
├── Barcodes              — jsonb string[]
├── PrimaryImageUrl       — string?
├── Price                 — decimal?
├── CurrencyCode          — string?
├── IsArchived            — bool
├── CatalogItemId         — Guid? → CatalogItem (Restrict) — привязка к каталогу WMS
├── MappingSource         — MarketplaceMappingSource? (Manual | AutoOfferId | AutoBarcode)
├── MappedAt              — DateTime?
└── SyncedAt              — DateTime

Уникальный индекс: (MarketplaceAccountId, ExternalId)
Индексы: (MarketplaceAccountId, OfferId), (CatalogItemId)

[Projectable] SearchString => Name + " " + OfferId + " " + ExternalId + " " + Sku
```

**Сырой ответ маркетплейса не сохраняется.** Хранить `jsonb` с полным объектом карточки на десятках тысяч строк — заметный рост таблицы ради редких разборов инцидентов. Провайдер отображает ответ в `ExternalCard` и выбрасывает остальное; если в будущем понадобится новое поле — оно добавляется колонкой и заполняется следующей полной синхронизацией, которая и так идёт по расписанию.

### Запуск синхронизации (`MarketplaceSyncRun`)

```
MarketplaceSyncRun : IHasIdentity
├── Id                    — Guid
├── MarketplaceAccountId  — Guid → MarketplaceAccount (Cascade)
├── Scope                 — MarketplaceSyncScope (Warehouses | Cards | All)
├── Status                — MarketplaceSyncStatus (Running | Success | Failed | Canceled)
├── StartedAt             — DateTime
├── FinishedAt            — DateTime?
├── TriggeredById         — Guid? → ApplicationUser (SetNull); null = фоновый запуск
├── WarehousesProcessed   — int
├── CardsProcessed        — int
├── CardsCreated          — int
├── CardsUpdated          — int
├── CardsArchived         — int
├── AutoMapped            — int
└── Error                 — AppFieldError? (jsonb)

Индекс: (MarketplaceAccountId, StartedAt DESC)
```

**Ошибки хранятся структурно, а не строкой.** `Error` и `LastSyncError` — это `AppFieldError` (`{ Code, Detail, Args }`), тот же тип, что лежит внутри `AppProblemDetails.Errors`. Что это даёт:

- фронт получает машиночитаемый `code` и рисует нужное действие, а не парсит текст;
- `Args` уносит контекст (`marketplaceStatus`, `marketplaceResponse`, `accountId`) без склейки в сообщение;
- тип уже есть в сгенерированном TS-клиенте — он часть `AppProblemDetails`, отдельной работы на фронте не нужно.

Собирать **только** через `AppProblems.MakeError(code, message, args)` — он же проставляет `Detail` в каноническом формате `"camelCaseCode: message"`. Текст `Detail` — англоязычный, для разработчика; локализация делается на фронте по `code` + `args`.

> **Нюанс сериализации.** jsonb пишет сериализатор Npgsql (`EnableDynamicJson()`), а не MVC-шный с `JsonStringEnumConverter`. Значит `ErrorCode` внутри колонки лежит **числом**, а наружу через DTO уезжает camelCase-строкой. Следствие: значения `ErrorCode` можно только **дописывать в конец** — вставка в середину переинтерпретирует уже записанные ошибки.

### Перечисления

| Перечисление | Значения |
|--------------|----------|
| `MarketplaceType` | `Ozon = 0`, `Wildberries = 1` |
| `MarketplaceWarehouseKind` | `Unknown = 0`, `Fbs = 1`, `Rfbs = 2`, `Express = 3`, `Fbo = 4` |
| `MarketplaceMappingSource` | `Manual = 0`, `AutoOfferId = 1`, `AutoBarcode = 2` |
| `MarketplaceSyncScope` | `Warehouses = 0`, `Cards = 1`, `All = 2` |
| `MarketplaceSyncStatus` | `Running = 0`, `Success = 1`, `Failed = 2`, `Canceled = 3` |

### Связь с существующим доменом

```
MarketplaceWarehouse.WarehouseId   ──> Warehouse.Id       (Restrict)
MarketplaceCard.CatalogItemId      ──> CatalogItem.Id     (Restrict)
OrderMarketplaceItem.MarketplaceCardId ─ ─> MarketplaceCard.ExternalId  (строковая ссылка, без FK)
```

`Restrict` на обеих привязках: удаление склада или позиции каталога, на которую ссылается карточка маркетплейса, должно явно блокироваться, а не тихо обнулять маппинг.

Связь `OrderMarketplaceItem → MarketplaceCard` остаётся строковой и без внешнего ключа до этапа синхронизации заказов: заказ может приехать с карточкой, которой ещё нет в WMS, и жёсткий FK сделал бы такой заказ несохраняемым.

---

## Провайдер-абстракция

Сервис синхронизации не знает ни про Ozon, ни про NSwag. Он работает с провайдер-нейтральными моделями.

```
IMarketplaceProvider
├── MarketplaceType Type { get; }
├── MarketplaceCapabilities Capabilities { get; }
├── Task<CredentialsValidationResult> ValidateAsync(MarketplaceCredentials, ct)
├── Task<IReadOnlyList<ExternalWarehouse>> FetchWarehousesAsync(MarketplaceCredentials, ct)
├── IAsyncEnumerable<IReadOnlyList<ExternalCard>> FetchCardsAsync(MarketplaceCredentials, ct)
└── Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials, ct)

MarketplaceCredentials  — record (string? ClientId, string ApiKey)
ExternalWarehouse       — record (string ExternalId, string Name, MarketplaceWarehouseKind Kind,
                                  string? Status, string? Address)
ExternalCard            — record (string ExternalId, string? Sku, string OfferId, string Name,
                                  IReadOnlyList<string> Barcodes, string? ImageUrl,
                                  decimal? Price, string? Currency, bool IsArchived)

ExternalSellerInfo      — record (string? Name, string? LegalName, string? Inn,
                                  string? Ogrn, string? OwnershipForm)

MarketplaceCapabilities — флаги: Warehouses, Cards, Orders, StockPush, SellerInfo
```

Все поля `ExternalSellerInfo` необязательные: площадка может отдавать лишь часть реквизитов, а у самозанятого нет ОГРН. `FetchSellerInfoAsync` вызывается только у провайдеров, объявивших флаг `SellerInfo`.

`IMarketplaceProviderRegistry.Get(MarketplaceType)` резолвит провайдера. `Capabilities` управляет UI: у аккаунта Ozon вкладки «Склады» и «Карточки» активны, вкладка «Заказы» появится, когда провайдер объявит `Orders`.

Различия площадок закрываются на уровне провайдера, а не схемы:

- **Форма учётных данных.** Ozon требует `ClientId` + `ApiKey`, Wildberries — только токен. Провайдер объявляет `RequiresClientId`; валидация запроса опирается на это, поле `ExternalClientId` в БД остаётся `null` для WB.
- **Пагинация.** Обе стратегии Ozon (`cursor`, `last_id`) и любая другая скрыты за `IAsyncEnumerable`.
- **Идентификаторы.** `ExternalId` — всегда `string`, хотя Ozon отдаёт `int64`. Это исключает миграцию, когда следующая площадка использует GUID или строковый nmID.

Возврат `IAsyncEnumerable` пакетами (а не одного списка) обязателен: у продавца могут быть десятки тысяч карточек, и держать их все в памяти не нужно — сервис синхронизации обрабатывает и сохраняет постранично.

---

## Алгоритм синхронизации

### Общие правила

- **Работа выполняется вне запроса.** `POST /sync` отвечает `202` сразу, значит синхронизация не может жить в скоупе запроса. Заявки идут в bounded `Channel` (`MarketplaceSyncQueue`, ёмкость 200, `SingleReader`), их разбирает `BackgroundService` (`MarketplaceSyncWorker`), создавая **свой DI-скоуп на каждый запуск**. `Task.Run` не годится: он не привязан к lifetime приложения, и незавершённые запуски при остановке контейнера теряются молча.
- **Реконсиляция при старте.** Запуск, оставшийся в статусе `Running` после падения, вечно блокирует аккаунт — и UI-проверка, и планировщик отказываются стартовать второй. Первое действие воркера — перевести все зависшие `Running` в `Failed` с кодом `marketplaceSyncInterrupted` и тем же `AppFieldError` откатить сводку затронутых аккаунтов (`LastSyncStatus`, `LastSyncError`, `LastSyncAt`), иначе аккаунт продолжит показывать исход предыдущего, доупавшего запуска.
- Один активный запуск на аккаунт. Гонка «кнопка в UI против Quartz» исключается advisory-локом PostgreSQL (`pg_try_advisory_lock` по хэшу `MarketplaceAccountId`). При занятом локе запрос получает `409 marketplaceSyncAlreadyRunning`.
  Лок **сессионный**, а Npgsql на возврате соединения в пул выполняет `DISCARD ALL`, который его снимает. Поэтому лок берётся на **выделенном `NpgsqlConnection`** из внедрённого `NpgsqlDataSource` и держится весь запуск, а не на соединении request-скоупного `DbContext`. Простаивающему соединению нужен `Keepalive=30` в строке подключения, иначе NAT или файрвол может тихо оборвать сессию и освободить лок.
  Проверка `AnyAsync(Status == Running)` в контроллере — это UX-подсказка ради быстрого `409`, гарантию даёт именно лок.
- Запуск создаёт `MarketplaceSyncRun` в статусе `Running` в отдельной транзакции и коммитит её сразу — прогресс должен быть виден в UI до окончания работы.
- Ошибка провайдера переводит запуск в `Failed`, пишет сообщение в `Error` и `MarketplaceAccount.LastSyncError`. Уже сохранённые страницы не откатываются: частичная синхронизация полезнее полного отката.
- Синхронизация **не пишет в changelog** — тысячи автоматических изменений затопили бы журнал. В журнал попадают только запуск и итог (`sync.started` / `sync.finished` на аккаунте) и ручные действия пользователя.

### Реквизиты продавца

Выполняется **первым шагом любого запуска и вне зависимости от `Scope`** — это один дешёвый запрос, и именно он даёт аккаунту имя. Шаг пропускается, если провайдер не объявил флаг `SellerInfo`.

1. `FetchSellerInfoAsync` → `MarketplaceAccount.Name`, `CompanyLegalName`, `Inn`, `Ogrn`, `OwnershipForm`.
2. Пустое `name` от маркетплейса **не перезаписывает** текущее — иначе аккаунт пропал бы из всех списков.
3. Остальные реквизиты пишутся как есть, включая `null`: реквизит, исчезнувший у продавца, должен исчезнуть и в WMS.

Изменения реквизитов попадают в changelog как диф `sync.finished` на аккаунте — отдельного действия для них нет.

### Склады

1. `FetchWarehousesAsync` — полный список (складов у продавца единицы, пагинация исчерпывается за один-два запроса).
2. Upsert по `(MarketplaceAccountId, ExternalId)`: обновляются `Name`, `Kind`, `ExternalStatus`, `Address`, `SyncedAt`.
3. Склады, не встреченные в выдаче, получают `IsArchived = true`. Удаления нет — архивный склад может быть привязан к `Warehouse`, и удаление порвало бы привязку.
4. Ранее заархивированный склад, снова появившийся в выдаче, получает `IsArchived = false` с сохранением привязки.

**Привязка `WarehouseId` синхронизацией никогда не трогается** — это ручное действие администратора.

### Карточки

1. `/v3/product/list`, страницы по 1000, курсор `last_id` — собираются `product_id`.
2. Каждая страница идентификаторов догружается через `/v3/product/info/list` пакетом до 1000.
3. Пакет уходит в upsert по `(MarketplaceAccountId, ExternalId)`; счётчики `CardsCreated` / `CardsUpdated` инкрементируются.
4. Карточки, не встреченные за запуск, помечаются `IsArchived = true` (сравнение по `SyncedAt < StartedAt` в рамках аккаунта).
5. Для новых карточек с `CatalogItemId == null` выполняется автосопоставление.

Обновление карточки **никогда не сбрасывает `CatalogItemId` и `MappingSource`** — маппинг живёт независимо от данных карточки.

### Автосопоставление

Применяется только к карточкам с `CatalogItemId == null`. Существующая привязка не перезаписывается ни при каких условиях.

1. **По артикулу.** `OfferId` сравнивается с `CatalogItem.Article` без учёта регистра. Кандидаты фильтруются: `IsArchived == false` и `Type ∈ { Standard, Unit, Bundle, Variation }`. Ровно один кандидат → привязка с `MappingSource = AutoOfferId`.
2. **По штрихкоду.** Если по артикулу совпадения нет — любой из `Barcodes` карточки сравнивается с `CatalogItem.Barcode`. Ровно один кандидат → `MappingSource = AutoBarcode`.
3. Ноль кандидатов или больше одного → карточка остаётся несопоставленной. Неоднозначность разрешает человек.

Автосопоставление по штрихкоду до `Variation` и `Bundle` не дотягивается — кандидаты фильтруются явным `Type ∈ { Standard, Unit }`. По соглашению `Barcode` у этих типов не заполняется (см. [items-specification.md](items-specification.md#fields-by-type)), но **схема этого не гарантирует**: `CatalogItem.Barcode` — обычная nullable-строка у всех пяти типов, ограничение нигде не enforced. Полагаться на `null` нельзя, поэтому фильтр по типу задаётся явно. Карточка, за которой стоит вариация, сопоставляется либо по артикулу самой вариации, либо вручную.

Аналогично `Article`: он **non-nullable у всех типов**, null-проверок при сравнении с `OfferId` не требуется.

Отдельный риск неоднозначности: если артикул вариации совпадает с артикулом одного из её же членов, кандидатов окажется два и автосопоставление корректно откажется выбирать. Это сознательное поведение — угадывание уровня привязки здесь дороже ручного разбора.

Ручной запуск автосопоставления по всему аккаунту доступен отдельной кнопкой и подчиняется тем же правилам.

### Допустимые цели маппинга

| Тип `CatalogItem` | Можно привязать карточку | Почему |
|-------------------|--------------------------|--------|
| `Standard` | ✓ | Обычный товар |
| `Unit` | ✓ | Сериализованный товар |
| `Bundle` | ✓ | Комплект, продающийся одной карточкой |
| `Variation` | ✓ | Одна карточка маркетплейса покрывает несколько взаимозаменяемых позиций каталога |
| `ProductGroup` | — | Виртуальная группа; не является компонентом заказа |

Набор совпадает с допустимыми типами `OrderBoxComponent` (см. [orders-specification.md](orders-specification.md)) — это не совпадение: из привязки карточки в итоге вырастает состав заказа. Нарушение → `422 marketplaceCardMappingTypeNotAllowed`.

**Почему `Variation` допустима.** Один и тот же товар часто продаётся под единственной карточкой, хотя на складе лежит несколькими ревизиями — сменилась упаковка, поставщик, партия. Для покупателя это один SKU, для склада — разные `CatalogItem`. Привязка карточки к `Variation` описывает ровно это: карточка задаёт *набор допустимых позиций*, а какая именно ревизия ушла — решается при сборке.

Новой механики это не требует. `Variation` уже допустимый тип `OrderBoxComponent`, а `AssemblyFulfillment.ResolvedCatalogItemId` уже хранит позицию, по которой реально двигались остатки: сборщик выбирает конкретный вариант из ячейки, сервер проверяет его членство в вариации. Разрешение варианта происходит в момент фулфилмента, а не в момент маппинга, — маркетплейсу знать о ревизиях не нужно.

> **Ограничение:** сама `Variation` в остатках не лежит — в инвентарь попадают только `Standard` и `Unit`. Поэтому карточка, привязанная к `Variation`, не имеет собственного остатка; для будущей выгрузки остатков он считается **суммой остатков по членам вариации**.

### Архивная позиция каталога

Установить привязку на архивную позицию нельзя — `422 marketplaceCardMappingArchivedItem`. Но позиция может уйти в архив уже после того, как привязка сделана, и тогда привязка **сохраняется**: рвать её автоматически нельзя, иначе разархивация товара молча потеряла бы ручную работу пользователя.

Вместо этого такая карточка помечается в списке чипом «Привязана к архивному товару». Признак вычисляемый, не хранимый:

```csharp
[Projectable]
public bool IsMappedToArchivedItem => CatalogItem != null && CatalogItem.IsArchived;
```

Фильтр состояния привязки на вкладке «Карточки» получает соответствующее значение — устаревшие привязки должны находиться одним кликом, а не листанием всего списка.

### Планировщик

Quartz регистрируется с in-memory хранилищем задач — согласуется с текущим однонодовым допущением проекта (`SecurityVersionStore` уже документирован как не multi-instance safe).

Одна задача `MarketplaceSyncScanJob` с `[DisallowConcurrentExecution]` запускается по фиксированному крону (по умолчанию раз в минуту) и выбирает аккаунты, у которых `IsActive == true` и `LastSyncAt + SyncIntervalMinutes <= now`, после чего ставит им синхронизацию `Scope = All`.

Сканирующая задача выбрана вместо триггера на аккаунт намеренно: не нужно мутировать расписание планировщика при каждом изменении `SyncIntervalMinutes`, а перезапуск приложения не теряет расписание.

---

## API

Префикс — `api/integrations/marketplaces`. Ответы, пагинация и ошибки — по общим правилам проекта (`Paginated<T>`, `AppProblemDetails`).

| Метод | Путь | Право | Назначение |
|-------|------|-------|------------|
| `GET` | `/accounts` | `integrations.view` | Список аккаунтов (пагинация, поиск) |
| `GET` | `/accounts/{id}` | `integrations.view` | Аккаунт с агрегатами (складов, карточек, несопоставленных) |
| `POST` | `/accounts` | `integrations.edit` | Создание аккаунта |
| `PUT` | `/accounts/{id}` | `integrations.edit` | Изменение (пустой `apiKey` — не менять ключ) |
| `DELETE` | `/accounts/{id}` | `integrations.edit` | Удаление аккаунта со складами и карточками |
| `POST` | `/accounts/{id}/test-connection` | `integrations.edit` | Проверка учётных данных без сохранения |
| `POST` | `/accounts/{id}/sync` | `integrations.map` | Запуск синхронизации, тело `{ scope }` → `202` + `syncRunId` |
| `GET` | `/accounts/{id}/sync-runs` | `integrations.view` | История запусков |
| `GET` | `/accounts/{id}/warehouses` | `integrations.view` | Склады маркетплейса |
| `PUT` | `/warehouses/{id}/mapping` | `integrations.map` | Привязка склада, `{ warehouseId }`, `null` — снять |
| `GET` | `/accounts/{id}/cards` | `integrations.view` | Карточки (поиск, `mappingState` = `all`/`unmapped`/`mapped`/`archivedItem`, `includeArchived`) |
| `PUT` | `/cards/{id}/mapping` | `integrations.map` | Привязка карточки, `{ catalogItemId }`, `null` — снять |
| `POST` | `/accounts/{id}/cards/auto-map` | `integrations.map` | Автосопоставление по всему аккаунту |
| `GET` | `/accounts/unmapped-count` | `integrations.view` | `{ count }` несопоставленных карточек по всем активным аккаунтам — источник данных для бейджа в сайдбаре |

`test-connection` вызывается и для несохранённого аккаунта: тело запроса может содержать `clientId` и `apiKey` напрямую, тогда `{id}` игнорируется. Это позволяет проверить ключ до создания записи.

### Права

```csharp
public static class Integrations
{
    public const string View = "integrations.view";
    public const string Edit = "integrations.edit";
    public const string Map  = "integrations.map";
}
```

Разделение `Edit` и `Map` намеренное: сопоставлять карточки и запускать синхронизацию должен уметь товаровед, а трогать API-ключи — только администратор. Добавление констант в `Infrastructure/Permissions.cs` автоматически регистрирует политики авторизации, попадает в `/api/permissions` и в enum `PermissionName` сгенерированного TS-клиента.

> **`integrations.map` выдаётся в связке с `catalog.view` и `warehouses.view`.** Ячейки привязки — это переиспользуемые `CatalogItemsSelect` и `WarehousesSelect`, которые ходят в `/api/catalog/for-select` и `/api/warehouses`. Без этих двух прав выпадашки получают `403`, и экраны привязки неработоспособны. Расширять авторизацию чужих контроллеров ради интеграций сочтено неоправданным.

> **Ограничение:** права модуля интеграций **не** имеют `_assigned`-вариантов. Аккаунт маркетплейса относится к магазину целиком, а не к конкретному складу, поэтому скоупинг по `AssignedWarehouses` здесь бессмысленен.

### Коды ошибок

Добавляются в `Infrastructure/ErrorCode.cs` отдельной секцией:

| Код | HTTP | Когда |
|-----|------|-------|
| `marketplaceAccountNotFound` | 404 | Аккаунт не найден |
| `marketplaceCredentialsInvalid` | 422 | Маркетплейс отверг `Client-Id`/`Api-Key` |
| `marketplaceCredentialsUnreadable` | 422 | Не удалось расшифровать сохранённый ключ |
| `marketplaceClientIdRequired` | 422 | Провайдер требует `ClientId`, а он не передан |
| `marketplaceApiError` | 502 | Маркетплейс вернул ошибку или недоступен |
| `marketplaceSyncAlreadyRunning` | 409 | По аккаунту уже идёт синхронизация |
| `marketplaceCardMappingTypeNotAllowed` | 422 | Попытка привязать карточку к `ProductGroup` |
| `marketplaceCardMappingArchivedItem` | 422 | Целевая позиция каталога в архиве |
| `marketplaceSyncInterrupted` | — | Запуск прерван остановкой приложения; проставляется реконсиляцией при старте, наружу по HTTP не отдаётся |
| `marketplaceWarehouseNotFound` | 404 | Склад маркетплейса не найден |
| `marketplaceCardNotFound` | 404 | Карточка маркетплейса не найдена |

Значения `ErrorCode` для этой секции дописываются **только в конец** enum'а — они персистятся числом в jsonb-колонках `Error` и `LastSyncError`.

### Changelog

Добавляются значения `AppEntityType`: `MarketplaceAccount`, `MarketplaceCard`. **Только в конец enum'а** — он персистится в `ChangeLogEntry.EntityType` как `int`, и вставка в середину молча переинтерпретировала бы все существующие записи журнала.

| Действие | `action` | `actionData` |
|----------|----------|--------------|
| Создание аккаунта | `account.created` | `{ marketplace }` |
| Изменение аккаунта | `account.updated` | `{ marketplace }` |
| Ротация ключа | `account.key_rotated` | `{ marketplace }` — без значений ключа |
| Удаление аккаунта | `account.deleted` | `{ marketplace }` |
| Итог синхронизации | `sync.finished` | `{ syncRunId, status, cardsCreated, cardsArchived, autoMapped }` |
| Ручная привязка карточки | `mapping.set` | `{ catalogItemId, source: "manual" }` |
| Снятие привязки | `mapping.cleared` | — |
| Автосопоставление | `mapping.auto` | `{ matched, remaining }` |

**Записи `sync.started` нет.** `AbstractChangeLogService` пишет запись только при непустом диффе `before`/`after`, а старт синхронизации сам по себе состояние аккаунта не меняет — запись либо не создалась бы вовсе, либо пришлось бы подделывать тип `Added`. Факт запуска и так виден в `MarketplaceSyncRun` со статусом `Running`, который создаётся и коммитится сразу; в журнал попадает итог. По той же причине `mapping.auto` пишется через дифф аккаунта (меняется `unmappedCardCount`) — прогон, не сопоставивший ничего, записи не создаёт.

Фоновая синхронизация выполняется без пользователя — `ChangeLogEntry.UserId` остаётся `null`, что схема допускает.

---

## Фронтенд

Раздел встраивается в `SettingsPage` одной записью в `settingsConfig.tsx`:

```
{
  path: "integrations",
  label: "Интеграции",
  icon: <StorefrontIcon />,
  component: MarketplacesSettingsPage,
  requiredPermission: "integrations.view",
  subroutes: [{ path: "new" }, { path: ":id" }],
}
```

Правки в `App.tsx` не требуются — `SidebarPage` строит вложенные маршруты сам.

```
src/pages/SettingsPage/pages/MarketplacesSettingsPage/
├── MarketplacesSettingsPage.tsx      — список аккаунтов
├── pages/
│   ├── MarketplaceAccountCreatePage/ — форма подключения
│   └── MarketplaceAccountPage/       — карточка аккаунта с вкладками
│       ├── AccountOverviewTab.tsx    — статус, ключ, интервал, кнопка синхронизации
│       ├── AccountWarehousesTab.tsx  — таблица складов + привязка
│       ├── AccountCardsTab.tsx       — таблица карточек + привязка
│       └── AccountSyncRunsTab.tsx    — история синхронизаций
└── components/
    ├── MarketplaceStatusChip.tsx
    ├── CardMappingChip.tsx           — «авто» / «вручную» / «архивный товар»
    ├── WarehouseMappingCell.tsx      — на базе WarehousesSelect
    └── CardMappingCell.tsx           — на базе CatalogItemsSelect
```

Существующие компоненты переиспользуются: `WarehousesSelect`, `CatalogItemsSelect`, `CatalogItemLink`, `DataTableContainer`, `FiltersBar`, `SearchInput`, `PageGenericHeader`, `ConfirmDialog`.

После добавления эндпоинтов на бэкенде — `npm run generate-api` (backend должен быть запущен). Дальше `npm run typecheck` **ожидаемо упадёт в двух местах** — оба типа исчерпывающие:

- `src/utils/permissionLabels.ts` — `Record<PermissionName, string>`, нужны три новых права;
- `src/utils/appEntityUtils.tsx` — `Record<AppEntityType, EntityTypeConfig>`, нужны `marketplaceAccount` и `marketplaceCard`.

Ошибки `lastSyncError` и `syncRun.error` приходят как `AppFieldError` (`{ code, detail, args? }`) — тот же тип, что внутри `AppProblemDetails`, он уже сгенерирован. Текст берётся по `code` + `args` через существующий `errorCodeArgMessages` в `src/utils/errorUtils.ts`; поле `detail` англоязычное и в UI не показывается.

Вкладок в приложении пока нет ни одной (`<Tabs>` используется только в мобильной навигации `SidebarLayout` и в пикере `StorageNodePickerContent`) — страница аккаунта вводит этот паттерн.

Бейджа-счётчика в сайдбаре тоже нет: `SectionConfig` не имеет соответствующего поля, а `settingsConfig.tsx` — обычный модульный массив и хуки вызывать не может. Счётчик придётся передавать **компонентом** (`badge?: React.ComponentType`), который сам дёргает `/accounts/unmapped-count`; `toNavItems` уже фильтрует секции по `requiredPermission`, так что бейдж и его запрос смонтируются только у тех, кому можно.

Счётчики активного запуска обновляются по событиям `marketplace.sync.progress` и `marketplace.sync.finished` — транспорт и схема описаны в [realtime-specification.md](realtime-specification.md). Опрос `/sync-runs` через `refetchInterval` остаётся запасным механизмом и включается, только когда стрим не установлен: замерший экран из-за проблем с транспортом недопустим.

---

## Базовый флоу пользователя

### Шаг 1 — Подключение магазина

1. Администратор открывает «Настройки» → «Интеграции» → «Подключить магазин».
2. Выбирает площадку (Ozon), вводит название магазина, `Client-Id` и `Api-Key`, задаёт интервал синхронизации (по умолчанию 30 минут).
3. Нажимает «Проверить подключение» — бэкенд дёргает `/v2/warehouse/list` с `limit = 1`. Успех подтверждается зелёной плашкой, ошибка — `marketplaceCredentialsInvalid` с текстом от маркетплейса.
4. Сохраняет. Ключ шифруется, наружу больше не отдаётся — в интерфейсе видна только маска `••••1234`.

### Шаг 2 — Первая синхронизация

1. Сразу после создания аккаунта автоматически запускается синхронизация со `scope = All`.
2. На карточке аккаунта видны статус `Running` и живые счётчики: складов, карточек создано/обновлено, автосопоставлено.
3. По завершении статус меняется на «Синхронизировано», проставляется время последней синхронизации.

### Шаг 3 — Привязка складов

1. Вкладка «Склады»: таблица складов Ozon — название, тип (FBS/rFBS/Express), статус, колонка «Склад WMS».
2. Администратор в каждой строке выбирает склад проекта. Привязка сохраняется сразу, без отдельной кнопки.
3. Непривязанные склады подсвечены предупреждением: без привязки будущая синхронизация заказов не сможет определить, на каком складе собирается заказ.

### Шаг 4 — Привязка карточек

1. Вкладка «Карточки»: таблица с изображением, названием, артикулом (`offer_id`), SKU, колонкой «Позиция каталога».
2. Фильтр по состоянию привязки — «Все / Не сопоставленные / Сопоставленные / Привязаны к архивному товару», отдельный переключатель «Показывать архивные карточки». По умолчанию открывается фильтр «Не сопоставленные» — это рабочий список.
3. Кнопка «Сопоставить автоматически» прогоняет матчинг по артикулу и штрихкоду и показывает итог: «Сопоставлено 143, требует ручного разбора 12».
4. Остаток разбирается вручную через `CatalogItemsSelect`. Источник привязки виден чипом «авто» / «вручную» — понятно, что проверял человек, а что машина.
5. Если нужной позиции в каталоге нет — переход в каталог, создание позиции, возврат к списку.

### Шаг 5 — Повседневная работа

- Quartz синхронизирует активные аккаунты по интервалу. Новые карточки приезжают несопоставленными.
- В сайдбаре у раздела «Интеграции» отображается бейдж с количеством несопоставленных карточек по всем активным аккаунтам.
- Ручной запуск синхронизации доступен кнопкой в любой момент; повторный запуск при активном — `409`.
- Аккаунт можно деактивировать (`IsActive = false`) — фоновая синхронизация прекращается, все данные и привязки сохраняются. Удаление аккаунта каскадно удаляет склады и карточки и блокируется, если карточки участвуют в заказах.

---

## Конфигурация и развёртывание

```
"Marketplaces": {
  "KeyRingPath": "/keys",
  "SyncScanCron": "0 * * * * ?",
  "DefaultSyncIntervalMinutes": 30,
  "Ozon": {
    "BaseUrl": "https://api-seller.ozon.ru",
    "TimeoutSeconds": 60,
    "PageDelayMs": 200
  }
}
```

В `docker-compose.yml` и `docker-compose.prod.yml` добавляется том для кольца ключей Data Protection:

```
volumes:
  - dataprotection-keys:/keys
```

Утрата тома делает сохранённые `Api-Key` нерасшифровываемыми — том обязателен к бэкапу наравне с БД.

---

## Статус реализации

### Реализовано

**Бэкенд целиком.** Существующие швы в домене, на которые модуль опирается: `Order.MarketplaceOrderId`, `OrderMarketplaceItem.MarketplaceCardId`, `OrderType.FBS` / `OrderType.FBO` (идентификаторы в коде — заглавными).

| Шаг | Статус |
|-----|--------|
| 1. Конвейер генерации: `paths.whitelist.json`, `generate-client.cs`, сгенерированный `OzonApiClient` | ✓ |
| 2. `Integrations/Abstractions` — провайдер-контракты, канонические модели, `MarketplaceApiException` | ✓ |
| 3. Домен + миграция `AddMarketplaces` | ✓ |
| 4. Data Protection, `IMarketplaceCredentialProtector`, `MarketplaceRequestContext`, `OzonAuthHandler` | ✓ |
| 5. `OzonClient` + `OzonMarketplaceProvider` | ✓ |
| 6. `MarketplaceSyncService`, очередь + воркер, advisory-лок, автосопоставление | ✓ |
| 7. Quartz `MarketplaceSyncScanJob` | ✓ |
| 8. `Permissions.Integrations`, коды ошибок, `AppEntityType`, changelog-сервисы, DTO, `AppMapperProfile` | ✓ |
| 9. `MarketplacesController` | ✓ |
| 10. Фронтенд: раздел настроек, вкладки, компоненты привязки, бейдж | — не начат |
| 11. Документация | ✓ (кроме [frontend.md](frontend.md) — ждёт шага 10) |

Регенерация TS-клиента (`npm run generate-api`) выполняется вместе с шагом 10.

### Проверено вручную

Через Scalar и прямые HTTP-вызовы, с заведомо неверным ключом: `test-connection` → `502 marketplaceApiError` с `args.marketplaceStatus`; `POST /sync` → мгновенный `202` + `syncRunId`; фоновый воркер берёт advisory-лок, ходит в реальный Ozon, кладёт структурированный `AppFieldError` в jsonb и зеркалит его в `LastSyncError`; `sync-runs`, `warehouses`, `cards`, `auto-map`, `unmapped-count` отвечают; changelog получает `account.created` и `sync.finished`; `DELETE` каскадно чистит аккаунт.

Синхронизация с боевым ключом Ozon (реальные склады и карточки, автосопоставление, архивация по `SyncedAt`) **не проверялась** — нужен настоящий аккаунт продавца.

### Отложено

- **Синхронизация заказов FBS** — `/v4/posting/fbs/list`, `/v3/posting/fbs/get`. Создание `Order` с `Type = Fbs`, заполнение `MarketplaceOrderId` и `MarketplaceItems`; при повторной синхронизации обновляются только метаданные и нераспознанные позиции, статус и задания на сборку не трогаются (правило зафиксировано в [orders-specification.md](orders-specification.md)).
- **Синхронизация заказов FBO** — `/v3/posting/fbo/list`.
- **Выгрузка остатков в маркетплейс** — `/v2/products/stocks`. Требует однозначной привязки склада: остаток по `CatalogItem` агрегируется в рамках `Warehouse`, привязанного к складу маркетплейса. Для виртуальных целей маппинга остаток вычисляется, а не читается напрямую: `Variation` → сумма остатков по членам вариации, `Bundle` → минимум по компонентам с учётом их количеств. Обход дерева здесь тот же, что уже делает `ICatalogService.ComputeContainsUnitAsync`.
- **Wildberries** — второй провайдер. Схема БД изменений не требует; нужны `WildberriesMarketplaceProvider`, конвейер генерации его клиента и учёт того, что `ClientId` там не используется.
- **Обновление цен** — `/v1/product/import/prices`.

---

## Принятые решения

Вопросы, разобранные при проектировании, и принятый по ним выбор — чтобы к ним не возвращались повторно.

| Вопрос | Решение | Следствие |
|--------|---------|-----------|
| Кратность привязки складов | **N:1** — несколько складов маркетплейса на один `Warehouse` | Оверселлинг при выгрузке остатков — известный принятый долг, разбирается на этапе выгрузки |
| Отдельное право на маппинг | **Да**, `integrations.map` отдельно от `integrations.edit` | Товаровед сопоставляет и запускает синхронизацию, к API-ключам доступа не имеет |
| Хранение сырого ответа маркетплейса | **Нет**, `RawPayload` не заводится | Новое поле добавляется колонкой и заполняется следующей плановой синхронизацией |
| Карточка привязана к архивной позиции | Привязка **сохраняется**, показывается чипом «Привязана к архивному товару» | `[Projectable] IsMappedToArchivedItem` + значение фильтра `archivedItem` |
| Остаток карточки, привязанной к `Variation` | **Сумма** остатков по членам вариации | Завышает доступность, если ревизии не полностью взаимозаменяемы; принято осознанно |
| Отмена запущенной синхронизации | **Не нужна** в первой версии | Значение `MarketplaceSyncStatus.Canceled` остаётся зарезервированным и в UI не используется |
| Хранение ошибок запуска | **`AppFieldError` в jsonb**, а не строка | Фронт локализует по `code` + `args`; `ErrorCode` можно только дописывать в конец |
| Клиент Ozon | **Генерация NSwag**, библиотекой в процессе, без `.nswag`-конфига | Следующие методы бесплатны; ценой — санитайзер для Swagger-2.0-наследия в спеке Ozon |
| Живые счётчики синхронизации | **Опрос** `/sync-runs`; SSE отложен | Бэкенд ничего не публикует; переход на realtime — подмена одного хука на фронте |

---

## Открытые вопросы

На момент написания открытых вопросов по модулю не осталось — все зафиксированы в разделе выше.
