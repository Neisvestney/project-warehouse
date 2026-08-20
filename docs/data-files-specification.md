# Спецификация подсистемы файлов (DataFiles)

## Обзор

Единая подсистема хранения и отображения пользовательских файлов: фотографии товаров, сканы договоров и накладных, акты, произвольные вложения к документам.

Подсистема состоит из:

1. Загрузки файла отдельным запросом с немедленным получением идентификатора (upload-first).
2. Хранилища за абстракцией `IFileStorage` с реализацией «локальный диск».
3. Привязки файлов к сущностям **через настоящие внешние ключи** — 1:1 полем на сущности, 1:N через выделенную связующую сущность.
4. Отдачи оригинала и превью изображений с ресайзом и дисковым кэшем.
5. Сборки мусора (непривязанные и осиротевшие файлы) фоновой задачей Quartz.
6. Фронтенда: три слоя компонентов и универсальная модалка просмотра, работающая как с файлами подсистемы, так и с внешними ссылками (например, изображениями карточек Ozon) — см. `frontend-components.md`.

---

## Ключевые решения

| Вопрос | Решение | Почему |
|--------|---------|--------|
| Где хранить байты | Абстракция `IFileStorage`, реализация `LocalFileStorage` (локальный диск / volume) | S3 подключается заменой реализации без изменения схемы БД и контроллеров |
| Метаданные | Таблица `DataFiles` в PostgreSQL | Байты на диске, метаданные и связи — в БД |
| Связь с сущностями | Настоящие FK: `Guid? XFileId` для 1:1, связующая сущность для 1:N | `ProjectTo` и AutoMapper работают штатно; ссылочная целостность на уровне БД |
| Момент загрузки | Upload-first: `POST /api/files` при выборе файла, привязка при сохранении формы | Превью и валидация до сабмита формы; цена — файлы-сироты, которые убирает GC |
| Уборка сирот | Quartz-задача, кандидаты вычисляются через `NOT EXISTS` по всем FK, найденным в метаданных EF | Список ссылающихся таблиц не нужно поддерживать вручную — забыть зарегистрировать новое место использования невозможно |
| Доступ к файлу | `[Authorize]`, без отдельного permission | См. [Права доступа](#права-доступа) |

### Почему не полиморфная таблица привязок

Рассматривался вариант `EntityFileAttachment(EntityType, EntityId, DataFileId)` по образцу `ChangeLogEntry`. Отклонён: `EntityId` в такой схеме не может быть внешним ключом, поэтому (а) нет ссылочной целостности, (б) коллекция файлов не является EF-навигацией, а значит не маппится в DTO через `ProjectTo` и требует отдельного запроса и отдельного эндпоинта на каждой странице. FK-вариант дороже на одну миграцию за каждое новое место использования, но не ломает ни один существующий паттерн проекта.

---

## Конфигурация

`Models/DataFilesOptions.cs` — по образцу `MarketplacesOptions`:

```csharp
public class DataFilesOptions
{
    public const string SectionName = "DataFiles";

    /// <summary>Корень хранилища на диске. В контейнере — примонтированный volume.</summary>
    public string StorageRoot { get; set; } = "/data/files";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain", "text/csv",
    ];

    /// <summary>Допустимые значения ?width= для превью. Произвольные значения запрещены.</summary>
    public int[] ThumbnailWidths { get; set; } = [64, 128, 256, 512, 1024];

    /// <summary>Сколько файл живёт незакреплённым, прежде чем его заберёт GC.</summary>
    public int OrphanTtlHours { get; set; } = 48;

    public string GcCron { get; set; } = "0 30 3 * * ?";

    /// <summary>Максимум удалений за один запуск GC — ограничивает размер транзакции.</summary>
    public int GcBatchSize { get; set; } = 500;
}
```

Секция `DataFiles` в `appsettings.json`. Биндится дважды — как `IOptions<DataFilesOptions>` и эагерно локальной переменной, потому что `GcCron` нужен на этапе регистрации Quartz и `StorageRoot` — для создания каталога при старте (тот же приём, что с `MarketplacesOptions.KeyRingPath`).

### Развёртывание

`StorageRoot` требует того же обращения, что и `/keys` для data-protection:

1. `Directory.CreateDirectory(dataFilesOptions.StorageRoot)` при старте в `Program.cs`.
2. `mkdir -p` + `chown $APP_UID` в `Dockerfile` — иначе примонтированный volume принадлежит root и приложение в него не пишет.
3. Именованный volume `datafiles_storage` в `docker-compose.yml` и `docker-compose.override.yml`.

> **Бэкап.** Каталог хранилища не самодостаточен: без дампа БД имена файлов на диске не сопоставимы с сущностями. Бэкапить только вместе с базой.

---

## Хранилище

`Infrastructure/Files/IFileStorage.cs`:

```csharp
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task<bool> DeleteAsync(string key, CancellationToken ct);
}
```

`LocalFileStorage` — единственная реализация, регистрируется `AddScoped<IFileStorage, LocalFileStorage>()`.

**Формат ключа:** `{yyyy}/{MM}/{dd}/{guid}{ext}` — например `2026/08/06/3f2b…-c1.pdf`.

- Каталоги по дате ограничивают число файлов в одной директории и позволяют архивировать хранилище по периодам.
- `{guid}` — это `DataFile.Id`, так что ключ восстановим, но он **всё равно хранится в БД** в `StorageKey`: смена схемы именования в будущем не должна ломать существующие записи.
- `{ext}` выводится **из провалидированного `ContentType`**, а не из имени, присланного клиентом. Расширение нужно только для удобства эксплуатации (открыть файл на диске напрямую).

**Обязательное требование к реализации:** `LocalFileStorage` проверяет, что итоговый абсолютный путь лежит внутри `StorageRoot` (`Path.GetFullPath(...).StartsWith(root)`), прежде чем что-либо открыть или удалить. Ключ никогда не приходит от клиента напрямую, но защита от path traversal должна стоять на уровне хранилища, а не вызывающего кода.

---

## Доменная модель

`Domain/DataFile.cs`:

```csharp
public class DataFile : IHasIdentity
{
    public Guid Id { get; set; }

    /// <summary>Путь внутри хранилища. Не совпадает с исходным именем файла.</summary>
    public string StorageKey { get; set; } = null!;

    /// <summary>Имя, с которым файл пришёл от клиента. Санитизировано, отдаётся при скачивании.</summary>
    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }

    /// <summary>Заполняются только для изображений — фронтенд резервирует место до загрузки превью.</summary>
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }

    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    [Projectable]
    public bool IsImage => ContentType.StartsWith("image/");
}
```

Конфигурация в `ApplicationDbContext.OnModelCreating`:

```csharp
builder.Entity<DataFile>(e =>
{
    e.HasKey(x => x.Id);
    e.Property(x => x.StorageKey).HasMaxLength(256);
    e.Property(x => x.OriginalFileName).HasMaxLength(256);
    e.Property(x => x.ContentType).HasMaxLength(128);
    e.HasIndex(x => x.StorageKey).IsUnique();
    e.HasIndex(x => x.CreatedAt);

    e.HasOne(x => x.CreatedBy)
        .WithMany()
        .HasForeignKey(x => x.CreatedById)
        .OnDelete(DeleteBehavior.SetNull);
});
```

`CreatedAt` выставляет вызывающий код (`DateTime.UtcNow`), как в модуле маркетплейсов; UTC-конвертер применяется глобально.

### Привязка 1:1

Поле `Guid? XFileId` + навигация `DataFile? X` на сущности-владельце, FK с `OnDelete(DeleteBehavior.Restrict)`.
Действующие точки — `CatalogItem.MainImageFileId` и `MarketplaceOrder.LabelFileId` (этикетка отправления, снимок
на момент печати). Регистрировать новую точку в сборщике мусора не нужно: `DataFileReferences` выводит предикат
осиротевших файлов из модели EF по односоставным внешним ключам на `DataFile`.

### Привязка 1:N

Выделенная связующая сущность на каждого владельца, реализующая `IDataFileLink` (`Id`, FK на владельца, FK на
`DataFile`, `Order`). Действующая точка — `CatalogItemImage`. FK на владельца — `Cascade`, FK на `DataFile` —
`Restrict`; индекс по паре «владелец + `Order`».

### Правила OnDelete

| Направление | Поведение | Почему |
|-------------|-----------|--------|
| Связующая сущность → владелец (`CatalogItemImage` → `CatalogItem`) | `Cascade` | Дочерняя коллекция — общий паттерн проекта |
| Любая ссылка → `DataFile` | **`Restrict`, всегда** | `Cascade` здесь означал бы «удаление файла удаляет товар». `SetNull` тихо оторвал бы файл от сущности. `Restrict` даёт СУБД право заблокировать удаление ещё используемого файла — второй рубеж под GC |
| `DataFile` → `ApplicationUser` (`CreatedBy`) | `SetNull` | Аудит-ссылка, как везде в проекте |

### Инвариант

> **Ссылка на `DataFile` существует только в виде настоящего внешнего ключа.** Идентификаторы файлов нельзя складывать в `jsonb`, в строковые колонки или в массивы без FK — сборщик мусора видит только внешние ключи и удалит такой файл как осиротевший.

---

## Маппинг DTO

`DataFileDto` не выносит `StorageKey` — это внутренняя деталь хранилища.

Файлы встраиваются прямо в DTO сущности-владельца (`CatalogItemDto.mainImage`, `images`) — это и есть выигрыш
FK-варианта: всё разворачивается в SQL внутри `ProjectTo<TDto>`, дополнительных запросов и отдельного эндпоинта
«дай вложения сущности X» не требуется. Фронтенд получает файлы вместе с сущностью одним запросом.

### Запись со стороны сущности

Контроллеры не пишут эту логику сами — они вызывают `IDataFileBindingService` (`BindSingleAsync` для 1:1,
`BindListAsync` для 1:N). Внутри сервис проверяет существование файла и синхронизирует список через
identity-перегрузку `IListUpdater` (см. [backend-patterns.md](backend-patterns.md#file-attachments-adding-a-new-attachment-point)).

- Существование проверяется **явно**, а не оставляется на внешний ключ: сырое нарушение `23503` вылезет как 500,
  а фронтенд умеет отрисовать только `AppProblemDetails`. Несуществующий идентификатор → `422`
  `ErrorCode.DataFileNotFound`.
- Инлайнить проверку-и-синхронизацию в контроллере нельзя: скопированная логика рано или поздно уедет в эндпоинт
  без проверки.
- Удаление привязки из списка удаляет только связующую строку. Сам `DataFile` остаётся и станет кандидатом на
  уборку — см. ниже.


---

## Сборка мусора

Файл становится мусором в двух случаях:

1. Загружен, но форму не сохранили — на него никогда не появилось ни одной ссылки.
2. Ссылка была, но исчезла — удалили накладную, заменили фото товара, убрали вложение из списка.

Оба случая — это одно и то же состояние: **на строку `DataFiles` не указывает ни один внешний ключ**. Отдельный флаг «привязан» не вводится, и от контроллеров не требуется ничего вызывать при сохранении или удалении сущности — забыть такой вызов невозможно, потому что его нет.

### Как строится запрос

Список ссылающихся колонок берётся **из метаданных модели EF во время выполнения**, а не поддерживается руками:

```csharp
private static IReadOnlyList<(string Table, string Column)> GetReferencingColumns(IModel model) =>
    model.GetEntityTypes()
        .SelectMany(t => t.GetForeignKeys())
        .Where(fk => fk.PrincipalEntityType.ClrType == typeof(DataFile) && fk.Properties.Count == 1)
        .Select(fk =>
        {
            var declaring = fk.DeclaringEntityType;
            var table = StoreObjectIdentifier.Table(declaring.GetTableName()!, declaring.GetSchema());
            return (Table: declaring.GetTableName()!, Column: fk.Properties[0].GetColumnName(table)!);
        })
        .Distinct()
        .ToList();
```

`Distinct()` обязателен: при TPH-наследовании (как у `InventoryItem`) несколько типов сущностей отображаются на одну таблицу и дали бы дубликаты условий.

Из этого собирается один `DELETE ... RETURNING`:

```sql
DELETE FROM "DataFiles"
WHERE "Id" IN (
    SELECT f."Id" FROM "DataFiles" f
    WHERE f."CreatedAt" < @cutoff
      AND NOT EXISTS (SELECT 1 FROM "CatalogItems" x WHERE x."MainImageFileId" = f."Id")
      AND NOT EXISTS (SELECT 1 FROM "CatalogItemImages" x WHERE x."DataFileId" = f."Id")
      -- ...по одному NOT EXISTS на каждый найденный внешний ключ
    LIMIT @batchSize
)
RETURNING "StorageKey";
```

`@cutoff = DateTime.UtcNow - OrphanTtlHours` защищает свежезагруженные файлы, которые пользователь ещё не успел сохранить формой.

> **`OrphanTtlHours` — это максимальное время, которое форма может простоять открытой между загрузкой файла и сохранением.** При 48 часах вкладка, забытая на трое суток, приведёт к тому, что сохранение упадёт нарушением FK. Уменьшать значение ниже суток не стоит.

### Порядок удаления

Сначала строка в БД (с `RETURNING "StorageKey"`), потом байты на диске. Обратный порядок при падении между шагами оставил бы запись, указывающую на несуществующий файл, — это битая ссылка в UI. Выбранный порядок в худшем случае оставляет неиспользуемые байты на диске, что безвредно и вычищается отдельной ревизией (см. «Отложено»).

Идентификаторы колонок в собранном SQL берутся из метаданных EF, а не из пользовательского ввода, но экранировать их двойными кавычками всё равно обязательно — Postgres чувствителен к регистру, а имена таблиц в проекте в PascalCase.

### Задача

`Infrastructure/Files/DataFilesGcJob.cs` — `[DisallowConcurrentExecution]`, зарегистрирована внутри **существующего** вызова `builder.Services.AddQuartz(...)` вторым `AddJob`/`AddTrigger`, с cron из `DataFilesOptions.GcCron`.

Задача берёт **advisory-lock PostgreSQL** через существующий `PostgresAdvisoryLock` на отдельном соединении из `NpgsqlDataSource`: job store у Quartz in-memory, поэтому `[DisallowConcurrentExecution]` действует только в пределах одного процесса, а два экземпляра приложения удаляли бы файлы одновременно. Требование зафиксировано в `docs/backend-patterns.md`.

Итог каждого запуска пишется структурным логом: сколько строк удалено, сколько байтов освобождено, сколько удалений с диска не удалось.

---

## API

Контроллер `Controllers/FilesController.cs`, маршрут `api/files`.

| Метод | Маршрут | Назначение |
|-------|---------|------------|
| `POST` | `/api/files` | Загрузка одного файла (`multipart/form-data`, поле `file`) → `DataFileDto` |
| `GET` | `/api/files/{id}` | Метаданные → `DataFileDto` |
| `GET` | `/api/files/{id}/content` | Оригинал, поток байтов |
| `GET` | `/api/files/{id}/thumbnail?width=256` | Превью изображения (только для `IsImage`) |

Удаления файла по идентификатору **нет**: единственный способ убрать файл — снять с него ссылку, дальше сработает GC. Это исключает состояние «сущность ссылается на удалённый файл».

### Загрузка

```csharp
/// <summary>Загрузить файл. Файл существует независимо от сущностей и будет удалён сборщиком мусора, если на него не появится ссылка.</summary>
[HttpPost]
[Authorize]
[RequestSizeLimit(32 * 1024 * 1024)]
[ProducesResponseType<DataFileDto>(StatusCodes.Status200OK)]
public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
```

Проверки по порядку:

1. `file is null || file.Length == 0` → `422`, `ErrorCode.DataFileEmpty`.
2. `file.Length > MaxFileSizeBytes` → `422`, `ErrorCode.DataFileTooLarge`, `args: {maxBytes}`.
3. Определение реального типа **по сигнатуре первых байтов**, а не по заголовку `Content-Type` от клиента. Несовпадение или тип вне `AllowedContentTypes` → `422`, `ErrorCode.DataFileTypeNotAllowed`, `args: {allowed}`.
4. Санитизация имени: `Path.GetFileName`, обрезка до 256 символов, вырезание управляющих символов.
5. Для изображений — чтение размеров в `ImageWidth`/`ImageHeight`.
6. Запись в хранилище, затем строка в БД. При падении записи в БД сохранённые байты удаляются.

`RequestSizeLimit` ставится с запасом над `MaxFileSizeBytes`: лимит Kestrel обрывает соединение без внятного тела ответа, а прикладная проверка возвращает нормальный `AppProblemDetails`.

### Создание файла на стороне сервера

Не всякий файл приходит из формы: этикетки маркетплейса генерируются самим бэкендом. Шаги 6 из списка выше вынесены в `IDataFileFactory.CreateAsync(stream, contentType, fileName, sizeBytes, createdById, …)` — ключ хранилища, запись байтов, строка в БД и компенсирующее удаление байтов при неудаче вставки. `FilesController.Upload` вызывает ту же фабрику, так что путь один, а не два.

Фабрика **намеренно не перепроверяет** `AllowedContentTypes`: это политика для недоверенного входа, а байты, сгенерированные нами, к нему не относятся. Валидация остаётся обязанностью вызывающего, который работает с загрузкой пользователя. В remarks интерфейса это записано — иначе кто-нибудь «усилит» проверку и сломает кеширование этикеток.

### Отдача

```csharp
Response.Headers["X-Content-Type-Options"] = "nosniff";
return File(stream, contentTypeForResponse, downloadFileName, enableRangeProcessing: true);
```

- `Content-Disposition: inline` — только для узкого списка: `image/jpeg`, `image/png`, `image/webp`, `image/gif`, `application/pdf`. Всё остальное отдаётся с `attachment`.
- **`image/svg+xml` отсутствует и в `AllowedContentTypes`, и в inline-списке.** SVG — это документ, способный нести скрипт; отданный inline с того же origin, он даёт хранимый XSS.
- `enableRangeProcessing: true` нужен для перемотки в PDF-вьюере браузера.
- `ETag` — на основе идентификатора файла: содержимое по конкретному `Id` неизменяемо, замена файла порождает новую строку. Позволяет отдавать `304` без чтения диска.

Эндпоинты — обычные экшены контроллера, поэтому попадают в конвейер раньше catch-all `app.Map("/api/{**path}")` и SPA-фоллбэка. Отдавать хранилище через `UseStaticFiles` нельзя: это обошло бы и авторизацию, и `Content-Disposition`.

### Превью изображений

`GET /api/files/{id}/thumbnail?width=256`:

- `width` вне `ThumbnailWidths` → `422`, `ErrorCode.DataFileWidthNotAllowed`. Произвольные значения запрещены, иначе кэш раздувается запросами вида `?width=1,2,3,…`.
- Файл не изображение → `422`, `ErrorCode.DataFileNotAnImage`.
- Результат кэшируется на диске ключом `{StorageKey}_w{width}` и переиспользуется. Кэш вычищается тем же GC вместе с оригиналом.
- Пропорции сохраняются, увеличение не выполняется: если оригинал уже уже запрошенной ширины, отдаётся он сам.
- Превью всегда отдаётся как `image/webp`, независимо от формата оригинала.

**Библиотека ресайза** — **SixLabors.ImageSharp 3.x**: полностью управляемый код, никаких нативных зависимостей в образе (альтернатива SkiaSharp тянет `SkiaSharp.NativeAssets.Linux` и системные библиотеки). Лицензионная оговорка — см. [README.md](README.md).

---

## Права доступа

Отдельный permission не вводится, все эндпоинты закрыты обычным `[Authorize]`.

Обоснование: право загружать файл неотделимо от права редактировать сущность, к которой файл прикрепляют. Отдельный `files.upload` пришлось бы выдавать каждой роли, которая редактирует хоть что-нибудь, — он не нёс бы информации. Загрузка сама по себе безвредна: незакреплённый файл невидим и удаляется GC. Право **прикрепить** файл — это право отредактировать сущность-владельца, и оно уже проверяется на её эндпоинте.

> **Известное ограничение.** Любой аутентифицированный пользователь может прочитать файл, зная его `Guid`. Guid неугадываем, но это модель «идентификатор = мандат»: она не учитывает складское ограничение видимости, которое реализует `UserQueryFilterService`. Пользователь, видящий только свои склады, при наличии чужого идентификатора файла прочитает вложение чужой накладной.
>
> Путь ужесточения, если потребуется: денормализовать на `DataFile` ссылку на склад-владелец, заполнять её при привязке и фильтровать чтение так же, как фильтруются сущности. В первую версию не входит.

---

## Коды ошибок

Секция `DataFiles` в `ErrorCode` — значения проставлены явно, поэтому новый код получает следующий свободный номер, а не следующую позицию (см. [«Enums: pinned values, free ordering»](backend-patterns.md#enums-pinned-values-free-ordering)):

```csharp
// DataFiles
DataFileNotFound,
DataFileEmpty,
DataFileTooLarge,
DataFileTypeNotAllowed,
DataFileNotAnImage,
DataFileWidthNotAllowed,
DataFileStorageError,
```

Каждому коду нужна русская строка в `src/utils/errorUtils.ts` → `errorCodeMessages`. Тип `ErrorCode` на фронтенде генерируется из OpenAPI, поэтому пропущенный перевод ломает сборку — забыть невозможно. Для `DataFileTooLarge` и `DataFileTypeNotAllowed` добавляются шаблоны в `errorCodeArgMessages`, использующие `args` (`{maxBytes}`, `{allowed}`).

---

## Фронтенд

Фронтенд — см. frontend-components.md.

---

## Отложено

- **S3-совместимое хранилище.** Вторая реализация `IFileStorage`, выбор через конфигурацию. Схема БД и контракты API не меняются.
- **Ревизия диска.** Обход хранилища в поисках байтов без строки в `DataFiles` (последствие падения между удалением строки и удалением файла). Отдельная задача с большим интервалом.
- **Складское ограничение чтения файлов.** См. «Права доступа».
- **Подписанные ссылки** для прямого `<img src>` с кэшем браузера.
- **Антивирусная проверка** загружаемых файлов.
- **Версионирование файла.** Сейчас замена — это новая строка `DataFile` и переставленная ссылка; история замен не хранится (при необходимости её даёт changelog сущности-владельца).
