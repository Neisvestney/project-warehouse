# Спецификация подсистемы файлов (DataFiles)

## Обзор

Единая подсистема хранения и отображения пользовательских файлов: фотографии товаров, сканы договоров и накладных, акты, произвольные вложения к документам.

На момент написания в проекте **нет ничего**, что работает с файлами: ни на бэкенде (`IFormFile`, `FileResult`, хранилище — отсутствуют), ни на фронтенде (`<input type="file">`, blob, object URL — отсутствуют). Подсистема проектируется с нуля.

Первая версия покрывает:

1. Загрузку файла отдельным запросом с немедленным получением идентификатора (upload-first).
2. Хранилище за абстракцией `IFileStorage` с реализацией «локальный диск».
3. Привязку файлов к сущностям **через настоящие внешние ключи** — 1:1 полем на сущности, 1:N через выделенную связующую сущность.
4. Отдачу оригинала и превью изображений с ресайзом и дисковым кэшем.
5. Сборку мусора (непривязанные и осиротевшие файлы) фоновой задачей Quartz.
6. Фронтенд: три слоя компонентов (`FileInput` / `FileView` / `FileControl`) и универсальную модалку просмотра, работающую как с файлами подсистемы, так и с внешними ссылками (например, изображениями карточек Ozon).

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

Поле на сущности:

```csharp
// CatalogItem.cs
public Guid? PhotoFileId { get; set; }
public DataFile? PhotoFile { get; set; }
```

