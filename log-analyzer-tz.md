# Техническое задание
# Web-приложение для анализа логов по времени

## 1. Назначение системы

Необходимо разработать web-приложение для загрузки, индексации, просмотра и анализа логов приложения и IIS-логов.

Основной сценарий работы системы:

1. Пользователь загружает файлы, папку или архив с логами.
2. Система рекурсивно находит все лог-файлы.
3. Система определяет формат логов и индексирует события.
4. Пользователь видит список найденных логов.
5. Пользователь открывает один из логов и выбирает интересующую строку.
6. Система автоматически показывает события из остальных логов за выбранный временной интервал.
7. Пользователь может менять интервал в секундах, фильтровать события и смотреть детали.

---

## 2. Цели разработки

Система должна обеспечивать:

- загрузку логов через web-интерфейс;
- загрузку одного файла, нескольких файлов, папки или архива;
- рекурсивный обход вложенных папок;
- автоматическое определение формата логов;
- парсинг логов приложения, NLog и IIS W3C;
- обработку многострочных исключений;
- индексацию событий по времени;
- быстрый поиск событий по временному диапазону;
- просмотр содержимого выбранного лога;
- корреляцию событий между разными логами по времени;
- фильтрацию по уровню, источнику, тексту и параметрам IIS;
- отображение прогресса импорта;
- хранение метаданных в SQLite;
- хранение событий логов в ClickHouse.

---

## 3. Технологический стек

### 3.1. Backend и UI

- ASP.NET Core
- Blazor Server / Blazor Web App
- C#
- Razor Components

### 3.2. Фоновые задачи

Для MVP:

- `BackgroundService`

Для расширенной версии:

- Hangfire

### 3.3. Базы данных

- SQLite — метаданные, проекты, загрузки, список файлов, статусы импорта.
- ClickHouse — события логов и быстрые запросы по времени.

### 3.4. Хранилище файлов

- Filesystem storage.

Пример структуры:

```text
/storage/{projectId}/{uploadId}/original/
/storage/{projectId}/{uploadId}/extracted/
```

### 3.5. UI-компоненты

Рекомендуемые варианты:

- Blazor built-in components
- QuickGrid
- MudBlazor
- Blazor `Virtualize`

---

## 4. Общая архитектура

```text
Browser
  ↓
Blazor UI
  ↓
ASP.NET Core Application
  ↓
Background Import Worker
  ↓
SQLite — метаданные
ClickHouse — события логов
Filesystem — оригинальные файлы
```

---

## 5. Основные модули системы

### 5.1. Web UI

Отвечает за:

- создание проекта анализа;
- загрузку логов;
- отображение прогресса импорта;
- отображение списка найденных логов;
- просмотр событий;
- выбор строки лога;
- отображение событий вокруг выбранного времени;
- фильтрацию и поиск;
- просмотр деталей события.

### 5.2. Backend Application Layer

Отвечает за:

- управление проектами;
- управление загрузками;
- запуск фоновой индексации;
- работу с SQLite;
- работу с ClickHouse;
- выполнение запросов анализа;
- подготовку данных для UI.

### 5.3. Import Worker

Отвечает за:

- чтение загруженных файлов;
- распаковку архивов;
- рекурсивный обход директорий;
- определение формата логов;
- парсинг строк;
- обработку многострочных событий;
- пакетную запись событий в ClickHouse;
- обновление прогресса импорта в SQLite.

### 5.4. Parser Layer

Отвечает за парсинг разных типов логов:

- основной формат приложения;
- NLog plain format;
- IIS W3C logs;
- fallback parser для неизвестных форматов.

### 5.5. Storage Layer

Отвечает за:

- сохранение оригинальных файлов;
- хранение распакованных архивов;
- удаление временных файлов;
- защиту от небезопасных путей внутри архивов.

---

## 6. Основные сущности

## 6.1. Project

Проект анализа логов.

### Поля

