# Positive Technologies — тестовое задание (SIEM API Services)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?logo=postgresql)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-%E2%9C%93-2496ED?logo=docker)](https://www.docker.com/)
[![Tests](https://img.shields.io/badge/Tests-NUnit%204-2E7D32)](https://nunit.org/)
[![Build](https://github.com/GolrovKirill/Test_Task_PT-/actions/workflows/ci.yml/badge.svg)](https://github.com/GolrovKirill/Test_Task_PT-/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> API управления документами на **.NET 10** (ASP.NET Core Minimal API). Сущность
> `Document` содержит ровно **20 полей** разных типов (17 клиентских + 3 серверных:
> `id`, `createdAt`, `updatedAt`). Реализованы CRUD, offset-пагинация, валидация
> запросов, отдельный endpoint экспорта во внешний сервис (MockSiem) и лента событий
> изменений для клиентов.

## Стек

- .NET 10, ASP.NET Core Minimal API, C# 13
- PostgreSQL 17, EF Core 10 (Npgsql)
- FluentValidation, OpenAPI + Scalar UI
- Transactional outbox + background dispatcher для событий
- NUnit 4, Docker Compose, StyleCop.Analyzers

## Быстрый старт (Docker Compose)

Секреты в репозиторий **не коммитятся**. Создайте `.env` из шаблона:

```bash
cp .env.example .env
```

Пример заполненного `.env` — **это пример, значения вымышленные**, подставьте свой пароль:

```dotenv
POSTGRES_USER=postgres
POSTGRES_PASSWORD=super-secret-password
POSTGRES_DB=pt_test_db
POSTGRES_TEST_DB=pt_test_db_tests
```

> `POSTGRES_PASSWORD` обязателен: без него `docker compose` завершится ошибкой
> (`${POSTGRES_PASSWORD:?...}`). Сам файл `.env` добавлен в `.gitignore` и в
> репозиторий не попадает.

Запуск (скрипт отключает проблемный BuildKit/Bake-режим Compose v5):

```bash
./scripts/docker-up.sh
```

Остановка / полный сброс (удаляет том с данными):

```bash
./scripts/docker-down.sh          # остановить, том сохраняется
./scripts/docker-down.sh --reset  # остановить и удалить том
```

Порты после запуска:

| Сервис      | URL                                        |
|-------------|--------------------------------------------|
| API         | http://localhost:8080/scalar/v1            |
| MockSiem    | http://localhost:8081 (health `/health`)   |
| PostgreSQL  | localhost:5432                             |

Проверка:

```bash
curl -s http://localhost:8080/health/ready
```

> Миграции EF Core применяются автоматически при старте API — отдельная команда
> для их запуска не нужна.

## Локальный запуск API без Docker

Для локального запуска строка подключения задаётся через **user-secrets**
(`UserSecretsId` уже прописан в `PT.Api.csproj`):

```bash
cd src/PT.Api
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=pt_test_db;Username=postgres;Password=<пароль>"
dotnet user-secrets set "MockSiem:BaseUrl" "http://127.0.0.1:5082/"
dotnet run
```

Либо через переменную окружения `DB_CONNECTION`. API: http://localhost:5080,
MockSiem: http://localhost:5082.

## Тесты

Интеграционные тесты используют отдельную БД `pt_test_db_tests`, которая создаётся
автоматически. Запуск через скрипт (читает `.env`):

```bash
./scripts/test.sh                # все тесты
./scripts/test.sh --filter "FullyQualifiedName~PT.Tests.Unit"   # только unit
```

Без скрипта — экспортируйте строку подключения вручную:

```bash
export TEST_DB_CONNECTION="Host=127.0.0.1;Port=5432;Database=pt_test_db_tests;Username=postgres;Password=<пароль>"
dotnet test
```

## API

Базовый префикс: `/api/v1`.

| Метод   | Маршрут                       | Описание |
|---------|-------------------------------|----------|
| POST    | `/api/v1/documents`           | Создать документ (17 полей), событие `DocumentCreated` |
| GET     | `/api/v1/documents`           | Список с offset-пагинацией (`page`, `pageSize` 1..100) |
| GET     | `/api/v1/documents/{id}`      | Получить документ |
| PUT     | `/api/v1/documents/{id}`      | Полное обновление, событие `DocumentUpdated` |
| DELETE  | `/api/v1/documents/{id}`      | Удалить, событие `DocumentDeleted` |
| POST    | `/api/v1/documents/export`    | Экспорт всех документов в MockSiem |
| GET     | `/api/v1/documents/events`    | Лента событий изменений (`since`, `limit`) |
| GET     | `/health/live`, `/health/ready` | Healthcheck |

- Валидация — FluentValidation через `ValidationFilter<T>`; ошибки возвращаются как
  `400 application/problem+json` с полями в camelCase.
- Пагинация — ответ `{ items, page, pageSize, totalItems, totalPages }`, сортировка
  `createdAt desc, id desc`.
- Экспорт — батчами (`Export:BatchSize`, по умолчанию 100) с retry на 5xx/408/429;
  200 при частичном успехе, 502 если не экспортирован ни один документ.
- События — transactional outbox: событие пишется в той же транзакции, что и изменение,
  затем фоновый `OutboxDispatcher` доставляет его в MockSiem (`POST /api/v1/events`).

## Структура проекта и взаимодействие

```text
PositiveTechnologiesTest/
├── src/
│   ├── PT.Domain/          — сущности, enum'ы, события (без внешних зависимостей)
│   ├── PT.Infrastructure/  — EF Core, миграции, outbox, экспорт в SIEM
│   ├── PT.Api/             — Minimal API, endpoints, валидация, DI, health
│   └── PT.MockSiem/        — заглушка SIEM-приёмника (in-memory)
├── tests/
│   └── PT.Tests/           — unit- и интеграционные тесты (NUnit)
├── infra/postgres/init/    — SQL создания тестовой БД
├── scripts/                — docker-up.sh, docker-down.sh, test.sh
├── docker-compose.yml      — postgres + api + mock-siem
└── Directory.Build.props   — общие настройки сборки и StyleCop
```

Кто с кем и зачем общается:

```text
Клиент ──HTTP──> PT.Api ──EF Core──> PostgreSQL
                    │
                    ├── OutboxDispatcher ──HTTP──> MockSiem (/api/v1/events)
                    └── ExportService ─────HTTP──> MockSiem (/api/v1/import)
```

- **Клиент → PT.Api** — CRUD, пагинация, экспорт и лента событий по HTTP/JSON.
- **PT.Api → PostgreSQL** — документы и outbox-сообщения сохраняются в одной транзакции.
- **OutboxDispatcher → MockSiem** — доставка событий изменений (`POST /api/v1/events`)
  с ретраями; MockSiem играет роль внешнего SIEM-приёмника.
- **ExportService → MockSiem** — выгрузка всех документов батчами (`POST /api/v1/import`).
- **PT.Tests → PT.Api + PostgreSQL** — интеграционные тесты поднимают API через
  `WebApplicationFactory` и работают с отдельной тестовой БД; MockSiem заменён заглушками.

## Соответствие требований

| Требование                | Код                                                    | Тесты                                                      |
| ------------------------- | ------------------------------------------------------ | ---------------------------------------------------------- |
| 20 полей разных типов     | `src/PT.Domain/Entities/Document.cs`                   | `DocumentsCrudTests`, `DocumentWriteRequestValidatorTests` |
| CRUD + контракт           | `src/PT.Api/Features/Documents/DocumentEndpoints.cs`   | `DocumentsCrudTests`                                       |
| Пагинация                 | `DocumentListQuery`, `PagedResult<T>`                  | `DocumentsPaginationTests`                                 |
| Валидация запросов        | `DocumentWriteRequestValidator`, `ValidationFilter<T>` | `DocumentWriteRequestValidatorTests`                       |
| Экспорт во внешний сервис | `DocumentExportService`, `HttpDocumentExporter`        | `ExportTests`, `HttpDocumentExporterTests`                 |
| События изменений         | `OutboxProcessor`, `OutboxDispatcher`, `GET /api/v1/documents/events` | `OutboxTests`                              |

## Ограничения

- MockSiem хранит события и документы в памяти; после рестарта они теряются.
- Экспорт и доставка событий идут в один инстанс MockSiem (нет ретраев на уровне
  очереди сверх outbox-ретраев).
- Для интеграционных тестов нужен запущенный PostgreSQL (локально или в Docker).
