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

### 3. Установить зависимости фронта

```bash
cd projectwarehouse.client
npm install
```

### 4. Запустить бэкенд

```bash
cd ProjectWarehouse.Server
dotnet run
```

ASP.NET автоматически поднимает Vite dev-сервер через SPA Proxy.

- API: https://localhost:7095
- Vite dev: https://localhost:5173

### 5. Применить миграции (первый запуск)

```bash
dotnet ef database update --project ProjectWarehouse.Server
```

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

---

## Создание миграций

```bash
dotnet ef migrations add <MigrationName> --project ProjectWarehouse.Server
dotnet ef database update --project ProjectWarehouse.Server
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