| Поле | Тип | Описание |
|---|---|---|
| `id` | UUID / TEXT | Идентификатор проекта |
| `name` | TEXT | Название проекта |
| `description` | TEXT | Описание |
| `created_at` | DATETIME | Дата создания |
| `updated_at` | DATETIME | Дата обновления |

---

## 6.2. UploadSession

Сессия загрузки и импорта логов.

### Поля

| Поле | Тип | Описание |
|---|---|---|
| `id` | UUID / TEXT | Идентификатор загрузки |
| `project_id` | UUID / TEXT | Идентификатор проекта |
| `status` | TEXT | Статус загрузки |
| `original_name` | TEXT | Имя исходного файла или архива |
| `total_files` | INTEGER | Всего найдено файлов |
| `processed_files` | INTEGER | Обработано файлов |
| `total_lines` | INTEGER | Всего строк |
| `processed_lines` | INTEGER | Обработано строк |
| `error_count` | INTEGER | Количество ошибок импорта |
| `created_at` | DATETIME | Дата создания |
| `finished_at` | DATETIME | Дата завершения |

### Статусы

```text
created
uploading
uploaded
indexing
completed
failed
cancelled
```

---

## 6.3. LogFile

Найденный лог-файл.

### Поля

| Поле | Тип | Описание |
|---|---|---|
| `id` | UUID / TEXT | Идентификатор файла |
| `project_id` | UUID / TEXT | Идентификатор проекта |
| `upload_session_id` | UUID / TEXT | Идентификатор загрузки |
| `original_path` | TEXT | Исходный путь |
| `stored_path` | TEXT | Путь хранения на сервере |
| `display_name` | TEXT | Отображаемое имя |
| `size_bytes` | INTEGER | Размер файла |
| `hash` | TEXT | Хэш файла |
| `format` | TEXT | Распознанный формат |
| `timezone` | TEXT | Часовой пояс |
| `first_timestamp` | DATETIME | Первое найденное время |
| `last_timestamp` | DATETIME | Последнее найденное время |
| `line_count` | INTEGER | Количество строк |
| `status` | TEXT | Статус обработки |

### Форматы

```text
app_pipe_log
nlog_plain
iis_w3c
unknown
```

---

## 6.4. LogEvent

Событие лога. Хранится в ClickHouse.

### Основные поля

| Поле | Тип | Описание |
|---|---|---|
| `project_id` | UUID | Идентификатор проекта |
| `upload_session_id` | UUID | Идентификатор загрузки |
| `log_file_id` | UUID | Идентификатор лог-файла |
| `timestamp_utc` | DateTime64 | Время события в UTC |
| `timestamp_ms` | Int64 | Время события в миллисекундах |
| `level` | String | Уровень события |
| `source` | String | Источник / logger |
| `thread_id` | String | Идентификатор потока |
| `line_number` | UInt64 | Номер строки |
| `byte_offset` | UInt64 | Смещение в файле |
| `message` | String | Сообщение |
| `exception` | String | Исключение / stack trace |
| `raw_text` | String | Исходный текст события |

### Дополнительные поля IIS

| Поле | Тип | Описание |
|---|---|---|
| `http_method` | String | HTTP-метод |
| `url` | String | URL |
| `status_code` | UInt16 | HTTP-статус |
| `client_ip` | String | IP клиента |
| `server_ip` | String | IP сервера |
| `user_agent` | String | User-Agent |
| `time_taken` | UInt32 | Время обработки запроса |

---

## 7. Требования к загрузке логов

Система должна поддерживать загрузку:

1. одного `.log` файла;
2. нескольких файлов;
3. папки с логами;
4. папки с вложенными папками;
5. ZIP-архива.

Во второй версии желательно добавить поддержку:

- `.7z`;
- `.tar`;
- `.tar.gz`.

---

## 8. Требования к обработке архивов

При загрузке архива система должна:

1. сохранить оригинальный архив;
2. безопасно распаковать архив во временную директорию;
3. заблокировать path traversal;
4. ограничить максимальный размер распакованных данных;
5. ограничить количество файлов в архиве;
6. ограничить глубину вложенности;
7. рекурсивно найти лог-файлы;
8. передать найденные файлы в очередь индексации.

