# Спецификация интеграции с маркетплейсами

## Обзор

Модуль интеграций подключает к WMS внешние торговые площадки. Первая реализуемая площадка — **Ozon Seller API**; архитектура изначально рассчитана на подключение **Wildberries** вторым провайдером без переделки схемы БД.

Первая версия покрывает четыре задачи:

1. Подключение магазина — хранение учётных данных (`Client-Id` + `Api-Key`) в зашифрованном виде.
2. Синхронизация складов маркетплейса и их привязка к `Warehouse` проекта.
3. Синхронизация карточек товаров маркетплейса и их привязка к `CatalogItem`.
4. Наблюдаемость — история запусков синхронизации, статусы, ошибки.

Вторая версия добавляет пятую задачу:

5. **Синхронизация заказов FBS** — импорт отправлений маркетплейса в заказы WMS, отслеживание их статуса на площадке и печать этикеток с артикулами WMS. См. раздел [«Синхронизация заказов FBS»](#синхронизация-заказов-fbs).

Синхронизация заказов FBO и обратная выгрузка остатков в маркетплейс не входят ни в одну из версий — см. раздел «Отложено». Схема данных проектируется так, чтобы обе задачи подключались добавлением сущностей, а не переделкой существующих.

> **Контекст:** домен заказов содержал швы под маркетплейс — `Order.MarketplaceOrderId` и `OrderMarketplaceItem.MarketplaceCardId` (см. [orders-specification.md](orders-specification.md)). Синхронизация заказов оба шва переделывает: `MarketplaceOrderId` заменяется выделенной сущностью `MarketplaceOrder`, а `MarketplaceCardId` из строки без внешнего ключа становится настоящим FK на `MarketplaceCard`. Обоснование — в разделе [«Заказ маркетплейса»](#заказ-маркетплейса-marketplaceorder).

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
| `POST /v4/posting/fbs/unfulfilled/list` | `PostingFbsUnfulfilledList` | Отправления FBS, не переданные в доставку | Пагинация по `cursor` + `limit` 1…100 |
| `POST /v3/posting/fbs/get` | `PostingAPI_GetFbsPostingV3` | Одно отправление по `posting_number` | Одно отправление за запрос |
| `POST /v2/posting/fbs/package-label` | `PostingAPI_PostingFBSPackageLabel` | PDF с этикетками отправлений | Не больше 20 номеров за запрос; только статус `awaiting_deliver` |

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

Поля ответа `/v4/posting/fbs/unfulfilled/list` (элемент `postings[]`), релевантные WMS:

```
posting_number      — string, номер отправления — первичный внешний идентификатор
order_number        — string, номер заказа, к которому относится отправление
status              — string, статус отправления
substatus           — string, подстатус
in_process_at       — date-time, начало обработки отправления
shipment_date       — date-time, до какого времени нужно собрать → PlannedShipmentAt
tracking_number     — string
multi_box_qty       — int, количество коробок
is_multibox         — bool
delivery_method     — { id, name, warehouse_id, warehouse, tpl_provider }
products[]          — { sku, offer_id, name, quantity, price, product_color }
```

`/v3/posting/fbs/get` отдаёт то же самое в `result` (тип `v3FbsPostingDetail`) плюс `cancellation` — причину отмены.

> **Найдено при реализации: спецификация сама себе противоречит по ответу `/v2/posting/fbs/package-label`.** Ответ 200 объявлен под media type `application/pdf`, но со схемой JSON-объекта `{ file_content (format: byte), file_name, content_type }`; ошибки при этом честно `application/json` → `rpcStatus`. Верить нельзя ни тому, ни другому, поэтому схема успешного ответа принудительно нормализуется в `type: string, format: binary` на шаге обрезки (ключ `binaryResponses` в whitelist-е) — NSwag тогда детерминированно генерирует `Task<FileResponse>`, а **что именно приехало, решают байты**: префикс `%PDF` → готовый PDF, первый непробельный байт `{` → JSON-конверт с base64, пустое тело → «ещё не готово». Один PDF на всю пачку отправлений.

> **Найдено при реализации: в `products[]` отправления нет `product_id`.** Ни `posting.v4...Postings.Products`, ни `v3PostingProductDetail` его не содержат — только `sku` и `offer_id`. А `MarketplaceCard.ExternalId` — это именно `product_id`. Поэтому позиция отправления сопоставляется с карточкой **по `Sku`, затем по `OfferId`**; ради этого заведён индекс `(MarketplaceAccountId, Sku)`.

### Как Ozon представляет цвета и размеры

Отдельной сущности «вариант товара» в API **нет**. Каждый цвет и каждый размер — это самостоятельный товар со своим `product_id`, `offer_id` и `sku`; объединение их в одну карточку на витрине чисто визуальное:

- `/v3/product/info/list` отдаёт `model_info: { model_id, count }` — `model_id` общий у всех товаров, объединённых на одной карточке, `count` — сколько их;
- значения цвета и размера лежат в характеристиках товара (`/v4/product/info/attributes` → `attributes[] { id, values[] }`), а какой `attribute_id` означает «Цвет», а какой «Размер», зависит от типа товара и выясняется через `/v1/description-category/attribute`;
- в отправлении FBS цвет приезжает готовой строкой в `products[].product_color`.

**Следствие для WMS: модель менять не нужно.** Раз каждый цвет/размер — отдельный товар со своим `offer_id`, он естественно ложится на одну `MarketplaceCard`, а та привязывается к своему `CatalogItem`. `ModelId` и характеристики **не сохраняются**: группировка карточек по модели в UI и разбор атрибутов дали бы ещё один постраничный обход всего каталога Ozon плюс справочник категорий, а рабочего сценария за этим нет.

### Методы для будущих этапов

Не генерируются, но зарезервированы в whitelist-е спецификации:

| Метод | Назначение |
|-------|------------|
| `POST /v2/products/stocks` | Обновление остатков в Ozon |
| `POST /v2/product/info/stocks-by-warehouse/fbs` | Остатки FBS по складам |
| `POST /v3/posting/fbo/list` | Отправления FBO |

`POST /v4/posting/fbs/list` в whitelist **не входит**: его фильтр требует `since` и `to` (`required: ["since", "to"]`), то есть заставляет держать окно по датам и рисковать пропущенными заказами на его границе. Та же выборка без обязательного окна доступна через `/v4/posting/fbs/unfulfilled/list` — см. [«Обнаружение отправлений»](#обнаружение-отправлений).

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

- `MarketplaceAccountDto` **не содержит поля ключа** — физически нечему протечь наружу. Отдаётся только `apiKeyLast4` (хвост ключа, маску `••••1234` рисует клиент) и `apiKeyUpdatedAt`.
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
├── Status                — MarketplaceWarehouseStatus (Active | Inactive | Unavailable)
├── ExternalStatus        — string?, статус площадки как есть, только для диагностики
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

### Заказ маркетплейса (`MarketplaceOrder`)

Расширение заказа WMS данными площадки. Отдельной страницы и списка у сущности нет — она всегда читается вместе с `Order` и инлайнится в его DTO.

```
MarketplaceOrder
├── OrderId               — Guid → Order (Cascade), одновременно первичный ключ
├── MarketplaceAccountId  — Guid → MarketplaceAccount (Restrict)
├── PostingNumber         — string, номер отправления
├── ExternalOrderNumber   — string?, номер заказа площадки, к которому относится отправление
├── Status                — MarketplaceOrderStatus, нормализованный статус
├── RawStatus             — string?, статус площадки как есть  ─┐ только диагностика
├── RawSubstatus          — string?, подстатус как есть         ─┘
├── ShipmentDate          — DateTime?, до какого времени собрать
├── InProcessAt           — DateTime?, начало обработки на площадке
├── TrackingNumber        — string?
├── DeliveryMethodName    — string?
├── MultiBoxQty           — int
├── LabelFileId           — Guid? → DataFile (Restrict)
├── LabelFetchedAt        — DateTime?
├── LabelError            — AppFieldError? (jsonb)
├── StatusSyncedAt        — DateTime, когда последний раз сверялся статус
└── SyncedAt              — DateTime

Уникальный индекс: (MarketplaceAccountId, PostingNumber)
Индексы: (MarketplaceAccountId, Status)
```

**Первичный ключ общий с `Order` (shared primary key).** Связь строго 1:1, отдельный `Guid Id` не нужен ни для чего: сущность не адресуется по HTTP и не появляется ни в одном списке. `IHasIdentity` она **не реализует** — интерфейс подразумевает самостоятельный объект с changelog-историей, а история изменений здесь ведётся на `Order`.

**Почему выделенная сущность, а не поля на `Order`.** Полей четырнадцать, и у прямых заказов все они `null` — `Order` обслуживает три типа заказов и обрастать площадочными атрибутами не должен. Решающий довод другой: чтобы сходить за этикеткой, нужно знать, **чьими ключами** идти, а места под `MarketplaceAccountId` в домене заказов не было вовсе.

**`Order.MarketplaceOrderId` удаляется.** Строковое поле дублировало бы `PostingNumber`, а два источника одного и того же номера рано или поздно разъедутся. Следствия:

- `Order.SearchString` становится `Number + " " + Notes + " " + MarketplaceOrder.PostingNumber` — поиск по номеру отправления сохраняется ценой `LEFT JOIN`;
- `MarketplaceOrder` попадает в `Include`/`ProjectTo` заказа, а `MarketplaceOrderId` уходит из `OrderDto` и `OrderListItemDto`, уступая место вложенному `MarketplaceOrderDto`;
- **FBO, когда до него дойдут руки, обязан ездить через ту же сущность** — другого места под номер отправления в домене не осталось. Это плюс: одна точка на обе схемы.

**`OrderMarketplaceItem.MarketplaceCardId` становится настоящим FK** `Guid? → MarketplaceCard (Restrict)`. Прежнее обоснование строковой ссылки — «заказ может приехать с карточкой, которой ещё нет в WMS» — снимается правилом [«заказ с непривязанным товаром не синхронизируется»](#непривязанные-товары-и-склады): к моменту создания заказа карточка гарантированно существует и привязана к каталогу. Строковый вариант вдобавок был неоднозначен — `ExternalId` уникален только в рамках аккаунта, а аккаунта в заказе раньше не хранилось.

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
├── OrdersProcessed       — int  ─┐
├── OrdersCreated         — int   │ заполняются только при Scope = Orders
├── OrdersUpdated         — int   │
├── OrdersSkipped         — int  ─┘
├── SkippedOrders         — jsonb SkippedOrderInfo[]?  — почему заказы не создались
└── Error                 — AppFieldError? (jsonb)

Индекс: (MarketplaceAccountId, StartedAt DESC)
```

`SkippedOrderInfo` — `{ PostingNumber, Reason, OfferIds[] }`, где `Reason` — тот же `ErrorCode`, что и в `AppFieldError` (`marketplaceOrderCardNotMapped` или `marketplaceOrderWarehouseNotMapped`). Список **ограничен первыми 100 записями**; сколько заказов пропущено всего, говорит `OrdersSkipped`. Без потолка прогон по магазину с неразобранным каталогом раздул бы одну строку таблицы на мегабайты, а UI всё равно не показывает больше сотни.

Пропуск заказа обязан быть виден. Молча не создать заказ — худший вариант отказа: на складе о нём узнают в момент, когда отгружать уже поздно. Поэтому причина пропуска сохраняется структурно и показывается в модалке синхронизации сразу по завершении прогона.

**Ошибки хранятся структурно, а не строкой.** `Error` и `LastSyncError` — это `AppFieldError` (`{ Code, Detail, Args }`), тот же тип, что лежит внутри `AppProblemDetails.Errors`. Что это даёт:

- фронт получает машиночитаемый `code` и рисует нужное действие, а не парсит текст;
- `Args` уносит контекст (`marketplaceStatus`, `marketplaceResponse`, `accountId`) без склейки в сообщение;
- тип уже есть в сгенерированном TS-клиенте — он часть `AppProblemDetails`, отдельной работы на фронте не нужно.

Собирать **только** через `AppProblems.MakeError(code, message, args)` — он же проставляет `Detail` в каноническом формате `"camelCaseCode: message"`. Текст `Detail` — англоязычный, для разработчика; локализация делается на фронте по `code` + `args`.

> **Нюанс сериализации.** jsonb пишет сериализатор Npgsql (`EnableDynamicJson()`), а не MVC-шный с `JsonStringEnumConverter`. Значит `ErrorCode` внутри колонки лежит **числом**, а наружу через DTO уезжает camelCase-строкой. Следствие: номера `ErrorCode` **проставлены явно и не перенумеровываются** — новый код берёт следующий свободный номер и объявляется там, где ему место по смыслу; менять номер у существующего значит переинтерпретировать уже записанные ошибки.

### Перечисления

| Перечисление | Значения |
|--------------|----------|
| `MarketplaceType` | `Ozon = 0`, `Wildberries = 1` |
| `MarketplaceWarehouseKind` | `Unknown = 0`, `Fbs = 1`, `Rfbs = 2`, `Express = 3`, `Fbo = 4` |
| `MarketplaceMappingSource` | `Manual = 0`, `AutoOfferId = 1`, `AutoBarcode = 2` |
| `MarketplaceSyncScope` | `Warehouses = 0`, `Cards = 1`, `Orders = 3`, `All = 2` |
| `MarketplaceSyncStatus` | `Running = 0`, `Success = 1`, `Failed = 2`, `Canceled = 3` |
| `MarketplaceOrderStatus` | `Unknown = 0`, `AwaitingDeliver = 1`, `Delivering = 2`, `Delivered = 3`, `Cancelled = 4`, `Arbitration = 5` |

`MarketplaceSyncScope.Orders` имеет номер `3` — значение персистится числом в `MarketplaceSyncRun.Scope` и не перенумеровывается. Заказы намеренно не входят в `All`: складам и карточкам хватает фонового интервала, а заказы запускаются отдельным пользовательским действием (см. [«Планировщик и запуск»](#планировщик-и-запуск)).

`MarketplaceOrderStatus` — нормализованный набор состояний, схлопывать словарь площадки обязан **провайдер**, как это уже сделано для `MarketplaceWarehouseStatus`. Для Ozon: `awaiting_deliver` → `AwaitingDeliver`; `delivering`, `driver_pickup`, `sent_by_seller` → `Delivering`; `delivered` → `Delivered`; `cancelled`, `not_accepted` → `Cancelled`; `arbitration`, `client_arbitration` → `Arbitration`; всё незнакомое → `Unknown` с `LogWarning`. `Unknown = 0` по той же причине, что `MarketplaceWarehouseStatus.Unavailable = 0`: неизвестное состояние не должно выглядеть рабочим.

### Связь с существующим доменом

```
MarketplaceWarehouse.WarehouseId       ──> Warehouse.Id       (Restrict)
MarketplaceCard.CatalogItemId          ──> CatalogItem.Id     (Restrict)
MarketplaceOrder.OrderId               ──> Order.Id           (Cascade, он же PK)
MarketplaceOrder.MarketplaceAccountId  ──> MarketplaceAccount (Restrict)
MarketplaceOrder.LabelFileId           ──> DataFile.Id        (Restrict)
OrderMarketplaceItem.MarketplaceCardId ──> MarketplaceCard.Id (Restrict)
```

`Restrict` на привязках склада и карточки: удаление склада или позиции каталога, на которую ссылается карточка маркетплейса, должно явно блокироваться, а не тихо обнулять маппинг.

`Restrict` на `MarketplaceOrder.MarketplaceAccountId` меняет поведение удаления аккаунта: раньше `DELETE /accounts/{id}` каскадно сносил склады и карточки, теперь аккаунт с импортированными заказами удалить нельзя — `409 marketplaceAccountHasOrders`. Каскад здесь означал бы удаление заказов вместе с историей сборки и движениями остатков.

`Restrict` на `LabelFileId` — общее правило подсистемы файлов ([data-files-specification.md](data-files-specification.md#правила-ondelete)): ссылка на `DataFile` всегда через настоящий FK и всегда `Restrict`, иначе сборщик мусора посчитает файл осиротевшим.

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
├── Task<ExternalSellerInfo> FetchSellerInfoAsync(MarketplaceCredentials, ct)
├── IAsyncEnumerable<IReadOnlyList<ExternalPosting>> FetchActivePostingsAsync(MarketplaceCredentials, ct)
├── Task<IReadOnlyList<ExternalPostingStatus>> FetchPostingStatusesAsync(MarketplaceCredentials, IReadOnlyList<string> postingNumbers, ct)
└── Task<ExternalLabelDocument> FetchLabelDocumentAsync(MarketplaceCredentials, IReadOnlyList<string> postingNumbers, ct)

MarketplaceCredentials  — record (string? ClientId, string ApiKey)
ExternalWarehouse       — record (string ExternalId, string Name, MarketplaceWarehouseKind Kind,
                                  MarketplaceWarehouseStatus Status, string? RawStatus, string? Address)
ExternalCard            — record (string ExternalId, string? Sku, string OfferId, string Name,
                                  IReadOnlyList<string> Barcodes, string? ImageUrl,
                                  decimal? Price, string? Currency, bool IsArchived)

ExternalSellerInfo      — record (string? Name, string? LegalName, string? Inn,
                                  string? Ogrn, string? OwnershipForm)

ExternalPosting         — record (string PostingNumber, string? ExternalOrderNumber,
                                  MarketplaceOrderStatus Status, string? RawStatus, string? RawSubstatus,
                                  string? WarehouseExternalId, string? DeliveryMethodName,
                                  DateTime? ShipmentDate, DateTime? InProcessAt,
                                  string? TrackingNumber, int MultiBoxQty,
                                  IReadOnlyList<ExternalPostingItem> Items)

ExternalPostingItem     — record (string? Sku, string OfferId, string Name, int Quantity)

ExternalPostingStatus   — record (string PostingNumber, MarketplaceOrderStatus Status,
                                  string? RawStatus, string? RawSubstatus, string? TrackingNumber)

ExternalLabelDocument   — record (bool IsReady, IReadOnlyList<string> PostingNumbers,
                                  string? ContentType, byte[]? Content)

MarketplaceCapabilities — флаги: Warehouses, Cards, Orders, Labels, StockPush, SellerInfo
```

**`ExternalPostingItem` не несёт ссылки на карточку** — в отправлении нет `product_id` (см. раздел исходных данных). Разрешение позиции в `MarketplaceCard` по `Sku`, затем по `OfferId`, делает сервис синхронизации.

`ExternalLabelDocument.IsReady = false` — это **не ошибка**, а штатный ответ «площадка ещё не сформировала этикетку». Ozon прямо рекомендует запрашивать этикетку через 45–60 секунд после сборки отправления и отвечает `The next postings aren't ready`. Провайдер распознаёт этот случай и возвращает `IsReady = false` вместо `MarketplaceApiException` — иначе временная неготовность выглядела бы как отказ интеграции. Распознавание живёт в одном общем хелпере, потому что Ozon сообщает о неготовности **двумя способами**: и как 200 с JSON-телом, и как 400/409.

`ExternalLabelDocument` несёт `ContentType`, потому что формат этикетки у площадок разный: Ozon отдаёт PDF, Wildberries — растровый или SVG-стикер. Сборщик итогового файла обязан уметь и то, и другое: PDF-страницы берутся как есть, картинка заворачивается в страницу. *(Формат стикеров WB по их спецификации не проверялся — уточняется при подключении провайдера.)*

**Документ отдаётся на всю пачку целиком, а не по отправлению.** Первоначально контракт задумывался как `IReadOnlyList<ExternalLabel>` — по записи на отправление, — но это заставило бы провайдера резать PDF, то есть тащить туда PDFsharp, хотя та же спецификация помещает нарезку и пачки в сервис. Провайдер отвечает ровно за то, что вернула площадка; страницы отправлениям сопоставляет сервис — см. [«Получение этикеток»](#получение-этикеток).

Все поля `ExternalSellerInfo` необязательные: площадка может отдавать лишь часть реквизитов, а у самозанятого нет ОГРН. `FetchSellerInfoAsync` вызывается только у провайдеров, объявивших флаг `SellerInfo`.

`IMarketplaceProviderRegistry.Get(MarketplaceType)` резолвит провайдера. `Capabilities` управляет UI: аккаунт без флага `Orders` не предлагается в модалке синхронизации заказов, без флага `Labels` — не даёт скачивать этикетки.

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
2. Upsert по `(MarketplaceAccountId, ExternalId)`: обновляются `Name`, `Kind`, `Status`, `ExternalStatus`, `Address`, `SyncedAt`.

   `Status` — площадко-независимое состояние (`Active` / `Inactive` / `Unavailable`), схлопывание словаря площадки делает провайдер, а не сервис синхронизации: `ExternalWarehouse` приезжает уже с нормализованным `Status` и сырым `RawStatus`. Для Ozon: `created` → `Active`, `disabled` → `Inactive`, всё остальное (`new`, `disabled_due_to_limit`, `blocked`, `error`, незнакомое, `null`) → `Unavailable`. Поэтому `Unavailable = 0` — незнакомый статус не должен выглядеть рабочим складом. Статус вне известного словаря пишется в лог `LogWarning` — иначе расширение словаря площадкой прошло бы незамеченным.
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

## Синхронизация заказов FBS

### Главный принцип

**Синхронизируются только отправления в статусе `awaiting_deliver` и в статусах, наступающих после него.** Это принципиальное ограничение, из которого следует всё остальное:

- `awaiting_deliver` наступает после того, как отправление собрано и разбито на упаковки **на стороне площадки**. WMS в этом не участвует и метод `/v4/posting/fbs/ship` не вызывает — заказ приезжает уже разделённым и с известным `multi_box_qty`.
- Этикетку Ozon печатает **только** для `awaiting_deliver`. Ограничившись этим статусом, мы получаем заказы, для которых этикетка доступна сразу, а не «когда-нибудь».
- Отправления в `awaiting_packaging` и более ранних состояниях в WMS не попадают вовсе — собирать их нечем, они ещё не сформированы.

**Статус на площадке идёт параллельно статусу WMS и никогда его не двигает.** Заказ создаётся в `OrderStatus.Confirmed` — то есть готовым к сборке — и дальше живёт по своей статусной машине ([orders-specification.md](orders-specification.md)). `MarketplaceOrder.Status` обновляется синхронизацией независимо и служит информацией для человека, а не триггером. Автоматических переходов между двумя статусными машинами нет ни в одну сторону: ни отмена на площадке не откатывает сборку, ни отгрузка в WMS не сообщается площадке.

### Обнаружение отправлений

Источник новых заказов — `POST /v4/posting/fbs/unfulfilled/list` с фильтром `statuses: ["awaiting_deliver"]`, курсорная пагинация, `limit` 100.

Именно `unfulfilled/list`, а не `/v4/posting/fbs/list`: у второго фильтр требует `since` и `to`, то есть вынуждает держать скользящее окно по датам. Окно — это либо пропущенный заказ на границе, либо перевыборка всей истории каждый прогон. У `unfulfilled/list` обязательных полей фильтра нет вовсе, а сама выдача уже ограничена отправлениями, не переданными в доставку, — ровно то, что нам нужно.

Каждое отправление проверяется по уникальному индексу `(MarketplaceAccountId, PostingNumber)`. Известное — пропускается (его состояние обновит следующий шаг), новое — проходит проверки и создаёт заказ.

### Догон статусов

Заказы, ушедшие из `awaiting_deliver`, **из выдачи `unfulfilled/list` исчезают**, и отличить «отгружено» от «отменено» по факту исчезновения невозможно. Поэтому вторым шагом прогона идёт точечная сверка: для всех `MarketplaceOrder` аккаунта, чей `Status` ещё не финальный (`Delivered` / `Cancelled`), вызывается `POST /v3/posting/fbs/get` по `PostingNumber`.

Запрос поштучный, зато список короткий — это открытые заказы склада, десятки, а не тысячи. Обновляются `Status`, `RawStatus`, `RawSubstatus`, `TrackingNumber`, `StatusSyncedAt`.

Выборка дополнительно ограничена условием **`StatusSyncedAt < run.StartedAt`**. Без него фаза обнаружения только что обновила ровно эти отправления, а фаза догона тут же сходила бы за ними по одному HTTP-вызову на каждый открытый заказ — каждый прогон, впустую.

Отправление, которого площадка уже не знает (404), из ответа **опускается**, а не роняет прогон: ему всё равно проставляется `StatusSyncedAt`, чтобы оно не опрашивалось вечно, и пишется `LogWarning`. Последний известный статус сохраняется.

Отмена на площадке заказ WMS **не трогает**: ни статус, ни задания на сборку, ни фулфилменты. Она поднимает в интерфейсе предупреждение «отменён на маркетплейсе» на строке списка и на странице заказа — дальше решает человек. Автоматический откат сборки означал бы возврат остатков по заказу, который, возможно, уже физически упакован.

### Непривязанные товары и склады

Заказ **не создаётся**, если выполнено хотя бы одно условие:

1. Любой товар отправления не имеет карточки в WMS либо его карточка не привязана к `CatalogItem` → `marketplaceOrderCardNotMapped`.
2. Склад отправления (`delivery_method.warehouse_id`) не найден среди `MarketplaceWarehouse` аккаунта либо его `WarehouseId` не заполнен → `marketplaceOrderWarehouseNotMapped`.

Первое условие — требование пользователя: заказ с неизвестным товаром бесполезен, собрать его нечем. Второе — техническое: `Order.WarehouseId` обязателен, и угадывать склад нельзя.

Пропуск **всегда наблюдаем**. Каждый пропущенный заказ попадает в `MarketplaceSyncRun.SkippedOrders` с номером отправления, причиной и списком `offer_id`, из-за которых он не прошёл, и показывается в модалке синхронизации отдельным блоком. Заказ, пропавший молча, обнаруживается на складе в момент, когда отгружать уже поздно, — этого допускать нельзя.

Пропуск не запоминается: на следующем прогоне отправление снова придёт из `unfulfilled/list`, и если товар за это время привязали — заказ создастся. Отдельной сущности «отложенный заказ» не заводится.

### Создание заказа

```
Order
├── Type              = FBS
├── Status            = Confirmed
├── Number            — из последовательности БД, как у всех заказов
├── WarehouseId       — MarketplaceWarehouse(delivery_method.warehouse_id).WarehouseId
├── PlannedShipmentAt = shipment_date отправления
├── CreatedById       = null — заказ создан интеграцией; кто запустил прогон, видно в MarketplaceSyncRun.TriggeredById
├── MarketplaceItems  — по одному на products[]: FK карточки + Quantity
├── MarketplaceOrder  — заполняется целиком из ExternalPosting
└── Boxes             — одна коробка со всеми позициями
```

**Коробка всегда одна, даже когда `multi_box_qty > 1`.** Заказ создаётся с одной коробкой, а сборщик разносит позиции по коробкам на странице сборки, где механика создания коробок и перемещения компонентов уже есть ([orders-specification.md](orders-specification.md#управление-коробками-во-время-сборки)). `MultiBoxQty` сохраняется как подсказка «упаковок должно получиться столько».

Восстанавливать раскладку из данных площадки нечего — у Ozon «много коробок» бывает двух разных видов, и ни один из них не описывает, что в какую упаковку положено:

- **Разделение заказа на отправления.** Заказ площадки (`order_number`) распадается на несколько `posting_number`, каждое — самостоятельное грузоместо со своей этикеткой и своим статусом. Родословная видна в `parent_posting_number` и `related_postings`. Для WMS это вообще не особый случай: раз единица импорта — отправление, заказ из трёх упаковок приезжает тремя заказами WMS. Это же и главный довод в пользу `posting_number` как единицы.
- **Многокоробочный товар** (`is_multibox`, `multi_box_qty`) — один крупногабаритный товар, физически едущий в нескольких коробках. Отправление при этом остаётся одно. Флаг `is_multibox = true` означает не «здесь много коробок», а «количество коробок ещё не передано площадке методом `/v3/posting/multiboxqty/set`»; этот метод в спецификации Ozon отнесён к схеме rFBS Агрегатор.

Оба механизма отрабатывают **до** `awaiting_deliver`, то есть до того, как отправление попадает в WMS: к моменту импорта заказ уже разделён, а `multi_box_qty` уже проставлен. Писать в площадку по этому поводу не нужно ничего.

Компонент коробки — это `CatalogItem`, к которому привязана карточка, с количеством из отправления. Позиции схлопываются в один компонент суммой количеств **по `CatalogItemId`, а не по карточке**: `MarketplaceCard.CatalogItemId` — связь N:1, и две разные карточки, указывающие на одну позицию каталога, иначе дали бы в одной коробке два компонента с одинаковым `CatalogItemId`. Тип компонента наследуется от привязки: `Bundle` собирается по дереву, `Variation` разрешается сборщиком в конкретный вариант через уже существующий `AssemblyFulfillment.ResolvedCatalogItemId`.

### Повторная синхронизация

Существующий заказ **никогда не пересоздаётся и не перестраивается**. Обновляются только поля `MarketplaceOrder` (статус, трек-номер, `ShipmentDate`, `StatusSyncedAt`) и, если `ShipmentDate` изменился, `Order.PlannedShipmentAt`.

Состав заказа, коробки, задания на сборку и фулфилменты синхронизация не трогает никогда — они могут быть уже частично собраны. Это то же правило, что зафиксировано для FBS в [orders-specification.md](orders-specification.md#fbs--специфика).

### Планировщик и запуск

Заказы синхронизируются **только вручную** — кнопкой «Синхронизировать заказы» на `/operations/orders/fbs`. В фоновый `MarketplaceSyncScanJob` они не входят, и `Scope = All` их не включает.

Причина: фоновый скан рассчитан на десятки минут между прогонами, а заказы нужны либо прямо сейчас (кладовщик пришёл на смену), либо не нужны совсем. Автоматический импорт заказов вдобавок означал бы создание заказов без участия человека при неразобранном каталоге — с горой пропусков, которые никто не увидит. Фоновая синхронизация заказов вынесена в «Отложено».

Запуск подчиняется тому же advisory-локу на аккаунт, что и остальные прогоны: если по аккаунту уже идёт синхронизация карточек, запуск заказов по нему получает `marketplaceSyncAlreadyRunning`. Это **не роняет весь запрос** — см. [`POST /accounts/sync-orders`](#api).

### Получение этикеток

Этикетки тянутся **лениво**, по требованию пользователя, а не при синхронизации. Импорт заказов не должен упираться в скорость печати на стороне площадки, а этикетка нужна в момент упаковки, а не в момент приезда заказа.

Алгоритм `POST /api/orders/labels { orderIds, grouping }`:

0. Отправление без сохранённой этикетки обязано быть в статусе «Ожидает отгрузки» (`AwaitingDeliver`) — площадка печатает только в нём. Хоть один такой заказ — весь запрос отклоняется `422 marketplaceOrderNotAwaitingDeliver` со списком номеров.

   > **Заказ с непустым `LabelFileId` проверку не проходит вовсе.** Он печатается в любом статусе: файл уже лежит в хранилище, площадка не дёргается, а перепечатка порванной или смазанной этикетки нужна как раз после отгрузки. Запрещать её ради симметрии проверки — терять рабочий сценарий.

1. Заказы группируются по `MarketplaceAccountId` — учётные данные у каждого свои.
2. У кого `LabelFileId` уже заполнен, тот берётся из `DataFile`, площадка не дёргается.
3. **Нарезка гибридная.** Отправления с `MultiBoxQty <= 1` запрашиваются пачками по 20 (`LabelBatchSize`, предел `/v2/posting/fbs/package-label`) — там страница строго одна, и соответствие по порядку запроса гарантировано. Отправления с `MultiBoxQty > 1` запрашиваются **по одному**, и весь ответ целиком становится этикеткой этого отправления, сколько бы страниц в нём ни было.

   > **Почему так.** Спецификация Ozon не говорит, печатает ли многокоробочное отправление одну страницу или по странице на коробку — это остаётся [открытым вопросом](#открытые-вопросы) до проверки на живом магазине. Поштучный запрос делает ответ на него **безразличным**: неизвестное число страниц не может сдвинуть пачку и наклеить артикулы одного заказа на коробку другого.
4. Ozon отвечает на пачку по принципу «всё или ничего»: неготовность одного отправления рушит всю пачку. Поэтому неудачная пачка **повторяется поштучно** — иначе один неготовый заказ лишил бы этикеток девятнадцать готовых.
5. У готовой пачки число страниц сверяется с числом отправлений. **Расхождение — не повод угадывать:** пачка выбрасывается и перезапрашивается поштучно, в лог уходит предупреждение с обоими числами. Это же и сигнал, что Ozon поменял поведение. Соответствие страниц отправлениям больше держать не на чем, а перепутанная этикетка хуже двадцати лишних HTTP-вызовов.
6. На каждую страницу наносятся артикулы (ниже), результат сохраняется отдельным `DataFile`, ссылка пишется в `MarketplaceOrder.LabelFileId` и `LabelFetchedAt`.

   > Строка `DataFile` коммитится **до** записи `LabelFileId`. Смерть запроса между ними оставляет осиротевший файл, который сборщик мусора подберёт через `OrphanTtlHours`, — состояние самоисцеляется, транзакция не нужна.
7. Отправление, по которому пришло `IsReady = false`, получает `LabelError` с кодом `marketplaceLabelNotReady`; `LabelFileId` остаётся пустым.

**Если хотя бы одна этикетка из запрошенных не готова — файл не отдаётся вовсе.** Ответ — `409 marketplaceLabelNotReady` со списком номеров отправлений в `args`, интерфейс показывает «Ozon ещё не сформировал этикетки для N заказов, попробуйте через минуту». Частичный PDF здесь опаснее отказа: пачка на 30 заказов, тихо приехавшая с 28 этикетками, приводит к двум неотгруженным коробкам.

Когда готовы все — страницы склеиваются в один PDF и отдаются потоком. Порядок задаёт `grouping`:

- `none` (по умолчанию) — **в порядке заказов в запросе**, чтобы пачка на печати совпадала со списком на экране;
- `article` — заказы с **одинаковым набором артикулов** идут подряд. Ключ группы — отсортированный список `артикул + количество` по всему отправлению, поэтому многопозиционный заказ образует свою группу, а не примазывается к первой попавшейся. Группы упорядочены по ключу, внутри группы сохраняется порядок запроса. Смысл в упаковке: сборщик берёт одну стопку одинакового товара и проходит её насквозь, вместо того чтобы бегать по стеллажам на каждую этикетку.

Артикулы для ключа берутся оттуда же, откуда и надпись на этикетке, — позиции с непривязанной карточкой в ключ не попадают. Склейка как `DataFile` не сохраняется: это одноразовый артефакт печати, и в хранилище он был бы мусором, который потом разгребает GC. Кэшируются только постраничные этикетки.

### Артикулы на этикетке

На страницу наносится текст **вдоль верхнего края**, столбиком, не более трёх строк:

```
ART-001 ×3
ART-042
ART-777 ×2
+2
```

Правила:

- строка — это артикул `CatalogItem`, привязанного к карточке позиции;
- количество дописывается через `×`, если оно больше единицы;
- позиций больше трёх — печатаются первые три, четвёртой строкой идёт `+N` с числом оставшихся;
- порядок строк — тот же, что в отправлении.

Реализация — **PDFsharp** (лицензия MIT): открывает готовый PDF площадки, рисует поверх страницы через `XGraphics`, и он же склеивает итоговый файл. **Поворот не применяется** — Ozon отдаёт этикетку уже развёрнутой в том виде, в каком она печатается, и текст просто следует за страницей. Генераторы вроде QuestPDF здесь не подходят — они рисуют документ с нуля и не умеют накладывать содержимое на чужую страницу.

> **Шрифт вшивается в сборку embedded-ресурсом.** В Linux-контейнере системных шрифтов нет вообще, а артикулы могут быть кириллическими. Без вшитого шрифта этикетки уедут в печать с квадратиками вместо букв, и узнают об этом на складе, а не в CI.

Этикетка — **снимок на момент печати**. Если карточку впоследствии перепривязали к другой позиции каталога, сохранённая этикетка не перегенерируется: она уже наклеена на коробку, и расхождение с текущей привязкой честнее, чем подмена задним числом.

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
| `GET` | `/accounts/order-sync-targets` | `integrations.map` | Аккаунты, доступные для синхронизации заказов, — источник данных для модалки |
| `POST` | `/accounts/sync-orders` | `integrations.map` | Запуск синхронизации заказов по нескольким аккаунтам, тело `{ accountIds }` → `202` |
| `GET` | `/sync-runs` | `integrations.view` | Прогоны по списку идентификаторов, `?ids=` — один опрос на всю модалку |

Этикетки живут в контроллере заказов, а не интеграций, — они запрашиваются из списка заказов и скоупятся по складу вместе с остальными операциями над заказами:

| Метод | Путь | Право | Назначение |
|-------|------|-------|------------|
| `POST` | `api/orders/labels` | `orders.view` / `orders.view_assigned` | Склеенный PDF этикеток по `{ orderIds, grouping }` |

### `POST /accounts/sync-orders`

```
Запрос:  { accountIds: Guid[] }
Ответ:   202 { items: [{ accountId, syncRunId }],
               failedItems: [{ accountId, accountName, error: AppFieldError }] }
```

Семантика частичного успеха — та же, что у [`batch-self-assign`](orders-specification.md#массовый-захват-post-apiordersbatch-self-assign) и `batch-fulfill`: занятый локом или неактивный аккаунт попадает в `failedItems`, остальные стартуют. Валить весь запрос из-за одного аккаунта, по которому в этот момент идёт фоновая синхронизация карточек, нельзя — пользователь отметил пять магазинов и ждёт пять результатов.

`403` возвращается только на уровне всего запроса — при отсутствии права `integrations.map`.

`GET /sync-runs?ids=` отдаёт прогоны без привязки к аккаунту в пути — модалка следит сразу за несколькими. Существующий `GET /accounts/{id}/sync-runs` остаётся: он про историю одного аккаунта.

### `POST api/orders/labels`

```
Запрос:  { orderIds: Guid[], grouping?: "none" | "article" }
Ответ:   200 application/pdf — склеенный файл
         409 marketplaceLabelNotReady — args.postingNumbers[] с номерами неготовых отправлений
         422 marketplaceOrderNotFromMarketplace — в списке есть заказ без MarketplaceOrder
         422 marketplaceOrderNotAwaitingDeliver — args.postingNumbers[] с номерами отправлений
                                                  без этикетки и не в статусе «Ожидает отгрузки»
```

Ответ либо файл целиком, либо отказ: частичной выдачи нет по причинам, разобранным в [«Получение этикеток»](#получение-этикеток).

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
| `marketplaceOrdersNotSupported` | 422 | Провайдер аккаунта не объявил `Orders` |
| `marketplaceAccountHasOrders` | 409 | Удаление аккаунта, по которому импортированы заказы |
| `marketplaceAccountInactive` | — | Аккаунт отключён; только в `failedItems` у `sync-orders` |
| `marketplaceLabelNotReady` | 409 | Площадка ещё не сформировала этикетки; `args.postingNumbers` — по каким, `args.count` — сколько |
| `marketplaceOrderNotFromMarketplace` | 422 | Запрошена этикетка для заказа без `MarketplaceOrder` |
| `marketplaceOrderCardNotMapped` | — | Заказ пропущен: товар без привязки к каталогу. Только в `SkippedOrders`, наружу по HTTP не отдаётся |
| `marketplaceOrderWarehouseNotMapped` | — | Заказ пропущен: склад отправления не привязан к складу WMS. Только в `SkippedOrders` |

Значения `ErrorCode` для этой секции объявлены в маркетплейсном блоке, к родне, и сохраняют номера, полученные при добавлении (109–115, 126). Они персистятся числом в jsonb-колонках `Error`, `LastSyncError` и `SkippedOrders`, поэтому перенумерация молча переинтерпретировала бы уже сохранённые ошибки; порядок объявления при этом свободен (см. [«Enums: pinned values, free ordering»](backend-patterns.md#enums-pinned-values-free-ordering)).

`args.count` у `marketplaceLabelNotReady` дублирует длину `postingNumbers` намеренно: клиентский `interpolateArgs` подставляет скаляр, а массив не склоняется.

### Changelog

Добавляются значения `AppEntityType`: `MarketplaceAccount = 9`, `MarketplaceCard = 10`. Номера проставлены явно и **не перенумеровываются** — enum персистится в `ChangeLogEntry.EntityType` как `int`, и смена номера молча переинтерпретировала бы все существующие записи журнала.

| Действие | `action` | `actionData` |
|----------|----------|--------------|
| Создание аккаунта | `account.created` | `{ marketplace }` |
| Изменение аккаунта | `account.updated` | `{ marketplace }` |
| Ротация ключа | `account.key_rotated` | `{ marketplace }` — без значений ключа |
| Удаление аккаунта | `account.deleted` | `{ marketplace }` |
| Итог синхронизации | `sync.finished` | `{ syncRunId, scope, status, cardsCreated, cardsArchived, autoMapped, ordersCreated, ordersUpdated, ordersSkipped }` |
| Ручная привязка карточки | `mapping.set` | `{ catalogItemId, source: "manual" }` |
| Снятие привязки | `mapping.cleared` | — |
| Автосопоставление | `mapping.auto` | `{ matched, remaining }` |

**Записи `sync.started` нет.** `AbstractChangeLogService` пишет запись только при непустом диффе `before`/`after`, а старт синхронизации сам по себе состояние аккаунта не меняет — запись либо не создалась бы вовсе, либо пришлось бы подделывать тип `Added`. Факт запуска и так виден в `MarketplaceSyncRun` со статусом `Running`, который создаётся и коммитится сразу; в журнал попадает итог. По той же причине `mapping.auto` пишется через дифф аккаунта (меняется `unmappedCardCount`) — прогон, не сопоставивший ничего, записи не создаёт.

Фоновая синхронизация выполняется без пользователя — `ChangeLogEntry.UserId` остаётся `null`, что схема допускает.

**Заказы, созданные синхронизацией, в changelog не пишутся** — по тому же правилу «синхронизация не пишет в журнал». Прогон на сотню заказов затопил бы журнал сотней записей `Added`, не несущих информации сверх той, что уже есть в `sync.finished` и в самих заказах. Получение этикетки тоже не логируется: это чтение с площадки, а не изменение заказа.

---

## Фронтенд

Раздел встраивается в `SettingsPage` одной записью в `settingsConfig.tsx`:

```
{
  path: "integrations",
  label: "Маркетплейсы",
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

После добавления эндпоинтов на бэкенде — `npm run generate-api` (backend должен быть запущен). Дальше `npm run typecheck` падает **в трёх местах** — все три типа исчерпывающие:

- `src/utils/permissionLabels.ts` — `Record<PermissionName, string>`, нужны три новых права;
- `src/utils/appEntityUtils.tsx` — `Record<AppEntityType, EntityTypeConfig>`, нужны `marketplaceAccount` и `marketplaceCard`;
- `src/utils/errorUtils.ts` — `errorCodeMessages: Record<ErrorCode, string>`, нужны все 11 новых кодов.

Синхронизация заказов добавила **четвёртое** такое место, которого в этом списке не было: `SYNC_SCOPE_LABELS: Record<MarketplaceSyncScope, string>` в `marketplaceUtils.ts` — новое значение `orders` ломает его так же. Плюс семь новых кодов ошибок в `errorCodeMessages` и новый `Record<MarketplaceOrderStatus, …>` в `marketplaceOrderUtils.ts`. Прав при этом не добавилось, `AppEntityType` тоже: `MarketplaceOrder` на бэкенде отображается в `AppEntityType.Order`.

Список `SYNC_SCOPES` на странице аккаунта намеренно **не** получил `orders` — заказы тянутся только со страницы FBS; в коде рядом стоит комментарий, иначе это «починят».

У `marketplaceCard` ссылки нет (`linkTemplate: "no-link"`): собственной страницы у карточки не существует, а маппинга `MarketplaceCard → AppEntity` в `AppMapperProfile` нет вовсе — эти два значения `AppEntityType` используются только как `ChangeLogEntry.EntityType`.

> **Найдено при реализации: nullable enum'ы уезжали в схему числом.** Трансформер enum-схем в `Program.cs` проверял `type.IsEnum`, а у nullable-свойства `context.JsonTypeInfo.Type` — это `Nullable<TEnum>`, для которого `IsEnum` равен `false`. В результате `MarketplaceSyncStatus` и `MarketplaceMappingSource` уезжали в OpenAPI как `integer`, и сгенерированный клиент объявлял их `= number`, хотя рантайм отдавал camelCase-строки. Исправлено разворотом `Nullable.GetUnderlyingType` перед проверкой; правка общая и чинит любой будущий nullable enum.

> **`MarketplaceCapabilities` — `[Flags]`.** `JsonStringEnumConverter` шлёт комбинацию одной строкой (`"warehouses, cards, sellerInfo"`), а сгенерированный тип — union одиночных значений. Сравнивать через `===` нельзя; на фронте есть `hasCapability()`, разбирающий строку. Если понадобится честная типизация — отдавать список строк из DTO.

Ошибки `lastSyncError` и `syncRun.error` приходят как `AppFieldError` (`{ code, detail, args? }`) — тот же тип, что внутри `AppProblemDetails`, он уже сгенерирован. Текст берётся по `code` + `args` через существующий `errorCodeArgMessages` в `src/utils/errorUtils.ts`; поле `detail` англоязычное и в UI не показывается.

Вкладок в приложении до этого не было ни одной (`<Tabs>` использовался только в мобильной навигации `SidebarLayout` и в пикере `StorageNodePickerContent`) — страница аккаунта вводит этот паттерн: один маршрут `:id`, активная вкладка в `?tab=` через `useSyncedWithQueryState`, неактивные вкладки не смонтированы. Зафиксирован в [frontend.md](frontend.md).

**Бейдж-счётчик в сайдбаре отложен.** `SectionConfig` не имеет соответствующего поля, а `settingsConfig.tsx` — обычный модульный массив и хуки вызывать не может. Счётчик пришлось бы передавать **компонентом** (`badge?: React.ComponentType`), который сам дёргает `/accounts/unmapped-count`, и протаскивать его через `toNavItems` в `SidebarNavLeafItem` и в оба места отрисовки внутри `SidebarLayout` — то есть править общий layout ради одного раздела. Решено не трогать: эндпоинт `/accounts/unmapped-count` реализован, но фронтом пока не используется, а число несопоставленных видно в списке аккаунтов и на вкладке «Обзор».

Счётчики активного запуска пока обновляются **опросом**: пока `lastSyncStatus == Running`, запросы аккаунта и `/sync-runs` идут с `refetchInterval` в 3 с и сами останавливаются по завершении. Realtime-клиента в проекте нет вообще ([realtime-specification.md](realtime-specification.md), раздел «Реализовано» — пусто), поэтому события `marketplace.sync.progress` и `marketplace.sync.finished` подключаются позже подменой этого одного места.

### Заказы FBS

Раздел заказов маркетплейс о себе почти ничего не знает — вся площадочная механика собрана в двух местах на `/operations/orders/fbs`.

```
src/components/orders/
├── OrdersListPage.tsx                     — существующий, получает три слота для FBS
└── marketplace/
    ├── SyncOrdersButton.tsx               — кнопка и состояние модалки
    ├── SyncOrdersDialog.tsx               — выбор аккаунтов, запуск, прогресс, итоги
    ├── SyncOrdersAccountAccordion.tsx     — один аккаунт: статус прогона, счётчики, пропуски
    ├── SkippedOrdersList.tsx              — что не приехало и почему
    ├── DownloadLabelsButton.tsx           — действие над выделенными строками
    ├── MarketplaceOrderStatusChip.tsx     — статус на площадке, в списке и на странице заказа
    └── marketplaceOrderUtils.ts           — метки и цвета статусов

src/utils/
├── downloadUtils.ts                       — saveBlob
└── blobErrorUtils.ts                      — parseProblemFromBlob
```

**Слоты, а не ветка по типу.** `OrdersListPage` обслуживает все три типа заказов, поэтому FBS-специфика приходит пропсами — `headerActions`, `bulkActions(selectedIds)` и `extraColumns` — и общий компонент не тянет ни импортов интеграций, ни права `integrations.map` в Direct и FBO. `marketplaceOrderUtils.ts` живёт в дереве операций, а не в настройках, по той же причине.

Две правки в общем компоненте, которые эти слоты потребовали:

- `colSpan` у пустой строки и лоадера был захардкожен восьмёркой — стал вычисляемым;
- `showBulkBar` опирался только на self-assign, а этикетки нужны при любом выделении. Условие стало `selectedIds.size > 0 && (showSelfAssign || bulkActions != null)`; без слагаемого про `bulkActions` панель начала бы появляться пустой на Direct и FBO.

**Модалка синхронизации.** Открывается кнопкой «Синхронизировать заказы» (право `integrations.map`). Содержимое:

1. Список аккаунтов из `/accounts/order-sync-targets` с чекбоксами, плюс кнопки-ярлыки **«Все Ozon»** и **«Все WB»** — они отмечают все аккаунты соответствующей площадки. Кнопка площадки, у которой нет ни одного подходящего аккаунта, не рисуется.
2. По нажатию «Синхронизировать» уходит один `POST /accounts/sync-orders`, модалка переключается в режим прогресса и не закрывается.
3. Прогресс — **аккордеон на аккаунт**. Заголовок несёт статус прогона и сводку («создано 12, обновлено 3, пропущено 2»), раскрытое тело — полные счётчики, ошибку прогона и список пропущенных заказов с причинами. Аккаунты из `failedItems` показываются сразу свёрнутыми с ошибкой вместо счётчиков.
4. Опрос — один `GET /sync-runs?ids=` с `refetchInterval` 2 с, пока хоть один прогон в статусе `Running`. Тот же временный механизм, что на вкладке «Обзор», и та же точка подмены при появлении SSE.
5. По завершении всех прогонов модалка остаётся открытой (итоги и пропуски нужно прочитать) и инвалидирует список заказов.

**Скачивание этикеток.** Выделение строк в `OrdersListPage` уже есть — его ввёл массовый self-assign. Кнопка «Скачать этикетки» шлёт `POST api/orders/labels` и сохраняет ответ как файл. На `409 marketplaceLabelNotReady` файл не скачивается, а показывается сообщение с числом неготовых отправлений и предложением повторить через минуту — код и `args.postingNumbers` разбираются существующим `resolveErrorMessage`.

Ответ здесь — поток PDF, а не JSON, поэтому вызывается напрямую сгенерированная SDK-функция с `parseAs: "blob"` (тот же приём, что в `useFileBlobUrl` — типы генератора для бинарных эндпоинтов ненадёжны). С `parseAs: "blob"` **тело ошибки тоже приезжает Blob'ом**, и `resolveErrorMessage` его не видит, поэтому заведён переиспользуемый `parseProblemFromBlob`.

Сохранение — `saveBlob`: `createObjectURL` плюс якорь с `download`. Это единственный механизм сохранения в приложении, и на нативной сборке он же правильный: WebView не рисует PDF в iframe, поэтому файл отдаётся системному приложению ([native-client.md](native-client.md)). Ветка `Capacitor.isNativePlatform()` здесь не нужна, и предпросмотр склеенного PDF внутри приложения не делается.

---

## Базовый флоу пользователя

### Шаг 1 — Подключение магазина

1. Администратор открывает «Настройки» → «Маркетплейсы» → «Подключить магазин».
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
- В сайдбаре у раздела «Маркетплейсы» отображается бейдж с количеством несопоставленных карточек по всем активным аккаунтам.
- Ручной запуск синхронизации доступен кнопкой в любой момент; повторный запуск при активном — `409`.
- Аккаунт можно деактивировать (`IsActive = false`) — фоновая синхронизация прекращается, все данные и привязки сохраняются. Удаление аккаунта каскадно удаляет склады и карточки и блокируется, если по нему импортированы заказы.

### Шаг 6 — Заказы FBS

1. Кладовщик открывает «Операции» → «Заказы» → «FBS» и жмёт «Синхронизировать заказы».
2. В модалке отмечает магазины — поштучно или кнопкой «Все OZON».
3. Модалка показывает по аккордеону на магазин: сколько заказов создано, обновлено, пропущено. Пропущенные раскрываются списком с причиной — «товар `ART-77` не привязан к каталогу».
4. Созданные заказы появляются в таблице сразу в статусе «Подтверждён» и дальше идут обычным путём: задания на сборку, сборка, отгрузка.
5. Перед упаковкой кладовщик отмечает нужные строки и жмёт «Скачать этикетки» — приходит один PDF, по странице на заказ, с артикулами WMS вдоль левого края. Если площадка ещё не всё сформировала, интерфейс просит подождать минуту.
6. Статус на площадке виден чипом в отдельной колонке и обновляется на следующей синхронизации. Заказ, отменённый на маркетплейсе, подсвечивается — решение по нему принимает человек.

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
    "PageDelayMs": 200,
    "LabelBatchSize": 20
  },
  "Labels": {
    "MaxArticlesOnLabel": 3,
    "FontResourceName": "ProjectWarehouse.Server.Resources.Fonts.LabelFont.ttf",
    "FontSize": 8,
    "Margin": 6
  }
}
```

`LabelBatchSize` вынесен в конфигурацию, но потолок в 20 задан самим Ozon — значение выше вернётся ошибкой площадки, а не ошибкой валидации.

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
| 10. Фронтенд: раздел настроек, вкладки, компоненты привязки | ✓ (бейдж в сайдбаре отложен) |
| 11. Документация | ✓ |

**Синхронизация заказов FBS — реализована.**

| Шаг | Содержание | Статус |
|-----|------------|--------|
| 1 | Whitelist `+ /v4/posting/fbs/unfulfilled/list`, `/v3/posting/fbs/get`, `/v2/posting/fbs/package-label`; нормализация бинарного ответа и санитайзер невалидного `type`; перегенерация `OzonApiClient` (4 → 7 операций) | ✓ |
| 2 | `Abstractions`: `ExternalPosting`, `ExternalPostingItem`, `ExternalPostingStatus`, `ExternalLabelDocument`, `MarketplaceOrderStatus`, флаг `Labels` | ✓ |
| 3 | `OzonClient` + `OzonMarketplaceProvider`: постраничная выборка отправлений, поштучная сверка статусов, определение формата этикетки по байтам, распознавание «ещё не готово» на обоих путях | ✓ |
| 4 | Домен: `MarketplaceOrder`, счётчики и `SkippedOrders` в `MarketplaceSyncRun`, `Orders` в `MarketplaceSyncScope`. Миграция `AddMarketplaceOrders` | ✓ |
| 5 | `MarketplaceOrderSyncService`, подключённый к `MarketplaceSyncService` по `Scope = Orders` | ✓ |
| 6 | Этикетки: PDFsharp, вшитый шрифт, `LabelPdfComposer`, `IDataFileFactory`, `MarketplaceLabelService` | ✓ |
| 7 | API: `order-sync-targets`, `sync-orders`, `sync-runs?ids=`, `api/orders/labels`, предпроверка `DELETE`. Семь новых кодов ошибок | ✓ |
| 8 | Фронтенд: слоты в `OrdersListPage`, модалка с аккордеонами, скачивание этикеток, чип статуса площадки | ✓ |
| 9 | Документация | ✓ |

**Нужно знать о конвейере генерации.** Новые пути притащили две проблемы, обе решены в `generate-client.cs`:

- NJsonSchema типизирует схему по **последнему сегменту** имени после точки, поэтому `posting.v4.…SortDir.Enum` и `posting.v3.FbsPosting.Container.CargoType.Enum` обе становились `Enum`, а NSwag разводил их суффиксами `Enum2` / `Enum3` — по порядку объявления, то есть имена типов поехали бы от любого обновления спеки. Вдобавок сгенерированный `enum Enum` перекрывал `System.Enum` внутри своего неймспейса и ронял сборку. Теперь точечные имена схем **сплющиваются** в плоский PascalCase (`PostingFbsUnfulfilledListRequestSortDirEnum`), а ссылки переписываются.
- `v3PostingProductDetail.jw_uin` объявлен как `"type": "array of strings"` — такого типа в JSON Schema нет. Санитайзер вычищает любой недопустимый `type`, печатая каждую правку в консоль.

**Шрифт этикеток** — Roboto Mono (Apache 2.0), `ProjectWarehouse.Server/Resources/Fonts/LabelFont.ttf`, вшит `EmbeddedResource` с явным `LogicalName`. Моноширинный удобен для артикулов; кириллица и знак `×` в наборе проверены. Отсутствие ресурса не роняет приложение на старте — пишется `LogError`, а падает только генерация этикеток.

### Проверено вручную

Через Scalar и прямые HTTP-вызовы, с заведомо неверным ключом: `test-connection` → `502 marketplaceApiError` с `args.marketplaceStatus`; `POST /sync` → мгновенный `202` + `syncRunId`; фоновый воркер берёт advisory-лок, ходит в реальный Ozon, кладёт структурированный `AppFieldError` в jsonb и зеркалит его в `LastSyncError`; `sync-runs`, `warehouses`, `cards`, `auto-map`, `unmapped-count` отвечают; changelog получает `account.created` и `sync.finished`; `DELETE` каскадно чистит аккаунт.

Синхронизация с боевым ключом Ozon (реальные склады и карточки, автосопоставление, архивация по `SyncedAt`) **не проверялась** — нужен настоящий аккаунт продавца.

По заказам FBS проверено без обращения к площадке: трансляция `Order.SearchString` через зависимую навигацию (`LEFT JOIN` + `CASE WHEN … IS NOT NULL`, всё под `COALESCE`, так что `ILIKE` не получает `NULL`); миграция применяется на живой БД; jsonb-коллекция `SkippedOrders` легла скалярным свойством, а не владеемой сущностью; конвейер PDF — нарезка, наложение текста, склейка и встраивание кириллического сабсета шрифта — прогнан на синтетическом трёхстраничном документе; приложение стартует, OpenAPI отдаёт все четыре новых эндпоинта, TypeScript и линтер чистые.

**Требует проверки на живом магазине** (по порядку, каждый пункт что-то закрывает):

1. Прогон `scope = orders` без привязок — курсорная пагинация, скоуп учётных данных на каждом шаге энумератора, наблюдаемость пропусков.
2. Привязать склад и карточки одного отправления, повторить — **сопоставление по `Sku` против `OfferId`**, номер заказа от БД, одна коробка, группировка компонентов.
3. Повтор без изменений — идемпотентность и то, что условие `StatusSyncedAt` действительно снимает лишние вызовы `/v3/posting/fbs/get`.
4. `POST /api/orders/labels` на один заказ, **с печатью** — сырой PDF против base64-конверта, положение текста и отступ, кириллица вместо квадратиков, кеш в `LabelFileId`.
5. Этикетка отправления, только что появившегося или уже ушедшего из `awaiting_deliver`, — **проводная форма «ещё не готово»**; после этого лишние ветви распознавания можно убрать.
6. Отправление с `multi_box_qty > 1` — одна страница или страница на коробку (см. открытые вопросы).
7. 25 однокоробочных отправлений одним запросом, одно заведомо неготовое, — чанки по 20, сверка числа страниц, поштучный повтор, отказ целиком с верным `postingNumbers`.
8. Отгрузка и отмена на площадке — словарь статусов: собрать `LogWarning` о незнакомых значениях и расширить карту по факту.

### Отложено

- **Фоновая синхронизация заказов** — заказы тянутся только вручную из модалки. Автоматический импорт по расписанию потребует политики для пропущенных заказов, которую сейчас отрабатывает человек, глядя в итоги прогона.
- **Отправление заказа на площадку** — `/v4/posting/fbs/ship`, сборка и разбиение отправления силами WMS. Это перевернуло бы флоу: заказы приходили бы в `awaiting_packaging`, а этикетка становилась бы доступной только после того, как WMS сообщит площадке состав упаковок.
- **Синхронизация заказов FBO** — `/v3/posting/fbo/list`. Схема готова: `MarketplaceOrder` рассчитана на оба типа.
- **Реакция на отмену заказа площадкой** — сейчас только предупреждение в интерфейсе. Автоматический откат сборки с возвратом остатков требует политики для уже упакованных заказов.
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
| Хранение ошибок запуска | **`AppFieldError` в jsonb**, а не строка | Фронт локализует по `code` + `args`; номера `ErrorCode` пиннятся и не перенумеровываются |
| Клиент Ozon | **Генерация NSwag**, библиотекой в процессе, без `.nswag`-конфига | Следующие методы бесплатны; ценой — санитайзер для Swagger-2.0-наследия в спеке Ozon |
| Живые счётчики синхронизации | **Опрос** `/sync-runs`; SSE отложен | Бэкенд ничего не публикует; переход на realtime — подмена одного хука на фронте |
| Бейдж несопоставленных в сайдбаре | **Отложен** | Требует поля `badge` в общем `SectionConfig` и правок `SidebarLayout` ради одного раздела; `/accounts/unmapped-count` реализован, но не используется |
| Вкладки на странице аккаунта | **`<Tabs>` + `?tab=`**, а не отдельные маршруты | Аккаунт грузится один раз, фильтры вкладок живут в тех же query-параметрах |
| Какие отправления тянем | **Только `awaiting_deliver` и статусы после него** | WMS не собирает отправление на площадке; этикетка доступна сразу |
| Метод выборки заказов | **`/v4/posting/fbs/unfulfilled/list`**, а не `/v4/posting/fbs/list` | У второго `since`/`to` обязательны — окно по датам с риском пропусков на границе |
| Отслеживание статуса после `awaiting_deliver` | **Поштучный `/v3/posting/fbs/get`** по незакрытым заказам | Из `unfulfilled/list` такие отправления исчезают, и «отгружено» неотличимо от «отменено» |
| Связь двух статусных машин | **Полностью независимы** | Заказ создаётся в `Confirmed`; отмена на площадке — предупреждение, а не откат сборки |
| Заказ с непривязанным товаром | **Не создаётся**, причина пишется в `SkippedOrders` | Пропуск обязан быть виден: молча пропавший заказ обнаруживается на отгрузке |
| Единица заказа | **Отправление** (`posting_number`), а не заказ площадки | Этикетка, сборка и отгрузка живут на отправлении |
| `Order.MarketplaceOrderId` | **Удаляется**, заменяется `MarketplaceOrder.PostingNumber` | `SearchString` получает `LEFT JOIN`; зато один источник номера и есть где хранить аккаунт |
| `OrderMarketplaceItem.MarketplaceCardId` | **Настоящий FK** на `MarketplaceCard` | Правило «непривязанное не синхронизируем» гарантирует существование карточки |
| Коробки при `multi_box_qty > 1` | **Одна коробка**, разбиение делает сборщик | Площадка не сообщает, что в какой упаковке — восстановить разбиение нечем |
| Момент получения этикетки | **Лениво, по кнопке** | Импорт не упирается в скорость печати площадки; этикетка нужна при упаковке |
| Неготовая этикетка в пачке | **Отказ целиком**, `409` со списком номеров | Частичный PDF на 28 из 30 заказов даёт две неотгруженные коробки |
| Хранение этикеток | Постранично в **`DataFile`**, склейка не сохраняется | Кэш от повторных вызовов площадки; склейка — одноразовый артефакт печати |
| Библиотека PDF | **PDFsharp** (MIT), шрифт embedded-ресурсом | Умеет рисовать поверх чужой страницы и склеивать; в контейнере системных шрифтов нет |
| Нарезка этикеток при мультибоксе | **Гибрид**: пачки по 20 для однокоробочных, поштучно при `MultiBoxQty > 1` | Делает открытый вопрос «одна страница или страница на коробку» безвредным |
| Ссылка позиции отправления на карточку | **По `Sku`, затем `OfferId`** | В `products[]` нет `product_id`, а `MarketplaceCard.ExternalId` — это он |
| Контракт этикеток у провайдера | **Документ на всю пачку**, не список по отправлению | Иначе провайдеру пришлось бы резать PDF, хотя нарезка живёт в сервисе |
| Схлопывание позиций в компонент | **По `CatalogItemId`**, не по карточке | Две карточки могут смотреть в одну позицию каталога |
| Слоты для FBS в общем списке заказов | **Явные пропсы** `headerActions` / `bulkActions` / `extraColumns` | Общий `OrdersListPage` не тянет импорты интеграций в Direct и FBO |
| Цвета и размеры Ozon | **Модель не меняется**, `model_id` и атрибуты не сохраняются | Каждый вариант — отдельный товар со своим `offer_id`, он и так ложится на одну `MarketplaceCard` |

---

## Открытые вопросы

Требуют проверки на живом магазине Ozon:

- **Страничность этикетки многокоробочного отправления** — одна страница или по странице на коробку. Спецификация Ozon молчит. На корректность это уже не влияет: такие отправления запрашиваются поштучно, и `LabelFileId` спокойно хранит многостраничный PDF.
- **Проводная форма ответа `package-label`** — сырой PDF, JSON-конверт с base64 или пустое тело, и каким кодом приходит «ещё не готово». Обёртка обрабатывает все варианты; после проверки лишние ветви можно убрать.

Требуют проверки при подключении Wildberries:

- **Формат стикера WB.** `ExternalLabelDocument` спроектирован с полем `ContentType` в расчёте на то, что WB отдаёт растровый или SVG-стикер, а не PDF. По спецификации Wildberries это **не проверялось** — если формат окажется другим, меняется только сборка итогового файла, контракт провайдера остаётся.
- **Словарь статусов WB** и то, во что схлопывается `MarketplaceOrderStatus` для их схемы поставок.
