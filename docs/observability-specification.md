# Спецификация наблюдаемости (OpenTelemetry)

## Обзор

Сквозная телеметрия приложения в формате **OTLP**: логи и распределённые трейсы с бэкенда и с фронтенда, сшитые в один трейс по `traceparent`. Складская операция целиком — HTTP-запрос, работа контроллера, SQL-запросы и вызовы Ozon Seller API — видна как одно дерево спанов, к которому прикреплены все порождённые ею строки логов.

Подсистема состоит из:

1. Инструментации бэкенда — OpenTelemetry SDK для трейсов, Serilog как пайплайн логов с OTLP-транспортом.
2. Инструментации фронтенда — OpenTelemetry Web SDK, трейсы `fetch` и загрузки документа, логи необработанных ошибок.
3. Прокси-эндпоинта `/api/telemetry/*`, через который фронтенд отдаёт телеметрию под существующей JWT-аутентификацией.
4. Коллектора на сервере, который **только принимает и складывает** OTLP-JSON на диск с ротацией.
5. Локального стека разбора: файлы забираются на машину разработчика и там читаются обратно в дашборд.
6. Dev-окружения, где тот же коллектор отдаёт телеметрию сразу в дашборд, минуя файлы.

### Главный принцип

**Сервер хранит, локальная машина обрабатывает.**

На проде нет ни базы телеметрии, ни индексации, ни UI, ни запросов по архиву. Прод-коллектор — это буфер с ротацией: принял OTLP, записал в файл, забыл. Всё, что стоит ресурсов — поиск, агрегация, отрисовка трейсов — выполняется локально над скачанной копией архива.

Из этого следует остальное:

- на сервере не появляется ни ClickHouse, ни Elasticsearch, ни Grafana — прод-контейнер один и почти не ест RAM;
- формат хранения совпадает с форматом передачи (OTLP JSON), поэтому конвертеры не нужны: чем архив записан, тем и читается;
- смена инструмента разбора (Aspire Dashboard → SigNoz → Grafana) не требует ни изменений в приложении, ни миграции архива — меняется только локальный конфиг;
- телеметрия недоступна в реальном времени: между событием и его появлением в дашборде лежит копирование файлов.

---

## Ключевые решения

| Вопрос | Решение | Почему |
|--------|---------|--------|
| Формат телеметрии | OTLP | Единый протокол для логов, трейсов и метрик; поддерживается всем, что может понадобиться дальше |
| Логи на бэкенде | Serilog остаётся пайплайном, добавляется `Serilog.Sinks.OpenTelemetry` | Существующий конфиг, `MinimumLevel.Override` и консольный sink для дева не трогаются; меняется только транспорт |
| Связь логов и трейсов | `trace_id`/`span_id` подставляются sink'ом из `Activity.Current` | Каждая строка лога сама прицепляется к спану, ничего писать руками не нужно |
| Хранилище на проде | `fileexporter` коллектора, OTLP JSON с ротацией, в volume | Никакой БД на сервере; ротация ограничивает диск сверху |
| Сжатие архива | Выключено | `otlpjsonfilereceiver` читает только несжатый JSON; сжатый архив пришлось бы распаковывать перед разбором |
| Разбор | Локальный коллектор с `otlpjsonfilereceiver` → Aspire Dashboard | Симметрия форматов: тот же компонент читает то, что записал `fileexporter` |
| Телеметрия в деве | Тот же коллектор, но экспорт сразу в дашборд вместо файлов | Прокси-эндпоинт и правила отсева отлаживаются локально; в конфигах различается один блок |
| Приём телеметрии с фронта | Прокси-эндпоинт `/api/telemetry/*` в ASP.NET | Порт коллектора не выставляется наружу: аутентификация, CORS и TLS уже решены приложением |
| Зависимость от nginx | Отсутствует | Коллектор публикует порты только внутри docker-сети; наружу торчит то же приложение на `4587`, nginx по-прежнему занят лишь маршрутизацией по домену |
| Метрики | Не собираются | Файловый архив плохо подходит для временных рядов: непрерывный поток точек раздувает диск и не даёт того, ради чего метрики нужны — запроса по окну |

### Почему не Seq, SigNoz или Grafana на сервере