Запрещается использовать пути из архива напрямую без нормализации.

---

## 9. Требования к парсингу логов

## 9.1. Основной формат приложения

Пример:

```text
WARN | 247 | 2026-04-21 05:45:29.6611 | Message text | Exception text |
```

Извлекаемые поля:

- `level`;
- `thread_id`;
- `timestamp`;
- `message`;
- `exception`;
- `raw_text`.

Пример шаблона:

```regex
^(?<level>\w+)\s+\|\s+(?<thread>\d+)\s+\|\s+(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)\s+\|\s+(?<message>.*?)\s+\|\s+(?<exception>.*)
```

---

## 9.2. NLog plain format

Пример:

```text
2026-02-18 14:22:33.8035 Warn Logger: Message text
```

Извлекаемые поля:

- `timestamp`;
- `level`;
- `message`;
- `raw_text`.

Пример шаблона:

```regex
^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)\s+(?<level>\w+)\s+(?<message>.*)
```

---

## 9.3. IIS W3C logs

Пример:

```text
#Fields: date time s-ip cs-method cs-uri-stem sc-status time-taken
2026-04-21 05:45:29 10.0.0.1 GET /api/test 500 123
```

Система должна читать строку `#Fields` и определять порядок колонок.

Извлекаемые поля:

- `date`;
- `time`;
- `timestamp`;
- `server_ip`;
- `client_ip`;
- `http_method`;
- `url`;
- `status_code`;
- `time_taken`;
- `user_agent`;
- `raw_text`.

---

## 9.4. Unknown / fallback parser

Если формат файла не распознан, система должна:

- сохранить файл со статусом `unknown`;
- попытаться найти timestamp в строках;
- сохранить непарсируемые строки как raw-события;
- показать пользователю количество ошибок парсинга.

---

## 10. Многострочные события

Система должна корректно обрабатывать многострочные exceptions.

Правило:

```text
Если строка начинается с распознаваемого timestamp / level / шаблона события,
она считается началом нового события.

Если строка не соответствует началу события,
она считается продолжением предыдущего события.
```

Продолжение должно добавляться в:

- `exception`;
- `raw_text`.

---

## 11. Работа с временем

Все события должны приводиться к UTC и сохраняться в поле:

```text
timestamp_utc
```

Также необходимо сохранять числовое значение времени:

```text
timestamp_ms
```

Требования:

- при импорте пользователь может указать timezone;
- для IIS W3C должна быть настройка `IIS logs are UTC`;
- в UI время отображается в выбранном часовом поясе;
- поиск по времени выполняется по UTC.

---

## 12. Индексация

Индексация должна выполняться в фоне.

### Процесс индексации

1. Пользователь загружает файлы.
2. Backend создает `UploadSession`.
3. Файлы сохраняются на сервер.
4. Создается фоновая задача индексации.
5. Worker определяет формат каждого файла.
6. Worker читает файл построчно.
7. Worker формирует `LogEvent`.
8. Worker пакетно записывает события в ClickHouse.
9. Worker обновляет прогресс в SQLite.
10. UI отображает прогресс импорта.

### Пакетная запись

Рекомендуемый размер batch для ClickHouse:

```text
5 000–50 000 событий
```

---

## 13. Требования к поиску по времени

Главный сценарий системы:

```text
Пользователь выбирает строку лога.
Система получает timestamp выбранного события.
Пользователь задает интервал до и после события.
Система показывает все события из выбранных или всех логов за этот интервал.
```

### Параметры поиска

- `project_id`;
- `selected_timestamp`;
- `before_seconds`;
- `after_seconds`;
- `selected_log_files`;
- `levels`;
- `text_query`.

### Отображаемые поля результата

- `timestamp`;
- `delta` относительно выбранной строки;
- `level`;
- `source`;
- `log_file`;
- `thread_id`;
- `message`.

