# ADR-002. Хранилище и очередь заданий

Статус: принято (M1a), реализовано в M1b (`studio/`). Вкладки заменены
серверами и разделами в ADR-004.

## Контекст

Студия хранит ассеты, историю их происхождения и очередь заданий к
серверам моделей (ADR-001). Работает один процесс студии на одном
ноутбуке. Серверы моделей запускает пользователь; студия не знает, когда
они появятся, и не следит за ресурсами.

## Решение

### Где лежат данные

Каталог данных — `$XDG_DATA_HOME/assets-studio` (по умолчанию
`~/.local/share/assets-studio`), переопределяется в конфиге. Он не
должен находиться в Dropbox. Внутри:

```text
studio.sqlite3           # метаданные и очередь
blobs/ab/abcdef….png     # файлы ассетов по sha256 содержимого
```

Файл записывается во временный путь в том же каталоге и атомарно
переименовывается. Одинаковое содержимое хранится один раз. Файлы
никогда не перезаписываются и не меняются.

### SQLite

Стандартный `sqlite3`, режим WAL, `foreign_keys=ON`, явный SQL без ORM.
Схема меняется пронумерованными миграциями (`storage/migrations/NNNN_*.sql`);
версия хранится в `PRAGMA user_version`. Время — UTC в ISO 8601.
Идентификаторы — ULID-подобные строки, сортируемые по времени создания.

```sql
CREATE TABLE assets (
  id          TEXT PRIMARY KEY,
  kind        TEXT NOT NULL CHECK (kind IN ('image', 'mesh', 'audio')),
  blob_sha256 TEXT NOT NULL,
  mime        TEXT NOT NULL,
  size_bytes  INTEGER NOT NULL,
  origin      TEXT NOT NULL CHECK (origin IN ('generated', 'upload', 'url')),
  source_url  TEXT,
  title       TEXT,
  favorite    INTEGER NOT NULL DEFAULT 0,
  meta        TEXT NOT NULL DEFAULT '{}',   -- JSON: seed, размеры, длительность…
  created_at  TEXT NOT NULL
);

CREATE TABLE asset_tags (
  asset_id TEXT NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
  tag      TEXT NOT NULL,
  PRIMARY KEY (asset_id, tag)
);

CREATE TABLE jobs (
  id              TEXT PRIMARY KEY,
  tab             TEXT NOT NULL,            -- id вкладки из конфига
  task            TEXT NOT NULL,
  prompt          TEXT,
  params          TEXT NOT NULL DEFAULT '{}',  -- как запрошено
  count           INTEGER NOT NULL CHECK (count >= 1),
  seed            INTEGER,                  -- после успеха: фактический seed
  status          TEXT NOT NULL CHECK (status IN (
                    'queued', 'waiting_model', 'running',
                    'succeeded', 'failed', 'cancelled')),
  error_code      TEXT,                     -- код контракта или студии, см. ниже
  error_message   TEXT,
  retryable_failures INTEGER NOT NULL DEFAULT 0,  -- подряд идущие повторяемые ошибки
  model_snapshot  TEXT,                     -- JSON: поле model из ответа /v1/generate
  effective_params TEXT,                    -- JSON: поле params из ответа (с умолчаниями)
  timing          TEXT,
  idempotency_key TEXT UNIQUE,
  retry_of        TEXT REFERENCES jobs(id),
  created_at      TEXT NOT NULL,
  started_at      TEXT,
  finished_at     TEXT
);
CREATE INDEX jobs_status ON jobs(status, created_at);

CREATE TABLE job_dependencies (
  job_id     TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  depends_on TEXT NOT NULL REFERENCES jobs(id),
  -- Какой результат предшественника и под какой ролью подать на вход.
  output_index INTEGER NOT NULL DEFAULT 0,
  input_role   TEXT NOT NULL,
  PRIMARY KEY (job_id, input_role)
);

CREATE TABLE job_inputs (
  job_id   TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  role     TEXT NOT NULL,
  asset_id TEXT NOT NULL REFERENCES assets(id),
  PRIMARY KEY (job_id, role)
);

CREATE TABLE job_outputs (
  job_id   TEXT NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
  position INTEGER NOT NULL,
  asset_id TEXT NOT NULL REFERENCES assets(id),
  PRIMARY KEY (job_id, position)
);

-- Роль входа заполняется либо готовым ассетом (job_inputs), либо
-- результатом предшественника (job_dependencies), но не обоими сразу:
-- это проверяется при создании задания.

CREATE TABLE lineage (
  parent_id TEXT NOT NULL REFERENCES assets(id),
  child_id  TEXT NOT NULL REFERENCES assets(id),
  relation  TEXT NOT NULL CHECK (relation IN ('input', 'mutation')),
  job_id    TEXT REFERENCES jobs(id),
  PRIMARY KEY (parent_id, child_id, relation)
);
```