```csharp
builder.Entity<CatalogItem>(e =>
{
    e.HasOne(x => x.PhotoFile)
        .WithMany()
        .HasForeignKey(x => x.PhotoFileId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

### Привязка 1:N

Выделенная связующая сущность на каждого владельца:

```csharp
public class ReceiptDocument : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }
    public Receipt Receipt { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
```

```csharp
builder.Entity<ReceiptDocument>(e =>
{
    e.HasKey(x => x.Id);

    e.HasOne(x => x.Receipt)
        .WithMany(x => x.Documents)
        .HasForeignKey(x => x.ReceiptId)
        .OnDelete(DeleteBehavior.Cascade);

    e.HasOne(x => x.DataFile)
        .WithMany()
        .HasForeignKey(x => x.DataFileId)
        .OnDelete(DeleteBehavior.Restrict);

    e.HasIndex(x => new {x.ReceiptId, x.Order});
});
```

### Правила OnDelete

| Направление | Поведение | Почему |
|-------------|-----------|--------|
| Связующая сущность → владелец (`ReceiptDocument` → `Receipt`) | `Cascade` | Дочерняя коллекция — общий паттерн проекта |
| Любая ссылка → `DataFile` | **`Restrict`, всегда** | `Cascade` здесь означал бы «удаление файла удаляет накладную». `SetNull` тихо оторвал бы документ от сущности. `Restrict` даёт СУБД право заблокировать удаление ещё используемого файла — второй рубеж под GC |
| `DataFile` → `ApplicationUser` (`CreatedBy`) | `SetNull` | Аудит-ссылка, как везде в проекте |

### Инвариант

> **Ссылка на `DataFile` существует только в виде настоящего внешнего ключа.** Идентификаторы файлов нельзя складывать в `jsonb`, в строковые колонки или в массивы без FK — сборщик мусора видит только внешние ключи и удалит такой файл как осиротевший.

---

## Маппинг DTO

`Models/Files/DataFileDto.cs`:

```csharp
public class DataFileDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long SizeBytes { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public bool IsImage { get; init; }
    public Guid? CreatedById { get; init; }
    public string? CreatedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

`StorageKey` в DTO **не выносится** — это внутренняя деталь хранилища.

`AppMapperProfile`:

```csharp
CreateMap<DataFile, DataFileDto>()
    .ForMember(d => d.CreatedByUserName, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.UserName : null));

CreateMap<ReceiptDocument, ReceiptDocumentDto>()
    .ForMember(d => d.File, opt => opt.MapFrom(s => s.DataFile));
```

Файлы встраиваются прямо в DTO сущности — это и есть выигрыш FK-варианта:

```csharp
CreateMap<CatalogItem, CatalogItemDto>()
    .ForMember(d => d.Photo, opt => opt.MapFrom(s => s.PhotoFile));

CreateMap<Receipt, ReceiptDto>()
    .ForMember(d => d.Documents, opt => opt.MapFrom(s => s.Documents.OrderBy(x => x.Order)));
```

Всё это разворачивается в SQL внутри `ProjectTo<TDto>` — дополнительных запросов и отдельного эндпоинта «дай вложения сущности X» не требуется. Фронтенд получает файлы вместе с сущностью одним запросом.

### Запись со стороны сущности

**1:1** — в `UpdateCatalogItemRequest` приходит `Guid? PhotoFileId`. Контроллер присваивает поле; если идентификатор указывает на несуществующий файл, СУБД вернёт нарушение FK — поэтому существование проверяется явно и отдаётся `422` с `ErrorCode.DataFileNotFound`.

**1:N** — список `List<ReceiptDocumentDto>` синхронизируется через `IListUpdater` (identity-based перегрузка, см. `docs/backend-patterns.md`):

```csharp
_listUpdater.UpdateList(
    dto.Documents,
    receipt.Documents,
    db.ReceiptDocuments,
    compare: (doc, docDto) => doc.Id == docDto.Id,
    isNew: docDto => docDto.Id == Guid.Empty,
    afterMap: (docDto, doc) => doc.DataFileId = docDto.File.Id);
```

Удаление вложения из списка удаляет только связующую строку. Сам `DataFile` остаётся и станет кандидатом на уборку — см. ниже.

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
      AND NOT EXISTS (SELECT 1 FROM "CatalogItems" x WHERE x."PhotoFileId" = f."Id")
      AND NOT EXISTS (SELECT 1 FROM "ReceiptDocuments" x WHERE x."DataFileId" = f."Id")
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

`Infrastructure/Files/DataFilesGcJob.cs` — по образцу `MarketplaceSyncScanJob`:

```csharp
[DisallowConcurrentExecution]
public class DataFilesGcJob(
    ApplicationDbContext db,
    IFileStorage storage,
    IOptions<DataFilesOptions> options,
    NpgsqlDataSource dataSource,
    ILogger<DataFilesGcJob> logger) : IJob
{
    public const string Key = "data-files-gc";

    public async Task Execute(IJobExecutionContext context) { /* ... */ }
}
```

Регистрация — внутри **существующего** вызова `builder.Services.AddQuartz(...)`, вторым `AddJob`/`AddTrigger`, с cron из `DataFilesOptions.GcCron`.

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

**Библиотека ресайза.** В проекте её нет. Предлагается **SixLabors.ImageSharp** — полностью управляемый код, никаких нативных зависимостей в контейнере. Оговорка: Six Labors Split License — бесплатна для организаций с выручкой до $1 млн в год, иначе нужна коммерческая лицензия. Альтернатива без этого условия — **SkiaSharp** (MIT), но она тянет `SkiaSharp.NativeAssets.Linux` и требует системных библиотек в образе. Решение за владельцем проекта; на схему БД и контракты API выбор не влияет.

---

## Права доступа

Отдельный permission не вводится, все эндпоинты закрыты обычным `[Authorize]`.

Обоснование: право загружать файл неотделимо от права редактировать сущность, к которой файл прикрепляют. Отдельный `files.upload` пришлось бы выдавать каждой роли, которая редактирует хоть что-нибудь, — он не нёс бы информации. Загрузка сама по себе безвредна: незакреплённый файл невидим и удаляется GC. Право **прикрепить** файл — это право отредактировать сущность-владельца, и оно уже проверяется на её эндпоинте.

> **Известное ограничение.** Любой аутентифицированный пользователь может прочитать файл, зная его `Guid`. Guid неугадываем, но это модель «идентификатор = мандат»: она не учитывает складское ограничение видимости, которое реализует `UserQueryFilterService`. Пользователь, видящий только свои склады, при наличии чужого идентификатора файла прочитает вложение чужой накладной.
>
> Путь ужесточения, если потребуется: денормализовать на `DataFile` ссылку на склад-владелец, заполнять её при привязке и фильтровать чтение так же, как фильтруются сущности. В первую версию не входит.

---

## Коды ошибок

Дописываются в конец `ErrorCode` (значения enum сохраняются в `jsonb` как целые — секции только дополняются):

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

Архитектура — три слоя, по образцу предыдущего проекта. Реестр сервисов загрузки (`fileServices`) не переносится: бэкенд один.

```
src/components/files/
├── inputs/
│   ├── AreaFileInput.tsx        зона перетаскивания
│   └── AddFileInput.tsx         кнопка «добавить файл»
├── FileImage.tsx                примитив: <img>, который сам грузит превью
├── views/
│   ├── RowFileView.tsx          строка списка: иконка, имя, размер, действия
│   ├── ImagePreviewFileView.tsx миниатюра
│   └── ImageCardFileView.tsx    карточка; при пустом значении работает как input
├── controls/
│   ├── SingleFileControl.tsx    одно значение: DataFileDto | null
│   └── FileListControl.tsx      список значений
├── viewer/
│   ├── FileViewerModal.tsx
│   ├── viewableFile.ts          общий тип источника: файл подсистемы или внешняя ссылка
│   ├── useViewableSource.ts     разрешение источника в src + метаданные
│   ├── ImageFileRenderer.tsx
│   ├── PdfFileRenderer.tsx
│   └── UnsupportedFileRenderer.tsx
├── hooks/
│   ├── useFileUpload.ts
│   └── useFileBlobUrl.ts
└── fileUtils.ts                 formatFileSize, iconForContentType, isPdf, ...
```

### Слои

**`FileInput`** — чистые компоненты, про API не знают. Общие пропсы: `onChange(files: File[])`, `loading`, `error`, `accept`, `multiple`, `disabled`. Перетаскивание — на нативных событиях `onDragOver`/`onDrop`: библиотеки дропзоны в проекте нет, а `@dnd-kit` предназначен для сортировки, а не для приёма файлов.

**`FileView`** — чистые компоненты отображения. Общие пропсы: `file: DataFileDto`, `loading`, `error`, `onDelete`, `onReplace`, `onOpen`. Оба слоя пригодны и вне подсистемы — для файлов, ещё не отправленных на сервер.

**`FileControl`** — единственный слой, который ходит в API. Комбинирует Input и View, отдаваемые пропсами:

```tsx
<SingleFileControl value={value} onChange={onChange} View={ImageCardFileView} Input={AddFileInput} />
```

`FileControl` — **управляемый компонент над `DataFileDto`**, а не над `File`. При выборе файла он вызывает `POST /api/files` и отдаёт наружу уже готовый `DataFileDto`; форма хранит идентификатор и отправляет его вместе с сущностью. Это и есть upload-first на практике. Интеграция с `react-hook-form` — через `Controller`, как у прочих полей проекта.

### Хуки

Загрузка использует сгенерированный `formDataBodySerializer` (`src/api/core/bodySerializer.gen.ts`, сейчас в проекте не используется):

```ts
const upload = useMutation({
  ...filesUploadMutation(),
  meta: {suppressGlobalError: true},
});
```

Чтение содержимого:

```ts
export function useFileBlobUrl(fileId: string | undefined, width?: number) {
  const {data: blob, isLoading, error} = useQuery({
    ...filesGetContentOptions({path: {id: fileId!}, query: {width}, parseAs: "blob"}),
    enabled: !!fileId,
    staleTime: Infinity,
  });

  const [url, setUrl] = useState<string>();
  useEffect(() => {
    if (!blob) return;
    const objectUrl = URL.createObjectURL(blob);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [blob]);

  return {url, isLoading, error};
}
```

Почему blob, а не прямой `<img src="/api/files/…">`: токен подставляет перехватчик запросов в `apiClient.ts`, а `<img>` мимо него не проходит — атрибут `src` не несёт заголовка `Authorization`. Отсюда следствие: у картинок нет HTTP-кэша браузера, его заменяет кэш React Query (`staleTime: Infinity`, ключ = идентификатор + ширина). Object URL создаётся на каждого потребителя и освобождается при размонтировании; `createObjectURL` от одного и того же `Blob` возвращает разные URL, поэтому потребители не мешают друг другу.

`parseAs: "blob"` указывается явно — вывод типа ответа генератором для бинарных эндпоинтов ненадёжен.

> Альтернатива, если кэш браузера станет критичен для длинных списков с миниатюрами: короткоживущий подписанный токен в query-строке, дающий работать обычному `<img src>`. В первую версию не входит.

### `FileImage` — самозагружающаяся картинка

Примитив нижнего уровня: ведёт себя как `<img>`, но вместо `src` принимает файл и сам разбирается с авторизацией, размером превью и жизненным циклом object URL. Нужен везде, где картинка — это просто картинка: превью товаров в списках и таблицах, миниатюры в полосе прокрутки модалки, аватар карточки маркетплейса.

Прообраз — `SelfLoadingPhoto` из предыдущего проекта; переносится идея, а не реализация (там не вызывался `revokeObjectURL`, не было отмены запроса при размонтировании, состояний загрузки и ошибки, и каждое монтирование качало файл заново).

```tsx
interface FileImageProps extends Omit<ImgHTMLAttributes<HTMLImageElement>, "src" | "width"> {
  /** Принимает всё, что понимает модалка просмотра, плюс сокращения: DTO или голый URL. */
  source: ViewableFile | DataFileDto | string | null | undefined;
  /** Ширина превью. `"auto"` — замерить контейнер и запросить ближайший допустимый размер. */
  previewWidth?: number | "auto";
  /** Грузить только при попадании во вьюпорт. По умолчанию включено. */
  lazy?: boolean;
  /** Что показать, если файла нет или он не загрузился. */
  fallback?: ReactNode;
}
```

Поведение:

- **Ленивая загрузка** через `IntersectionObserver` — обязательна для списков каталога, иначе открытие страницы порождает сотню запросов превью. `IntersectionObserver` отсутствует в самых старых WebView целевого диапазона (`chrome >= 49`), поэтому при его отсутствии компонент грузит картинку сразу — деградация в поведение «как без ленивости», а не в пустой блок.
- **`previewWidth: "auto"`** замеряет фактическую ширину контейнера, умножает на `devicePixelRatio` и **округляет вверх до ближайшего значения из `ThumbnailWidths`**. Округление обязательно: эндпоинт превью отвергает произвольную ширину с `DataFileWidthNotAllowed`, и передавать туда сырой результат замера нельзя.
- **Резервирование места** по `imageWidth`/`imageHeight` из DTO (`aspect-ratio`), чтобы список не дёргался по мере подгрузки. Для внешних ссылок пропорции неизвестны — используется заданный размер контейнера.
- **Состояния**: `<Skeleton />` во время загрузки, `fallback` при ошибке или отсутствии источника. Object URL освобождается при размонтировании — это уже обеспечивает `useFileBlobUrl`.
- **Кэш** — общий с остальной подсистемой: React Query по ключу «идентификатор + ширина», поэтому одна и та же картинка в списке и в модалке грузится один раз.
- **Внешние ссылки** идут прямым `src`, без запроса и без ресайза — `previewWidth` для них влияет только на вёрстку.

`FileImage` — основа для `ImagePreviewFileView` и `ImageCardFileView` (они добавляют рамку, действия и открытие модалки), а также замена существующему `CardImage.tsx` в разделе маркетплейсов: тот делает ровно это для внешних URL и после появления `FileImage` становится лишним.

### Универсальная модалка просмотра

`FileViewerModal` открывается через существующий императивный менеджер модалок (`useModal().showModal`) — он для этого и предназначен, но пока использован в одном месте проекта.

#### Источник просмотра

Модалка **не привязана к подсистеме файлов**. На вход она принимает источники двух видов:

```ts
// viewableFile.ts
export type ViewableFile =
  | {kind: "dataFile"; file: DataFileDto}
  | {kind: "external"; url: string; name?: string; contentType?: string};

export const viewable = (file: DataFileDto): ViewableFile => ({kind: "dataFile", file});

export const viewableUrl = (url: string, opts?: {name?: string; contentType?: string}): ViewableFile => ({
  kind: "external",
  url,
  ...opts,
});
```

Внешний вид нужен там, где изображение живёт не у нас: миниатюры карточек Ozon (`MarketplaceCard.primaryImageUrl` — уже отображаются компонентом `CardImage`), любые будущие ссылки на сторонние ресурсы. Смешанные списки допустимы: фото товара из нашего хранилища и картинка карточки маркетплейса могут листаться в одной галерее.

```tsx
interface FileViewerModalProps extends ModalComponentProps<null> {
  files: ViewableFile[];
  initialIndex?: number;
  /** Вызывается только для источников вида `dataFile`; для внешних кнопка удаления скрыта. */
  onDelete?: (file: DataFileDto) => void;
}
```

```ts
// один файл
await showModal(FileViewerModal, {files: [viewable(file)]});
// список с открытием на конкретном элементе
await showModal(FileViewerModal, {files: receipt.documents.map((d) => viewable(d.file)), initialIndex: 2});
// внешние изображения карточек маркетплейса
await showModal(FileViewerModal, {
  files: cards.filter((c) => c.primaryImageUrl).map((c) => viewableUrl(c.primaryImageUrl!, {name: c.name})),
});
```

Один компонент покрывает и одиночный файл, и список — разница только в наличии навигации:

| | Один файл | Список |
|---|---|---|
| Стрелки влево/вправо | скрыты | показаны |
| Счётчик «3 из 7» | скрыт | показан |
| Полоса миниатюр снизу | скрыта | показана при `files.length > 1` |

#### Разрешение источника

Оба вида источника приводятся к одной структуре, и дальше вся модалка работает только с ней — рендереры про `kind` не знают:

```ts
interface ResolvedViewable {
  key: string;
  name: string;
  contentType?: string;
  /** Готовый src для <img>/<iframe>: object URL для файла подсистемы, прямая ссылка для внешнего. */
  src?: string;
  isLoading: boolean;
  error?: unknown;
  /** Только для файлов подсистемы — у внешней ссылки этих данных нет. */
  meta?: {sizeBytes: number; createdAt: string; createdByUserName?: string | null};
  download: {mode: "blob" | "newTab"; fileName?: string};
}

function useViewableSource(item: ViewableFile): ResolvedViewable;
```

Разница между видами локализована в этом хуке:

| | `dataFile` | `external` |
|---|---|---|
| Получение содержимого | авторизованный запрос → `Blob` → object URL (`useFileBlobUrl`) | `src` = сама ссылка, запроса нет |
| Тип содержимого | `contentType` из DTO | `contentType` из пропса, иначе выводится из расширения в URL |
| Состояние загрузки | из React Query | всегда `false` — грузит браузер |
| Скачивание | `blob` | `newTab` |

Хук вызывает `useQuery` безусловно и гасит его через `enabled: item.kind === "dataFile"` — условный вызов хука недопустим.

Тип внешней ссылки выводится по расширению (`.jpg`, `.png`, `.webp`, `.avif`, `.gif`, `.pdf`, с учётом query-строки). Если расширения нет — источник **оптимистично считается изображением**: подавляющее большинство внешних ссылок в приложении это картинки, а если предположение неверно, `onError` у `<img>` переключит элемент на `UnsupportedFileRenderer`. Отдельного запроса ради `Content-Type` не делаем — это лишний round-trip и упирается в CORS.

**Выбор способа отображения** — реестр рендереров, проверяемых по порядку; первый подошедший выигрывает:

```ts
const renderers: {
  match: (v: ResolvedViewable) => boolean;
  Component: ComponentType<{item: ResolvedViewable}>;
}[] = [
  {match: (v) => isImageContentType(v.contentType), Component: ImageFileRenderer},
  {match: (v) => v.contentType === "application/pdf", Component: PdfFileRenderer},
  {match: () => true, Component: UnsupportedFileRenderer},
];
```

- **`ImageFileRenderer`** — `<img src={item.src}>`. Масштабирование колесом и перетаскивание мышью/пальцем реализуются вручную (`transform: scale/translate`): библиотеки лайтбокса в проекте нет, а задача решается парой обработчиков. Двойной тап сбрасывает масштаб — в проекте уже есть `use-double-tap`. Для файлов подсистемы место резервируется по `imageWidth`/`imageHeight` из DTO, чтобы модалка не прыгала; для внешних ссылок размеры заранее неизвестны, поэтому используется контейнер фиксированной высоты. Обработчик `onError` переключает элемент на `UnsupportedFileRenderer` — это же основной путь деградации для внешней ссылки, оказавшейся не картинкой.
- **`PdfFileRenderer`** — `<iframe src={item.src}>` со встроенным просмотрщиком браузера. Библиотеку PDF не добавляем: `@vitejs/plugin-legacy` в проекте нацелен на `chrome >= 49`, под который современные сборки pdf.js не собираются. Для **внешних** PDF встроенный просмотрщик не используется — см. оговорки ниже.
- **`UnsupportedFileRenderer`** — иконка по типу, имя, размер (если известен), кнопка действия: «Скачать» для файла подсистемы, «Открыть в новой вкладке» для внешней ссылки.

**Тулбар:** имя, кнопки действия и, если передан `onDelete`, кнопка удаления. Размер, автор и дата загрузки берутся из `meta` и **скрываются для внешних ссылок** — этих данных у них нет; строка тулбара не должна схлопываться, поэтому блок метаданных занимает место всегда. Кнопка удаления для внешних источников не показывается: удалять там нечего. Клавиши: `Esc` — закрыть, `←`/`→` — переключение в списке.

#### Оговорки по внешним ссылкам

- **CORS не мешает отображению.** Мы подставляем ссылку в `src`, а не загружаем её через `fetch`, поэтому политика удалённого хоста на отображение не влияет. Обратная сторона — содержимое недоступно как `Blob`, а атрибут `download` на кросс-доменной ссылке браузер игнорирует. Поэтому «Скачать» для внешнего источника превращается в «Открыть в новой вкладке» (`target="_blank" rel="noopener noreferrer"`).
- **`<iframe>` может быть заблокирован** заголовком `X-Frame-Options` или `Content-Security-Policy: frame-ancestors` удалённого хоста, причём **без события ошибки** — пользователь увидит пустую рамку и не поймёт, почему. Надёжно определить это из JS нельзя, поэтому для внешних PDF встроенный просмотрщик не пытаемся показывать вовсе: сразу `UnsupportedFileRenderer` с кнопкой «Открыть в новой вкладке».
- **Смешанный контент.** Ссылка на `http://` со страницы, открытой по `https://`, блокируется браузером. Такие URL отбраковываются при нормализации и уходят в `UnsupportedFileRenderer` — молча битая картинка хуже явного сообщения.
- **Referrer.** Внешним изображениям ставится `referrerPolicy="no-referrer"`, чтобы внутренние адреса приложения не утекали на сторонний хост.
- **Кэш.** Внешние ссылки не проходят через React Query — их кэширует сам браузер по обычным HTTP-заголовкам.

**Мобильный режим:** `fullScreen` по `useMediaQuery(theme.breakpoints.down("sm"))` — так же, как в существующих диалогах проекта.

> **Ограничение нативного клиента.** WebView Android 7 (ТСД АТОЛ Smart Slim, см. [native-client.md](native-client.md)) **не рендерит PDF** ни в `<iframe>`, ни в `<object>` — покажет пустую рамку. `PdfFileRenderer` обязан определять эту среду (`Capacitor.isNativePlatform()`) и подставлять вместо просмотрщика `UnsupportedFileRenderer` с кнопкой скачивания, которая передаёт файл системному приложению. Изображения в WebView работают штатно.

---

## Порядок реализации

1. **Хранилище и домен.** `DataFilesOptions`, `IFileStorage` + `LocalFileStorage`, сущность `DataFile`, миграция, каталог в `Dockerfile` и volume в compose-файлах.
2. **API.** `FilesController` (загрузка, метаданные, отдача), коды ошибок, валидация по сигнатуре байтов, санитизация имени.
3. **Превью.** Библиотека ресайза, эндпоинт миниатюр, дисковый кэш.
4. **Сборка мусора.** `DataFilesGcJob`, построение запроса из метаданных EF, advisory-lock, регистрация в Quartz.
5. **Фронтенд, базовый слой.** `npm run generate-api`, тип `ViewableFile`, хуки `useFileUpload` / `useFileBlobUrl`, компонент `FileImage`, переводы кодов ошибок, `fileUtils`.
6. **Фронтенд, компоненты.** `FileInput`, `FileView`, `FileControl`.
7. **Модалка просмотра.** Рендереры, навигация по списку, внешние ссылки, ветка нативного клиента.
8. **Первое применение.** Фото у `CatalogItem` (1:1) и вложения у `Receipt` (1:N) — обкатка обоих видов привязки. Галерея изображений карточек Ozon и замена `CardImage` на `FileImage` — обкатка внешних источников.
9. **Документация.** См. ниже.

Шаги 1–4 и 5–7 независимы после согласования контракта API и могут идти параллельно.

## Документация к обновлению

| Файл | Что добавить |
|------|--------------|
| `docs/README.md` | Строка в индексе документации |
| `docs/api.md` | Эндпоинты `api/files` |
| `docs/errors.md` | Коды секции DataFiles |
| `docs/backend-patterns.md` | Паттерн «привязка файла через FK + GC по метаданным модели» |
| `docs/frontend.md` | Разделы по слоям компонентов, `FileImage`, модалке просмотра; обновление дерева каталогов; удаление `CardImage` из описания раздела маркетплейсов |
| `docs/native-client.md` | Оговорка про PDF в WebView |

## Отложено

- **S3-совместимое хранилище.** Вторая реализация `IFileStorage`, выбор через конфигурацию. Схема БД и контракты API не меняются.
- **Ревизия диска.** Обход хранилища в поисках байтов без строки в `DataFiles` (последствие падения между удалением строки и удалением файла). Отдельная задача с большим интервалом.
- **Складское ограничение чтения файлов.** См. «Права доступа».
- **Подписанные ссылки** для прямого `<img src>` с кэшем браузера.
- **Антивирусная проверка** загружаемых файлов.
- **Версионирование файла.** Сейчас замена — это новая строка `DataFile` и переставленная ссылка; история замен не хранится (при необходимости её даёт changelog сущности-владельца).
