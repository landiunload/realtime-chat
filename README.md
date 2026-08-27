# Realtime Chat

Небольшой чат на ASP.NET Core SignalR с историей в PostgreSQL. Решение использует
vertical slices: контракт и модель сообщений находятся рядом с хабом, а PostgreSQL
подключён через узкий интерфейс хранилища.

## Стек

- .NET 10 LTS / C# 14, EF Core 10.0.11, Npgsql 10.0.3;
- SignalR JavaScript 10.0.11 с SRI-проверкой;
- PostgreSQL 17.11 Alpine, Docker Compose;
- xUnit 4, Microsoft Testing Platform и NSubstitute 6.2.

SDK и полный граф NuGet зафиксированы в `global.json` и `packages.lock.json`.
Восстановление проверяет прямые и транзитивные уязвимости. Все зависимости имеют
разрешительные open-source-лицензии; перечень — в `THIRD-PARTY-NOTICES.md`.

## Запуск

```powershell
Copy-Item .env.example .env
# замените пример пароля в .env
docker compose up --build
```

Откройте <http://localhost:8081>. Контейнер приложения работает не от root, с
read-only filesystem и без Linux capabilities. Пароль базы не хранится в Git.

## Проверка

```powershell
dotnet restore --locked-mode
dotnet test -c Release
dotnet list RealtimeChat.slnx package --vulnerable --include-transitive
dotnet list RealtimeChat.slnx package --deprecated --include-transitive
dotnet list RealtimeChat.slnx package --outdated --include-transitive
```

## Границы

Это анонимный демонстрационный чат: имя не подтверждает личность. Сервер привязывает
имя и комнату к соединению, ограничивает входящий пакет, число сообщений в DOM и
разрешённые browser origins; CSP не допускает произвольный inline-код, а CDN-скрипт
проверяется через SRI. Для публичного развёртывания всё равно нужны TLS,
аутентификация, rate limiting, миграции/резервные копии PostgreSQL и собственные
значения `AllowedHosts` и `Security:AllowedHubOrigins`.

Образ PostgreSQL оставлен на последнем патче ветки 17: переход на major 18 требует
явной миграции данных и поэтому намеренно не выполняется поверх существующего volume.