Таблицы эволюции (`evolutions`, `candidates`) проектируются в M5: к тому
моменту будет понятно, чего требует интерфейс.

### Очередь

```text
queued ──► waiting_model ──► running ──► succeeded
   │            │               └──────► failed ──(retry)──► новое задание
   └────────────┴──► cancelled
```

- **Диспетчер** — фоновая задача в процессе студии. На каждый **адрес
  сервера** одновременно выполняется не больше одного задания: сервер всё
  равно обрабатывает запросы по одному, а несколько вкладок могут
  указывать на один сервер (например, text-to-image и image-to-image).
- Среди заданий одного сервера берётся самое раннее по `created_at`, у
  которого все зависимости `succeeded`. Задание, ждущее зависимость, не
  задерживает более поздние независимые задания. Если зависимость
  `failed` или `cancelled`, задание переходит в `failed` с кодом
  `dependency_failed`.
- Сервер недоступен или отвечает `loading`/`error` → задание в
  `waiting_model`. Как только `/v1/info` вернёт `ready`, задание уходит.
- Ответ `busy` — нормальное состояние (например, запрос из `curl` или
  другой программы): задание возвращается в `waiting_model` без
  ограничения числа попыток.
- Ошибка с `retryable: true` → задание возвращается в `waiting_model`;
  после 3 таких ошибок подряд — `failed` с кодом и сообщением последней.
- Любая другая ошибка → `failed` с `error_code` и `error_message` из
  контракта.
- Соединение оборвалось во время генерации (сервер упал) → `failed` с
  кодом `connection_lost`. Автоматического повтора нет: неизвестно,
  выполнилась ли генерация, а повтор мог бы снова уронить сервер.
- Таймаута ожидания ответа по умолчанию нет: генерация может идти
  минутами. Для вкладки его можно задать в конфиге (`timeout_s`); по
  истечении — `failed` с кодом `timeout`.
- При старте студии задания в `running` переводятся в `failed` с кодом
  `interrupted`: ответ сервера уже потерян, а повторная отправка без
  ведома пользователя могла бы задвоить генерацию.
- Задание может быть создано сразу в статусе `failed/dependency_failed`:
  если предшественник упал между проверкой запроса и записью задания.
  Клиенты API и MCP должны смотреть на статус созданного задания.
- Повтор упавшего задания — всегда **новое** задание с `retry_of`
  (кнопка «Повторить» в UI или вызов API); история не переписывается.
- Отмена возможна в `queued` и `waiting_model`. Генерацию, уже
  отправленную серверу, контракт v1 прервать не умеет.
- `idempotency_key` — повторный `POST /api/jobs` с тем же ключом
  возвращает существующее задание вместо создания нового.
- Результат сохраняется одной транзакцией: блобы записаны на диск до
  начала транзакции, затем `assets`, `job_outputs`, `lineage` и статус
  `succeeded`.

Коды ошибок студии дополняют коды контракта (ADR-001) в `error_code`:
`dependency_failed`, `connection_lost`, `timeout`, `interrupted`,
`invalid_response` (сервер ответил не по контракту).

## Альтернативы

- **ORM (SQLAlchemy).** Схема маленькая, явный SQL читается проще и
  людьми, и моделями; миграции — обычные SQL-файлы.
- **Отдельный процесс-Worker с lease/heartbeat.** Был нужен, пока студия
  запускала модели. Теперь студия — один процесс, и `running` после
  перезапуска однозначно означает прерванное задание.
- **Файлы рядом с БД в человекочитаемых путях** (`assets/<название>.png`).
  Контентная адресация исключает конфликты имён и дубли; человекочитаемые
  имена даёт экспорт.