---

## 14. UI: основные страницы

## 14.1. Страница проектов

Функции:

- просмотр списка проектов;
- создание нового проекта;
- открытие проекта;
- удаление проекта.

---

## 14.2. Страница загрузки

Функции:

- загрузка файла;
- загрузка нескольких файлов;
- загрузка папки;
- загрузка ZIP-архива;
- выбор timezone;
- настройка обработки IIS-логов;
- запуск импорта;
- отображение прогресса.

Поля формы:

- название проекта;
- описание;
- timezone;
- признак `IIS logs are UTC`;
- признак `combine multiline exceptions`;
- выбор файлов / папки / архива.

---

## 14.3. Страница прогресса импорта

Должна отображать:

- текущий статус;
- количество найденных файлов;
- количество обработанных файлов;
- количество обработанных строк;
- количество ошибок;
- текущий обрабатываемый файл;
- время начала;
- время завершения;
- кнопку отмены импорта.

---

## 14.4. Страница списка логов

Должна отображать:

| Колонка | Описание |
|---|---|
| Имя файла | Отображаемое имя |
| Путь | Исходный путь |
| Формат | Распознанный формат |
| Размер | Размер файла |
| Строк | Количество строк |
| Первый timestamp | Первое событие |
| Последний timestamp | Последнее событие |
| Статус | Статус обработки |

Фильтры:

- по имени файла;
- по формату;
- по статусу;
- по временному диапазону.

---

## 14.5. Страница анализа

Страница анализа должна состоять из следующих областей:

```text
┌──────────────────────────────────────────────────────────────┐
│ Project / Time range / Search / Interval                     │
├───────────────┬──────────────────────────────────────────────┤
│ Log files     │ Events table                                 │
│               │                                              │
│ [x] App log   │ time | delta | level | source | message       │
│ [x] IIS log   │                                              │
│ [x] NLog      │                                              │
├───────────────┴──────────────────────────────────────────────┤
│ Selected event details                                       │
└──────────────────────────────────────────────────────────────┘
```

### Левая панель

Список логов:

- checkbox выбора лога;
- имя файла;
- формат;
- количество событий;
- количество ERROR/WARN;
- временной диапазон файла.

### Верхняя панель

Фильтры:

- временной диапазон;
- интервал до события;
- интервал после события;
- уровни событий;
- текстовый поиск;
- выбранные источники;
- очистка фильтров.

### Центральная таблица

Колонки:

| Колонка | Описание |
|---|---|
| `time` | Время события |
| `delta` | Разница относительно выбранного события |
| `level` | Уровень |
| `source` | Источник |
| `log_file` | Файл |
| `thread_id` | Поток |
| `message` | Сообщение |

### Панель деталей

Должна отображать:

- `raw_text`;
- `message`;
- `exception`;
- `file path`;
- `line number`;
- `timestamp`;
- `level`;
- `metadata`;
- IIS-поля, если событие из IIS.

---

## 15. Виртуализация таблиц

UI не должен рендерить большое количество строк напрямую.

Обязательно использовать:

- `Virtualize`;
- QuickGrid с виртуализацией;
- либо другой Blazor grid с server-side paging / virtualization.

Требования:

- данные загружаются постранично;
- один запрос не должен возвращать неограниченное количество строк;
- таблица должна корректно работать на десятках и сотнях тысяч событий.

---

## 16. Фильтрация

Система должна поддерживать фильтрацию:

- по временному диапазону;
- по выбранным лог-файлам;
- по уровню события;
- по тексту;
- по `thread_id`;
- по `source/logger`;
- по HTTP-статусу для IIS;
- по URL для IIS;
- по HTTP-методу для IIS.

Уровни событий:

```text
ERROR
WARN
INFO
DEBUG
TRACE
FATAL
```

Для IIS:

```text
2xx
3xx
4xx
5xx
```

---

## 17. Timeline

Система должна предоставлять агрегированное представление событий по времени.

Пример:

```text
05:45 — ERROR: 2, WARN: 10, INFO: 30
05:46 — ERROR: 0, WARN: 3, INFO: 21
05:47 — ERROR: 1, WARN: 8, INFO: 25
```

Поддерживаемые bucket intervals:

- 1 second;
- 5 seconds;
- 10 seconds;
- 1 minute;
- 5 minutes;
- 1 hour.

Timeline должен строиться на основе данных из ClickHouse.

---

## 18. API / Application endpoints

Даже при использовании Blazor рекомендуется иметь API/application endpoints для основных операций.

## 18.1. Projects

```http
POST   /api/projects
GET    /api/projects
GET    /api/projects/{projectId}
DELETE /api/projects/{projectId}
```

## 18.2. Uploads

```http
POST /api/projects/{projectId}/uploads
POST /api/uploads/{uploadId}/files
POST /api/uploads/{uploadId}/start
GET  /api/uploads/{uploadId}/status
POST /api/uploads/{uploadId}/cancel
```

## 18.3. Logs

```http
GET /api/projects/{projectId}/logs
GET /api/logs/{logFileId}/events?offset=0&limit=500
GET /api/events/{eventId}
```

## 18.4. Time correlation

```http
GET /api/projects/{projectId}/events/around
```

Параметры:

```text
timestamp
beforeSeconds
afterSeconds
logFileIds
levels
query
```

## 18.5. Search

```http
GET /api/projects/{projectId}/events/search
```

Параметры:

```text
from
to
query
levels
logFileIds
limit
offset
```

## 18.6. Timeline

```http
GET /api/projects/{projectId}/timeline
```

Параметры:

```text
from
to
bucket
levels
logFileIds
```

---

## 19. SQLite schema

```sql
CREATE TABLE projects (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE upload_sessions (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    status TEXT NOT NULL,
    original_name TEXT NULL,
    total_files INTEGER DEFAULT 0,
    processed_files INTEGER DEFAULT 0,
    total_lines INTEGER DEFAULT 0,
    processed_lines INTEGER DEFAULT 0,
    error_count INTEGER DEFAULT 0,
    created_at TEXT NOT NULL,
    finished_at TEXT NULL
);

CREATE TABLE log_files (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    upload_session_id TEXT NOT NULL,
    original_path TEXT NOT NULL,
    stored_path TEXT NOT NULL,
    display_name TEXT NOT NULL,
    size_bytes INTEGER,
    hash TEXT,
    format TEXT,
    timezone TEXT,
    first_timestamp TEXT NULL,
    last_timestamp TEXT NULL,
    line_count INTEGER DEFAULT 0,
    status TEXT NOT NULL
);

CREATE TABLE import_errors (
    id TEXT PRIMARY KEY,
    upload_session_id TEXT NOT NULL,
    log_file_id TEXT NULL,
    line_number INTEGER NULL,
    error_message TEXT NOT NULL,
    raw_text TEXT NULL,
    created_at TEXT NOT NULL
);
```

Рекомендуемые индексы:

```sql
CREATE INDEX ix_upload_sessions_project_id
ON upload_sessions(project_id);

CREATE INDEX ix_log_files_project_id
ON log_files(project_id);

CREATE INDEX ix_log_files_upload_session_id
ON log_files(upload_session_id);

CREATE INDEX ix_import_errors_upload_session_id
ON import_errors(upload_session_id);
```

---

## 20. ClickHouse schema

```sql
CREATE TABLE log_events
(
    project_id UUID,
    upload_session_id UUID,
    log_file_id UUID,

    timestamp_utc DateTime64(4, 'UTC'),
    timestamp_ms Int64,

    level LowCardinality(String),
    source String,
    thread_id String,

    line_number UInt64,
    byte_offset UInt64,

    message String,
    exception String,
    raw_text String,

    http_method LowCardinality(String),
    url String,
    status_code UInt16,
    client_ip String,
    server_ip String,
    user_agent String,
    time_taken UInt32
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(timestamp_utc)
ORDER BY (project_id, timestamp_utc, log_file_id, line_number);
```