Все три хранят телеметрию в собственном формате и вместе с UI: Seq тянет свой движок, SigNoz — ClickHouse, Grafana — Loki и Tempo. Любой из них съедает на сервере больше памяти, чем само приложение, ради данных, которые смотрят раз в неделю после инцидента. Файловый архив стоит околонуля и остаётся валидным входом для любого из этих инструментов, если однажды понадобится поднять его локально.

### Почему прокси-эндпоинт, а не выставленный наружу коллектор

OTLP-приёмник коллектора не умеет ни JWT, ни ротации токенов, ни `AppProblemDetails`. Выставленный наружу, он становится второй дверью в систему — открытой, анонимной и принимающей произвольные объёмы данных от кого угодно. Проксирование через контроллер отдаёт приёму телеметрии всё, что у приложения уже есть: `[Authorize]`, CORS-политику `CapacitorPolicy`, TLS, единый порт.

---

## Топология

```
                  ┌─ прод (docker-compose.prod.yml) ───────────────────────┐
                  │                                                        │
[бэкенд] ──OTLP/gRPC :4317──┐                                              │
                  │         ├─> otel-collector ──> /telemetry/*.json       │
[фронтенд] ─HTTPS─┴─> /api/telemetry/* ──OTLP/HTTP :4318──┘  (volume)      │
                  │         (прокси в ASP.NET)                             │
                  └────────────────────────────────────────────────────────┘
                                                     │
                                          scripts/fetch-telemetry.ps1
                                                     ▼
                  ┌─ машина разработчика ──────────────────────────────────┐
                  │  otel-replay (otlp_json_file) ──> Aspire Dashboard     │
                  │                                   http://localhost:18890
                  │                                                        │
                  │  dev (docker-compose.yml, профиль telemetry):          │
                  │  dotnet run ─OTLP─> otel-collector ──> Aspire Dashboard│
                  │                     :4317/:4318       http://localhost:18888
                  └────────────────────────────────────────────────────────┘
```

Порты `4317`/`4318` коллектора существуют только внутри сети `projectwarehouse` и на проде не публикуются на хост; в деве — публикуются, см. [Dev-окружение](#dev-окружение).

---

## Серверная часть

### Коллектор

Сервис в `docker-compose.prod.yml`:

```yaml
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel/config.yaml"]
    networks:
      - projectwarehouse
    volumes:
      - ./otel/collector.prod.yaml:/etc/otel/config.yaml:ro
      - telemetry_data:/telemetry
    restart: unless-stopped
```

Образ именно `-contrib`: `fileexporter` и `filterprocessor` в базовую сборку не входят. Портов в `ports:` нет — приложение обращается к нему по имени сервиса внутри сети.

`otel/collector.prod.yaml`:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317   # бэкенд
      http:
        endpoint: 0.0.0.0:4318   # прокси-эндпоинт фронтенда

processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 192
  filter/noise:
    error_mode: ignore
    traces:
      span:
        - 'attributes["url.path"] == "/health"'
        - 'IsMatch(attributes["url.path"], "^/api/telemetry/")'
  batch:
    timeout: 10s
    send_batch_size: 1024

exporters:
  file/traces:
    path: /telemetry/traces.json
    format: json
    rotation: {max_megabytes: 64, max_days: 14, max_backups: 20, localtime: true}
  file/logs:
    path: /telemetry/logs.json
    format: json
    rotation: {max_megabytes: 64, max_days: 14, max_backups: 20, localtime: true}

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, filter/noise, batch]
      exporters: [file/traces]
    logs:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [file/logs]
  telemetry:
    logs:
      level: warn
