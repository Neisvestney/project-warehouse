# ProjectWarehouse

ASP.NET Core 10 Web API + React (Vite) SPA. PostgreSQL через Docker Compose.

## Стек

- **Backend:** ASP.NET Core 10, EF Core 9, Npgsql, Serilog, AutoMapper
- **Frontend:** React 19, Vite, MUI, React Router
- **БД:** PostgreSQL 17

## Зависимости

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Локальная разработка

### 1. Переменные окружения

```bash
cp .env.example .env
```

### 2. Запустить PostgreSQL

```bash
docker compose up postgres -d
```

### 3. User Secrets (первый запуск)

JWT-ключ и пароль администратора хранятся в user secrets, не в репозитории:

```bash
cd ProjectWarehouse.Server
dotnet user-secrets set "Jwt:SecretKey" "your-secret-min-32-chars-here!!"
dotnet user-secrets set "Seed:AdminPassword" "YourAdminPassword1!"
```

`Seed:AdminUsername` по умолчанию `admin`.

### 4. Установить зависимости фронта

```bash
cd projectwarehouse.client
npm install
```

### 5. Запустить бэкенд

```bash
cd ProjectWarehouse.Server
dotnet run
```

Миграции применяются автоматически при старте. ASP.NET автоматически поднимает Vite dev-сервер через SPA Proxy.

- API: https://localhost:7095
- Vite dev: https://localhost:5173

---

## Docker (полный стек)

```bash
# Dev
docker compose up --build

# Prod
cp .env.example .env.prod  # заполнить POSTGRES_PASSWORD
docker compose --env-file .env.prod up --build
```

Приложение доступно на http://localhost:4587.

Образ собирается из `ProjectWarehouse.Server/Dockerfile` в две стадии: SDK-стадия
восстанавливает NuGet-пакеты и `npm ci`, затем `dotnet publish` собирает бэкенд и
через ProjectReference на `.esproj` прогоняет vite-сборку клиента в `wwwroot`.
Финальный образ — `aspnet:10.0` с непривилегированным пользователем, каталогами
`/keys` и `/data/files` под volume-монтирование и `HEALTHCHECK` на `GET /health`.
Сборка требует BuildKit: используются cache-mount'ы для `~/.nuget/packages` и `~/.npm`.

---

## Создание миграций

```bash
dotnet ef migrations add <MigrationName> --project ProjectWarehouse.Server
```

Применяются автоматически при следующем запуске сервера. Для ручного применения без запуска:

```bash
dotnet ef database update --project ProjectWarehouse.Server
```

---

## Нативный клиент (Android / ТСД)

Приложение можно упаковать в Android APK через Capacitor для работы на ТСД АТОЛ Smart Slim (Android 7).

Подробнее: [docs/native-client.md](docs/native-client.md)

```bash
cd projectwarehouse.client
npm run build
npx cap sync
npx cap open android
```

---

## Линтинг фронта

```bash
cd projectwarehouse.client
npm run eslint       # проверка
npm run eslint:fix   # автофикс
npm run prettier     # проверка форматирования
npm run prettier:fix # автофикс форматирования
```
