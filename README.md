# Positive Technologies — тестовое задание (SIEM API Services)

API управления документами на **.NET 10** (ASP.NET Core Minimal API). Сущность
`Document` содержит ровно **20 полей** разных типов (17 клиентских + 3 серверных:
`id`, `createdAt`, `updatedAt`). Реализованы CRUD, offset-пагинация, валидация
запросов, отдельный endpoint экспорта во внешний сервис (MockSiem) и лента событий
изменений для клиентов.

## Стек

- .NET 10, ASP.NET Core Minimal API, C# 13
- PostgreSQL 17, EF Core 10 (Npgsql)
- FluentValidation, OpenAPI + Scalar UI
- Transactional outbox + background dispatcher для событий
- NUnit 4, Docker Compose, StyleCop.Analyzers

## Быстрый старт (Docker Compose)

Секреты в репозиторий **не коммитятся**. Перед первым запуском создайте `.env`
из шаблона и задайте пароль:

```bash
cp .env.example .env
# отредактируйте .env и задайте POSTGRES_PASSWORD
```

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

## Конфигурация и секреты

Пароли и ключи **не хранятся в коде и не коммитятся**. Обязательные секреты:

| Переменная            | Где нужна                                  | Как задать                          |
|-----------------------|--------------------------------------------|-------------------------------------|
| `POSTGRES_PASSWORD`   | Docker Compose (Postgres, API)             | `.env` (см. `.env.example`)         |
| `POSTGRES_USER`       | Docker Compose                             | `.env`, по умолчанию `postgres`     |
| `POSTGRES_DB`         | Docker Compose                             | `.env`, по умолчанию `pt_test_db`   |
| `POSTGRES_TEST_DB`    | `scripts/test.sh` (интеграционные тесты)   | `.env`, по умолчанию `pt_test_db_tests` |
| `DB_CONNECTION`       | `dotnet ef` (миграции)                     | переменная окружения                |

`docker-compose.yml` использует `POSTGRES_PASSWORD` с обязательной подстановкой
`${POSTGRES_PASSWORD:?...}` — без `.env` запуск упадёт с понятной ошибкой. Файл
`.env` добавлен в `.gitignore`; в репозитории лежит только `.env.example` без реальных
значений.

### Локальный запуск API без Docker

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

### Миграции

Фабрика дизайн-тайма читает `DB_CONNECTION` из окружения (пароль в коде не хранится):

```bash
DB_CONNECTION="Host=localhost;Port=5432;Database=pt_test_db;Username=postgres;Password=<пароль>" \
  ./.tools/ef migrations add InitialCreate
```

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

Покрытие (локально, результат не коммитится):

```bash
./scripts/test.sh --collect:"XPlat Code Coverage"
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

## Архитектурные решения

| Решение | Почему | Альтернатива |
|---------|--------|--------------|
| Transactional outbox | Гарантирует, что событие публикуется ровно в момент сохранения изменения, без потерь при падении | Прямая публикация в обработчике (теряет события при сбое) |
| `FOR UPDATE SKIP LOCKED` + lease 5 мин + per-message save | Параллельные воркеры не обрабатывают одно сообщение дважды, падение не откатывает уже опубликованные | Чтение без блокировок (дубли), пакетный save (откатывает всю пачку) |
| `TitleKey` = `lower(title)` (computed, unique) | Регистронезависимая уникальность title на уровне БД → 409 | Проверка в приложении (гонки) |
| `DocumentWriteRequest` с nullable-типами | Отличает «поле не прислали» от «значение по умолчанию» для PUT | Отдельные DTO для POST/PUT (дублирование) |
| `text[]`, `uuid[]`, `jsonb` | Нативные типы Postgres для массивов/метаданных | Строковая сериализация (теряет индексацию и типы) |
| Enums как `varchar(50)` | Устойчивость к перестановке числовых значений | Хранение `int` (хрупко при рефакторинге) |
| `numeric(4,2)` для `QualityScore` | Точная десятичная арифметика | `double` (ошибки округления) |
| Export: snapshot id → батчи | Стабильный снимок при длительном экспорте | Потоковая пагинация (гонки с удалениями) |
| Minimal API | Меньше церемоний, читаемые endpoint'ы | Controllers |

## Соответствие требований

| Требование | Код | Тесты |
|------------|-----|-------|
| 20 полей разных типов | `src/PT.Domain/Entities/Document.cs` | `DocumentsCrudTests`, `DocumentWriteRequestValidatorTests` |
| CRUD + контракт | `src/PT.Api/Features/Documents/DocumentEndpoints.cs` | `DocumentsCrudTests` |
| Пагинация | `DocumentListQuery`, `PagedResult<T>` | `DocumentsPaginationTests` |
| Валидация запросов | `DocumentWriteRequestValidator`, `ValidationFilter<T>` | `DocumentWriteRequestValidatorTests` |
| Экспорт во внешний сервис | `DocumentExportService`, `HttpDocumentExporter` | `ExportTests`, `HttpDocumentExporterTests` |
| События изменений | `OutboxProcessor`, `OutboxDispatcher`, feed endpoint | `OutboxTests` |

## CI/CD

Конфигурация: [`.github/workflows/ci.yml`](.github/workflows/ci.yml). После пуша в
GitHub workflow запускается автоматически.

- **Linux**: полная сборка + все тесты (PostgreSQL поднимается как service container),
  сбор покрытия и выгрузка в Codecov.
- **Windows**: сборка + только unit-тесты (PostgreSQL недоступен, интеграционные
  пропускаются фильтром `PT.Tests.Unit`).
- Используется `dotnet-version: '10.0.x'` (проект на .NET 10, **не** 9.x).

Опциональные секреты репозитория (без них CI использует локальный dev-пароль):

- `TEST_DB_PASSWORD` — пароль Postgres в CI (по умолчанию `postgres`).
- `CODECOV_TOKEN` — токен Codecov для приватных репозиториев.

## Ограничения

- MockSiem хранит события и документы в памяти; после рестарта они теряются.
- Экспорт и доставка событий идут в один инстанс MockSiem (нет ретраев на уровне
  очереди сверх outbox-ретраев).
- Покрытие — см. текущий отчёт `dotnet test --collect:"XPlat Code Coverage"` (результат
  в репозиторий не добавляется).