```

`filter/noise` обязателен, а не косметичен: без него запрос к `/api/telemetry/*` порождает спан, спан уезжает в коллектор следующим запросом, который порождает спан — трейсинг начинает трейсить сам себя. `/health` отсекается по той же причине, что и всегда: опрос раз в несколько секунд, полезной информации ноль.

Верхняя граница диска — `max_megabytes × max_backups` на сигнал, то есть ~2.5 ГБ на оба при значениях выше.

### Пакеты приложения

```
Serilog.Sinks.OpenTelemetry
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Exporter.OpenTelemetryProtocol
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.Quartz
Npgsql.OpenTelemetry
```

Отдельная EF-инструментация не нужна: Npgsql 8+ эмитит собственный `ActivitySource`, а `Npgsql.OpenTelemetry` даёт метод `.AddNpgsql()`, подписывающий на него провайдер трейсов.

### Конфигурация

`Models/ObservabilityOptions.cs` — по образцу `DataFilesOptions`:

```csharp
public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>OTLP-приёмник коллектора. В контейнере — имя сервиса в docker-сети.</summary>
    public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";

    public string ServiceName { get; set; } = "projectwarehouse.server";

    /// <summary>Доля трейсов, попадающих в архив. Ошибки и медленные запросы не сэмплируются.</summary>
    public double TraceSampleRatio { get; set; } = 1.0;

    /// <summary>Потолок тела одного OTLP-запроса от фронтенда, байт.</summary>
    public int MaxClientPayloadBytes { get; set; } = 1024 * 1024;
}
```

Секция `Observability` в `appsettings.json`; про значения для локального запуска — [Dev-окружение](#dev-окружение).

### Регистрация

В `Program.cs`, до `builder.Build()`:

```csharp
builder.Services.Configure<ObservabilityOptions>(
    builder.Configuration.GetSection(ObservabilityOptions.SectionName));
var observability = builder.Configuration.GetSection(ObservabilityOptions.SectionName)
    .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: observability.ServiceName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o =>
        {
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/api/telemetry")
                              && ctx.Request.Path != "/health";
        })
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddQuartzInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(observability.OtlpEndpoint)));
```

Фильтр по пути дублирует `filter/noise` коллектора намеренно: локальный дев-запуск ходит в коллектор напрямую, минуя прод-конфиг, и без фильтра в приложении рекурсия воспроизводится на машине разработчика.

**Текст SQL записывается** — `EnableCommandTextInstrumentation` в настройках трассировки Npgsql включён. Спан без текста запроса отвечает «здесь ушло 800 мс» и не отвечает на единственный интересный вопрос — на что именно; спрашивать это у спана и лезть за ответом в код сводит на нет смысл трейсинга запросов к БД. Цена — объём: сгенерированный EF запрос на десяток таблиц весит больше всего остального спана вместе взятого и расходует бюджет ротации быстрее. Персональных данных в параметрах нет, схема заказов их не хранит (см. [Приватность](#приватность)). Если архив начнёт упираться в `max_backups` раньше, чем в `max_days`, флаг выключается одной строкой конфигурации — без изменений в остальной инструментации.

Serilog получает второй sink в существующем `UseSerilog`:

```csharp
    .WriteTo.OpenTelemetry(o =>
    {
        o.Endpoint = observability.OtlpEndpoint;
        o.Protocol = OtlpProtocol.Grpc;
        o.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = observability.ServiceName,
        };
    })
```

Консольный sink остаётся: он читается глазами при `dotnet run`, OTLP-sink — нет.

### Обогащение

Middleware, добавляющее в `LogContext` и в текущий `Activity` то, чего нет в стандартной инструментации:

| Атрибут | Источник |
|---------|----------|
| `enduser.id` | claim `sub` |
| `enduser.name` | claim `name` |
| `app.time_zone` | `IRequestTimeZoneAccessor` |

Пишутся и в спан (`Activity.Current?.SetTag`), и в `LogContext` — спан нужен для фильтрации трейсов по пользователю, `LogContext` для строк лога, порождённых вне HTTP-спана.

### Сэмплинг

Джоба `MarketplaceSyncScanJob` запускается раз в минуту (`SyncScanCron: "0 * * * * ?"`) и в подавляющем большинстве запусков не делает ничего. Полторы тысячи пустых трейсов в сутки забивают архив и мешают искать глазами, поэтому пустые проходы сканера отбрасываются, а трейсы реальной синхронизации сохраняются целиком.

Пороги и правила живут в `filter/noise` коллектора, а не в приложении: менять их перезапуском одного контейнера дешевле, чем пересборкой образа.

### Приватность

Архив телеметрии — чувствительные данные и хранится с теми же ограничениями, что и volume `datafiles_storage`.

- **Api-Key маркетплейсов.** `HttpClientInstrumentation` не записывает заголовки запросов, и включать их запись нельзя: `OzonAuthHandler` подставляет расшифрованный ключ именно в заголовок.
- **Тела ответов Ozon.** Записываются в атрибуты спанов на уровне `Debug` — это главный инструмент разбора расхождений между площадкой и WMS. Персональных данных получателя Ozon продавцу не раскрывает, а схема `Order` их и не хранит: `MarketplaceOrder` держит номер отправления, аккаунт, статус и этикетку (см. [orders-specification.md](orders-specification.md#заказ-order)).

---

## Прокси-эндпоинт телеметрии

`Controllers/TelemetryController.cs`:

| Метод | Путь | Назначение |
|-------|------|------------|
| `POST` | `/api/telemetry/v1/traces` | Спаны фронтенда |
| `POST` | `/api/telemetry/v1/logs` | Логи фронтенда |

Поведение:

- `[Authorize]` без отдельного permission — телеметрию шлёт любой аутентифицированный пользователь, как и в случае `/api/files`;
- тело принимается как `application/json` (OTLP/HTTP+JSON) и **не разбирается**: контроллер копирует поток в `HttpClient` до коллектора, не десериализуя OTLP;
- размер тела ограничен `MaxClientPayloadBytes` через `[RequestSizeLimit]`; превышение — `413`, а не `AppProblemDetails`, поскольку отправителя это не читает;
- ответ — `202 Accepted` с пустым телом: клиенту незачем ждать записи на диск;
- недоступность коллектора не считается ошибкой приложения — эндпоинт возвращает `202` и пишет `Warning` в лог, иначе сбой телеметрии превращается в сбой для пользователя;
- в changelog не попадает, в OpenAPI-документе присутствует (клиент кодогенерацией не пользуется, но контракт должен быть виден).

`HttpClient` до коллектора регистрируется через `IHttpClientFactory` **без** `AddStandardResilienceHandler`: повторные попытки для телеметрии бессмысленны, батч дешевле потерять, чем удерживать в памяти.

---

## Фронтенд

### Пакеты

```
@opentelemetry/api
@opentelemetry/sdk-trace-web
@opentelemetry/exporter-trace-otlp-http
@opentelemetry/sdk-logs
@opentelemetry/api-logs
@opentelemetry/exporter-logs-otlp-http
@opentelemetry/instrumentation-fetch
@opentelemetry/instrumentation-document-load
@opentelemetry/resources
@opentelemetry/semantic-conventions
```

### Инициализация

`src/services/telemetry.ts`, точка входа `initTelemetry()`. Модуль **грузится динамическим `import()` после первой отрисовки**, а не из `main.tsx`: ~70 КБ gzip в критическом пути ради телеметрии — плохой размен, особенно для мобильного клиента на складе.

Ключевые части:

```ts
const provider = new WebTracerProvider({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: "projectwarehouse.client",
    [ATTR_SERVICE_VERSION]: import.meta.env.VITE_APP_VERSION,
    "session.id": sessionId,
  }),
  spanProcessors: [new BatchSpanProcessor(new OTLPTraceExporter({
    url: `${window.location.origin}/api/telemetry/v1/traces`,
    headers: authHeaders,
  }))],
});
provider.register();

registerInstrumentations({
  instrumentations: [
    new DocumentLoadInstrumentation(),
    new FetchInstrumentation({
      propagateTraceHeaderCorsUrls: [new RegExp(`^${escapeRegExp(window.location.origin)}/api/`)],
      ignoreUrls: [/\/api\/telemetry\//],
      clearTimingResources: true,
    }),
  ],
});
```

`session.id` — `crypto.randomUUID()`, живущий в `sessionStorage`: он склеивает действия одной вкладки, когда трейсов много и непонятно, чьи они.

### Аутентификация экспортёров

`headers` OTLP-экспортёра принимает не только объект, но и асинхронную фабрику `() => Promise<Record<string, string>>`. Токен берётся в момент отправки, а не в момент создания экспортёра:

```ts
const authHeaders = async () => {
  const token = await getFreshAccessToken();
  return token ? {Authorization: `Bearer ${token}`} : {};
};
```

`getFreshAccessToken()` экспортируется из `src/services/apiClient.ts` и переиспользует существующую логику упреждающего обновления токена — ту же, что и request-интерцептор. Собственный экспортёр писать не требуется.

### Пропагация контекста

`propagateTraceHeaderCorsUrls` ограничен собственным API-origin: `traceparent` не должен уезжать на сторонние домены. Инструментация перехватывает `fetch`, на котором построены и сгенерированный `@hey-api` клиент, и SSE-обвязка `serverSentEvents.gen.ts`, — оборачивать их вручную не нужно.

`ignoreUrls` для `/api/telemetry/` закрывает ту же рекурсию, что `filter/noise` на сервере, но на шаг раньше — иначе отправка батча порождает спан, который попадает в следующий батч.

Долгоживущий SSE-стрим порождает спан длиной в сессию. Это шум, а не сигнал: он отбрасывается тем же `ignoreUrls`, статус соединения виден в `RealtimeProvider` (см. [frontend-realtime.md](frontend-realtime.md)).

### Спаны бизнес-операций

Автоматической инструментации кликов нет. Спаны действия заводятся вручную и **только вокруг операций, меняющих состояние склада** — тех, про которые задают вопрос «почему это заняло минуту» или «что пошло не так у кладовщика в среду»:

| Операция | Атрибуты спана |
|----------|----------------|
| Подтверждение заказа | `order.id`, `order.type` |
| Передача заказа на сборку и завершение сборки | `order.id`, `assembly_task.id` |
| Отгрузка | `order.id`, число коробок |
| Проведение приёмки, перемещения, списания | тип документа, `document.id`, число позиций |
| Завершение инвентаризации | `stocktake.id`, число расхождений |
| Ручной запуск синхронизации маркетплейса | `marketplace.account_id` |
| Печать этикеток | число отправлений |

Обёртка — тонкий хелпер над `tracer.startActiveSpan`, вызываемый из обработчика:

```ts
await withOperationSpan("order.confirm", {"order.id": orderId}, async () => {
  await confirmOrder({path: {id: orderId}});
});
```

Внутри колбэка спан активен, поэтому запрос `@hey-api` клиента становится его потомком, а `traceparent` уносит на бэкенд идентификатор именно этого спана. Ошибка внутри колбэка помечает спан статусом `ERROR` и записывается как исключение — хелпер это делает сам, вызывающий код о телеметрии не знает ничего, кроме имени операции.

**Почему не `@opentelemetry/instrumentation-user-interaction`.** Автоматическая инструментация кликов требует `ZoneContextManager` и вместе с ним `zone.js`: между обработчиком события в React и вызовом `fetch` стоит `await`, а стандартный контекст-менеджер теряет активный спан на асинхронной границе. `zone.js` патчит все асинхронные API браузера ради этого одного эффекта, добавляет заметный вес мобильному клиенту и остаётся источником трудноуловимых конфликтов. Взамен он инструментирует **каждый** клик — включая раскрытие аккордеона и переключение вкладки, — то есть платит за шум.

Ручные спаны дают ровно те операции, ради которых трейсинг заведён, ценой одной строки в обработчике. Внутри `withOperationSpan` контекст не теряется: спан активен на протяжении единственного `await`, который хелпер сам и создаёт.

Хелпер живёт в `@opentelemetry/api` и не тянет за собой SDK, поэтому импортируется в компоненты напрямую и работает до того, как модуль телеметрии догрузился: без зарегистрированного провайдера `trace.getTracer` возвращает no-op, колбэк выполняется как обычно. Ленивая загрузка остаётся ленивой, а обработчики не обязаны знать, инициализирована телеметрия или нет.

### Логи

`LoggerProvider` с `BatchLogRecordProcessor` и `OTLPLogExporter` на `/api/telemetry/v1/logs`. Что в него попадает:

- `window.onerror` и `unhandledrejection` — необработанные исключения с трассировкой стека;
- существующие `console.warn`/`console.error` приложения через тонкую обёртку;
- переходы роутера как события — без них ошибка не привязана к экрану, на котором произошла.

Записи, созданные внутри активного спана, наследуют его `trace_id`, поэтому ошибка на фронте и упавший запрос на бэке оказываются в одном дереве.

### Окно до аутентификации

Эндпоинт требует токен, поэтому телеметрия до входа отправлена быть не может. Записи копятся в кольцевом буфере на 100 элементов и выгружаются после успешного логина. Пользователь, который так и не вошёл, своей телеметрии не оставит — для внутренней складской системы приемлемо, публичной страницы, ради которой стоило бы городить анонимный приём, здесь нет.

### Нативный клиент

Capacitor-сборка после выбора сервера переходит на его URL (см. [native-client.md](native-client.md)), поэтому `window.location.origin` указывает на сам сервер и относительные пути работают без изменений. На origin лаунчера (`capacitor://localhost`) телеметрия не инициализируется: пользователь там ещё не выбрал сервер, и слать некуда.

---

## Dev-окружение

В `docker-compose.yml` те же два узла, что и в проде, но с другим выходом: коллектор экспортирует не в файлы, а сразу в Aspire Dashboard, поднятый рядом. Файлового архива в деве нет — свою телеметрию смотрят в момент написания кода, а не через неделю после инцидента.

```yaml
  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:latest
    profiles: ["telemetry"]
    ports:
      - "18888:18888"
    environment:
      - DASHBOARD__OTLP__AUTHMODE=Unsecured
      - DASHBOARD__FRONTEND__AUTHMODE=Unsecured

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    profiles: ["telemetry"]
    command: ["--config=/etc/otel/config.yaml"]
    ports:
      - "4317:4317"
      - "4318:4318"
    volumes:
      - ./otel/collector.dev.yaml:/etc/otel/config.yaml:ro
    depends_on:
      - aspire-dashboard
```

`profiles: ["telemetry"]` держит оба сервиса вне обычного `docker compose up`: чаще всего дев-compose поднимают ради одного постгреса, и навязывать ему два лишних контейнера незачем. Телеметрия включается явно: `docker compose --profile telemetry up -d`.

**Порты публикуются на хост — в отличие от прода.** Бэкенд в деве запускается `dotnet run` на самой машине (см. [README.md](README.md#run-backend)), а не в контейнере, поэтому обратиться к коллектору по имени сервиса внутри docker-сети не может: только `localhost:4317`. Обратная сторона — коллектор в деве принимает OTLP от кого угодно с этой машины, что для локального запуска нормально и в прод-конфиг не переносится.

`otel/collector.dev.yaml` отличается от прод-конфига блоком экспортёров и ничем больше:

```yaml
exporters:
  otlp:
    endpoint: aspire-dashboard:18889
    tls: {insecure: true}

processors:
  batch:
    timeout: 1s

service:
  pipelines:
    traces: {receivers: [otlp], processors: [filter/noise, batch], exporters: [otlp]}
    logs:   {receivers: [otlp], processors: [batch], exporters: [otlp]}
```

`batch.timeout` снижен с десяти секунд до одной: в деве задержка между запросом и его появлением в дашборде — это время, которое разработчик проводит, глядя в пустой экран и гадая, доехало ли вообще. `memory_limiter` убран — ограничивать поток одного разработчика нечем и незачем.

### Почему коллектор в деве, а не прямой экспорт в дашборд

Aspire Dashboard сам принимает OTLP, и приложение могло бы слать телеметрию прямо в него, сэкономив контейнер. Не делается по двум причинам.

**Прокси-эндпоинт.** `TelemetryController` пересылает тело в OTLP/HTTP-приёмник по адресу из конфигурации. Если в деве этим приёмником становится дашборд, а в проде коллектор, контроллер начинает зависеть от окружения — ровно та ветка, которая однажды работает локально и не работает на проде.

**Правила отсева.** `filter/noise` — единственное место, где живёт список того, что не попадает в архив. Проверять его надо там же, где пишется код, а не обнаруживать после выкатки, что рекурсия трейсинга отфильтрована только в одном из двух окружений.

### Настройки приложения

`appsettings.Development.json`:

```json
"Observability": {
  "OtlpEndpoint": "http://localhost:4317"
}
```

Пустая строка выключает экспорт целиком — это состояние по умолчанию для того, кто compose не поднимал: приложение стартует и пишет в консоль ровно как раньше. Без явного выключения `dotnet run` без docker упирается в таймаут экспортёра на каждом батче и засоряет вывод.

Уровни `Serilog` в деве уже понижены до `Debug`, и OTLP-sink наследует их: в дашборд едет заметно больше строк, чем в прод-архив.

### Фронтенд в деве

Vite проксирует `/api/*` на `https://localhost:7095` (см. [README.md](README.md#run-frontend)), поэтому относительный адрес экспортёров работает без изменений — телеметрия с `localhost:5173` доходит до бэкенда, оттуда до коллектора, оттуда в дашборд.

Ленивая загрузка модуля телеметрии сохраняется и в деве: инициализация из `main.tsx` дала бы поведение, отличное от прода, именно в том месте, где отличий быть не должно, — в порядке старта приложения.

---

## Разбор прод-архива

`docker-compose.telemetry.yml` в корне репозитория — стек, поднимаемый только на машине разработчика:

```yaml
services:
  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:latest
    ports:
      - "18890:18888"
    environment:
      - DASHBOARD__OTLP__AUTHMODE=Unsecured
      - DASHBOARD__FRONTEND__AUTHMODE=Unsecured

  otel-replay:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel/config.yaml"]
    volumes:
      - ./otel/collector.local.yaml:/etc/otel/config.yaml:ro
      - ./telemetry-archive:/telemetry:ro
    depends_on:
      - aspire-dashboard
```

`otel/collector.local.yaml`:

```yaml
receivers:
  otlp_json_file:
    include: ["/telemetry/*.json"]
    start_at: beginning
    on_truncate: read_whole_file

exporters:
  otlp:
    endpoint: aspire-dashboard:18889
    tls: {insecure: true}

service:
  pipelines:
    traces: {receivers: [otlp_json_file], processors: [batch], exporters: [otlp]}
    logs:   {receivers: [otlp_json_file], processors: [batch], exporters: [otlp]}
```

Дашборд открывается на `http://localhost:18890` — не на `18888`, который занят живым дев-стеком. Порты разведены намеренно: рядом открытые вкладки с текущей телеметрией дева и со скачанным прод-инцидентом — обычный режим разбора, а не редкость.

Дашборд держит телеметрию **в памяти**: перезапуск контейнера очищает всё, но архив на диске цел, и повторный прогон восстанавливает картину.

`scripts/fetch-telemetry.ps1` — по образцу `scripts/backup-db.ps1`: забирает `/telemetry/*.json` с прод-сервера в `./telemetry-archive`, докачивая изменившееся. Параметр диапазона дат ограничивает выборку файлов ротации, чтобы не тянуть весь архив ради вчерашнего инцидента.

---

## Порядок внедрения

| Этап | Содержание | Признак готовности |
|------|-----------|--------------------|
| 1 | Dev-стек в `docker-compose.yml`, OTel в `Program.cs`, Serilog-sink | Трейс HTTP-запроса с вложенным SQL виден на `localhost:18888` |
| 2 | Коллектор в `docker-compose.prod.yml`, `fileexporter` | В `/telemetry/*.json` растут файлы |
| 3 | Стек разбора и `scripts/fetch-telemetry.ps1` | Скачанный прод-архив открывается на `localhost:18890` |
| 4 | `TelemetryController` | `curl` с валидным токеном доходит до дашборда |
| 5 | Фронтенд: трейсы и `fetch` | Запрос с фронта и его обработка на бэкенде в одном трейсе |
| 6 | `withOperationSpan` и обёртки бизнес-операций | Подтверждение заказа — один трейс от обработчика до SQL |
| 7 | Фронтенд: логи и обработчики ошибок | Необработанное исключение с привязкой к экрану |

Порядок именно такой: дев-стек первым, потому что он же служит стендом для проверки всех последующих этапов — конфиг коллектора, прокси-эндпоинт и фронтовые экспортёры отлаживаются локально и уезжают на прод уже рабочими. Этапы 1–3 самодостаточны: если дальше не пойти, бэкенд всё равно остаётся наблюдаемым.

---

## Ограничения

- **Задержка.** Телеметрия появляется в дашборде только после выгрузки файлов. Живой отладки на проде нет.
- **Нет запросов по архиву на сервере.** Чтобы что-то найти, нужно скачать файлы. При выросшем объёме выборка ограничивается диапазоном ротации, а не фильтром по содержимому.
- **Метрики не собираются.** Загрузка CPU, размер пула соединений, длина очереди синхронизации остаются невидимыми.
- **Дашборд без ретенции.** Aspire Dashboard хранит данные в памяти процесса; долговременное хранилище — сам файловый архив.
- **Один узел.** Как и Quartz с in-memory job store, схема предполагает один инстанс приложения.
- **Действия пользователя видны выборочно.** Трейс начинается либо с HTTP-запроса, либо с явно обёрнутой бизнес-операции. Клик, не приводящий к запросу, и операция, которую забыли обернуть, в телеметрии не существуют — новое действие требует строчки в обработчике, и забыть её нечему помешать.

Развитие — в [backlog.md](backlog.md).