Основной запрос поиска по времени:

```sql
SELECT *
FROM log_events
WHERE project_id = {projectId:UUID}
  AND timestamp_utc BETWEEN {from:DateTime64}
                        AND {to:DateTime64}
ORDER BY timestamp_utc, log_file_id, line_number
LIMIT 10000;
```

Запрос событий выбранного лога:

```sql
SELECT *
FROM log_events
WHERE project_id = {projectId:UUID}
  AND log_file_id = {logFileId:UUID}
ORDER BY timestamp_utc, line_number
LIMIT {limit:UInt32}
OFFSET {offset:UInt32};
```

Запрос timeline:

```sql
SELECT
    toStartOfInterval(timestamp_utc, INTERVAL 1 minute) AS bucket,
    level,
    count() AS count
FROM log_events
WHERE project_id = {projectId:UUID}
  AND timestamp_utc BETWEEN {from:DateTime64}
                        AND {to:DateTime64}
GROUP BY bucket, level
ORDER BY bucket;
```

---

## 21. Структура решения

Рекомендуемая структура solution:

```text
LogAnalyzer.sln

/src
  LogAnalyzer.Web
    Blazor UI
    ASP.NET Core endpoints
    auth
    upload endpoints

  LogAnalyzer.Application
    use cases
    import orchestration
    query services

  LogAnalyzer.Domain
    Project
    UploadSession
    LogFile
    LogEvent
    parser abstractions

  LogAnalyzer.Infrastructure
    SQLite repositories
    ClickHouse writer/query services
    filesystem storage
    archive extraction

  LogAnalyzer.Worker
    background import worker
    optional for separated worker process

/tests
  LogAnalyzer.Tests
```

Для MVP `Worker` можно держать внутри `LogAnalyzer.Web` как `BackgroundService`.

---

## 22. Парсеры

Рекомендуемый интерфейс парсера:

```csharp
public interface ILogParser
{
    string Name { get; }

    bool CanParse(LogSample sample);

    IAsyncEnumerable<ParsedLogEvent> ParseAsync(
        Stream stream,
        LogParserContext context,
        CancellationToken cancellationToken);
}
```

Реализации:

```text
PipeSeparatedAppLogParser
NLogPlainParser
IisW3CParser
FallbackTextParser
```

---

## 23. Производительность

Система должна:

- не загружать весь лог в память;
- читать файлы потоково;
- писать события в ClickHouse батчами;
- отображать строки в UI через virtual scrolling;
- ограничивать размер результата запросов;
- поддерживать пагинацию;
- показывать прогресс импорта;
- не блокировать UI во время индексации.

Целевые показатели для MVP:

| Метрика | Значение |
|---|---|
| Скорость импорта | не менее 50 000 строк/сек на обычном сервере |
| Поиск по интервалу | до 1 секунды для типовых запросов |
| Размер страницы событий | 500–1000 строк за один запрос |
| Размер файла | от нескольких МБ до нескольких ГБ |

---

## 24. Безопасность

Система должна:

- ограничивать максимальный размер загрузки;
- проверять расширения файлов;
- безопасно распаковывать архивы;
- запрещать path traversal;
- ограничивать количество файлов в архиве;
- ограничивать глубину вложенности;
- не исполнять содержимое загруженных файлов;
- хранить файлы в изолированной директории;
- логировать ошибки импорта;
- не показывать пользователю системные пути сервера без необходимости.

---

## 25. Логирование самой системы

Приложение должно вести собственные логи:

- старт импорта;
- завершение импорта;
- ошибки парсинга;
- ошибки записи в ClickHouse;
- ошибки чтения файлов;
- ошибки распаковки архивов;
- отмена импорта пользователем;
- длительные запросы анализа.

---

## 26. MVP-состав

В первую версию должны войти:

1. Web-интерфейс на Blazor.
2. Создание проекта анализа.
3. Загрузка log-файлов.
4. Загрузка ZIP-архивов.
5. Рекурсивный поиск логов.
6. Фоновая индексация.
7. SQLite для метаданных.
8. ClickHouse для событий.
9. Парсинг основного формата приложения.
10. Парсинг NLog plain.
11. Парсинг IIS W3C.
12. Обработка многострочных exceptions.
13. Список найденных логов.
14. Просмотр событий выбранного лога.
15. Выбор события и поиск всех логов вокруг его времени.
16. Настройка интервала `before_seconds` и `after_seconds`.
17. Фильтр по уровню события.
18. Детальная панель выбранной строки.
19. Прогресс импорта.
20. Базовый timeline.

---

## 27. Что можно вынести во вторую версию

Во вторую версию можно вынести:

1. Поддержку `.7z`, `.tar`, `.tar.gz`.
2. Продвинутый полнотекстовый поиск.
3. Группировку похожих ошибок.
4. Экспорт найденного временного среза.
5. Сохранение пользовательских фильтров.
6. Авторизацию и разграничение доступа.
7. Сравнение двух импортов.
8. Автоматическое определение timezone.
9. Графики и heatmap ошибок.
10. Автоматический поиск корреляций.
11. Интеграцию с внешними источниками логов.
12. Импорт логов по URL или из сетевой папки.
13. Ролевую модель пользователей.
14. Очистку старых проектов по retention policy.

---

## 28. Критерии приемки MVP

MVP считается выполненным, если:

1. Пользователь может создать проект анализа.
2. Пользователь может загрузить `.log` файлы.
3. Пользователь может загрузить ZIP-архив.
4. Система находит логи внутри архива и подпапок.
5. Система индексирует события в ClickHouse.
6. Метаданные загрузки и файлов сохраняются в SQLite.
7. UI показывает список найденных логов.
8. Пользователь может открыть лог и увидеть события.
9. Пользователь может выбрать строку лога.
10. Система показывает события из других логов за выбранный интервал.
11. Пользователь может менять интервал в секундах.
12. Пользователь может фильтровать события по уровню.
13. Детали выбранного события отображают `raw_text` и `exception`.
14. Импорт выполняется в фоне.
15. UI показывает прогресс импорта.
16. Большие файлы не загружаются целиком в память.
17. Многострочные exceptions отображаются как одно событие.
18. IIS W3C-логи корректно парсятся.
19. Timeline строится по данным из ClickHouse.
20. Ошибки импорта сохраняются и доступны пользователю.

---

## 29. Основной пользовательский сценарий

```text
1. Пользователь открывает приложение.
2. Создает проект анализа.
3. Загружает ZIP-архив или набор лог-файлов.
4. Указывает timezone.
5. Запускает импорт.
6. Видит прогресс обработки.
7. После завершения открывает список найденных логов.
8. Выбирает нужный лог.
9. Видит события этого лога.
10. Кликает на интересующую строку.
11. Указывает интервал, например 30 секунд до и 10 секунд после.
12. Система показывает события из всех выбранных логов вокруг этого времени.
13. Пользователь анализирует последовательность событий.
14. При необходимости фильтрует по ERROR/WARN/IIS 5xx.
15. Открывает детали конкретного события.
```

---

## 30. Итоговая рекомендация по реализации

Рекомендуемый стек первой версии:

```text
UI:
Blazor Server / Blazor Web App

Backend:
ASP.NET Core

Language:
C#

Metadata DB:
SQLite

Log Events DB:
ClickHouse

Storage:
Filesystem

Background Processing:
BackgroundService

UI Tables:
QuickGrid / MudBlazor / Virtualize

Realtime Progress:
Blazor Server circuit или SignalR
```

Ключевой принцип системы:

```text
SQLite хранит состояние анализа и метаданные.
ClickHouse хранит события логов и отвечает на быстрые аналитические запросы.
Blazor отображает интерфейс.
ASP.NET Core управляет загрузкой, индексацией и поиском.
```
