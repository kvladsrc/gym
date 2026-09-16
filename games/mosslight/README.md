# Mosslight

Небольшой 3D-прототип: робот исследует парящий сад, собирает пять огоньков
и возвращается к каменным воротам. Уровень и персонаж созданы в Blender.

## Запуск

Применить `home-manager` из корня monorepo. Он предоставляет Unity Hub,
официальный `unity` CLI и `blender-mcp`. Установить через Hub Editor
**6000.6.0f1** с Linux Build Support.

Открыть в Hub существующий проект `games/mosslight/unity`.
Редактор запускает человек; команды ниже подключаются к нему и не запускают
второй Editor. Для игры открыть `Assets/Scenes/Mosslight.unity` и нажать Play.

Управление: WASD/стрелки — движение, Shift — бег, Space — прыжок,
правая кнопка мыши — камера, колесо — расстояние, R — сначала.

Команды из корня репозитория:

```sh
nix develop -c just mosslight assets
nix develop -c just mosslight status
nix develop -c just mosslight prepare
nix develop -c just mosslight build
nix develop -c just mosslight play
nix develop -c just mosslight smoke
nix develop -c just mosslight check
```

`assets` генерирует FBX и `.blend` локально в headless Blender.
`assets-mcp` выполняет тот же скрипт через работающий Blender MCP,
в отдельной сцене Mosslight; другие сцены не удаляются.
`prepare` пересоздаёт только демонстрационную сцену из исходников и FBX:
ручные изменения этой сцены будут заменены, поэтому сохранять их отдельно.
Запускать prepare/build при остановленном Play Mode.
`build` использует асинхронные MCP-команды `build`/`build_status`, сверяет
идентификатор сборки и сохраняет отчёт в `artifacts/build-report.json`.
Длительную сборку нельзя помещать в `eval`: его тайм-аут не отменяет сборку.

## Что хранится в Git

C#, настройки Unity, manifest/lockfile пакетов, текстовая сцена, материалы,
`.meta` и скрипт генерации ассетов. FBX, `.blend`, кеши и сборки игнорируются.
Метаданные FBX сохраняются: GUID связывают модели со сценой и должны
оставаться прежними после повторной генерации. Внешние ассеты не нужны.

В чистой рабочей копии выполнить `assets` до первого открытия проекта.
Сцена и материалы уже сохранены; `prepare` нужен при изменении генератора
или сборщика сцены. Масштаб Unity: один метр на единицу.

## Автоматизация и проверки

Unity CLI **1.0.0-beta.9**, Pipeline **0.6.0-exp.1**.
Blender MCP **1.6.4**, Python MCP SDK **1.30.0**.
Пакеты workstation задаются в Home Manager; отдельного Unity в devShell нет.
Проверенный процесс для агента описан в
[unity-workspace](../../.agents/skills/unity-workspace/SKILL.md).

`just mosslight mcp-config` показывает настройки для Codex.
`just mosslight mcp-config --apply` обновляет только секции Blender/Unity
в пользовательском config.toml. Unity MCP закрепляется за этим проектом.
Для проверки протокола без перезапуска клиента:

```sh
nix develop -c python3 games/mosslight/tools/mcp_call.py --server unity list
```

`check` проверяет синтаксис Python/JSON, уникальность GUID, наличие `.meta`,
исключение бинарных артефактов из Git и shellcheck launcher.
Zuul job `mosslight-source-check` использует только Python, Git и Bash.
Он не компилирует C# и не доказывает работоспособность Unity.

`prepare` проверяет ссылки сцены, масштаб робота и коллизию под стартовой
точкой. Локальная сборка компилирует C#. `smoke` запускает настоящий Linux Player
с GPU, проводит робота к кристаллу, проверяет движение/сбор/высоту и пишет
`artifacts/player-smoke.log` и `artifacts/smoke.png`. Проверка имеет тайм-аут.
Изображение нужно просмотреть отдельно: успешная физика не гарантирует
правильные материалы и видимость персонажа.

`artifacts/build/Mosslight.x86_64` — локальная сборка. Unity в CI не требуется.

## Границы прототипа

Персонаж использует CharacterController и процедурный шаг отдельных частей
модели, без skeletal rig. Рендерер — встроенный Standard, без URP/HDRP.
Арты и механика оригинальные; стороннего платного контента нет.
Следующие усложнения добавляются по потребностям игры.
