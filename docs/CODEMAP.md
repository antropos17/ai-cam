# BugCam — карта кодовой базы (одна страница)

> Обновляется на каждом закрытии блока. Карте никогда не верить вопреки коду:
> при любом расхождении прав код — карту чинить. Раздутая или протухшая карта
> хуже её отсутствия. Читать первой в свежей сессии, затем PLAN/STATUS.

## Ключевые входы

- **Вход поиска:** `GhostEvidencePlayModeHost.TryStartTowerSearch(GhostSearchEntry, source, out reject)` — единственный публичный вход (окно и меню). Entry-структура и резолвер настроек: `Editor/GhostSearchEntry.cs`. С A2 entry несёт `SceneKind` (Tower | CapturedScene): scene-режим захватывает открытую сцену в раннере (fail-closed → `SCENE_CAPTURE_FAILED`), статики идут через `EpsilonSearchRunner.StaticColliders`.
- **Захват сцены (A2):** `Core/SceneCapture.cs` — три исхода (captured / excluded-safely / fail-closed с per-object причинами), Box+Sphere (`Size` сферы = диаметр), кинематика → static (+Animator-предупреждение в окно/вердикт/manifest), stable ID = hierarchy path + sibling index, SHA-256 capture hash; секция `sceneCapture` в manifest (писатель) — только для scene-ранов.
- **Источники настроек (A1):** приоритет правка окна > ассет (GUID) > дефолты; единственный путь конструирования — `GhostSearchEntryResolver` (grep-пин: `CreateDefault(` — 0 в хосте/окне, 1 в резолвере). Границы валидации — консты в `DivergenceSettings`; полный контракт — `docs/CONTRACT-2.2.1.md`.
- **Писатель улик:** `Evidence/GhostEvidenceWriter.cs` (`Write`, `BuildManifestJson` — секция `settingsSource`, `BuildMetricsJson`). Раскладка: `Library/BugCamEvidence/Runs/<run-id>/` → `manifest.json`, `metrics.json`, `summary.md`, `report/console-report.txt`, `runs/baseline.json` + `fan-00…14`, `visuals/*.png`; указатель `.../Checkpoint/last-run.txt`.
- **Пины тестов:** source-scan пины хоста/окна — `Tests/EditMode/GhostWindowUxTests.cs` (Play Mode exit-маркер) и `GhostEvidenceTests.cs` (маршрут TryStart/coroutine); контракт A1 (таблица валидации verbatim, приоритет, mm↔m, персистентность) — `SearchEntryParameterizationTests.cs`; контракт захвата A2 (три исхода, детерминизм, hash, предупреждения) — `SceneCaptureTests.cs`; бит-идентичный повтор поиска (A5, закрывает PLAN 1.4 VERIFY (a)) — `Tests/PlayMode/SearchRepeatPlayModeTests.cs`.
- **Гейт-числа башни (блок 2.2):** threshold `1.98919879E-05` м (19.9 µm), первый кадр 27 (body 49), 21/49 тел, 191×. Сдвиг при дефолтах = STOP.
- **Выходной гейт A8 (домино, captured-scene path):** `Tests/DominoScene.unity` (5 домино 0.02×0.1×0.06 м + ground; body 1 = наклонённый триггер = дефолтная цель scene-режима) — run `ghost-20260803T174400539…`: THRESHOLD BRACKET FOUND 2.30 мм = порог обнаружения возмущения на самой цели, не цепная дивергенция (product-находка в STATUS). Agreement-замер score- vs позиционной половины гейта на домино: 202/480, opposite-direction 175 ⇒ **score-половина AND-гейта закреплена навсегда** (адъюдикация 2026-08-03, вопрос удаления закрыт).
- **Контракт блока 2.2.2 (mesh-захват, ратифицирован 2026-08-04):** `docs/CONTRACT-2.2.2.md` — ровно два расширения захвата (статический MeshCollider любой convexity → CapturedStatic; динамика + convex MeshCollider → CapturedDynamic), меш по ссылке (assetGuid + localFileId + contentHash геометрии всех субмешей) через инжектируемый провайдер (интерфейс в Core, реализация в Editor, без `#if UNITY_EDITOR` в Core), новый код `SCENE_MESH_RESOLVE_FAILED`, хэш захвата аддитивен (Box/Sphere-строки байт-в-байт). **Код блока ещё не написан** — карта отражает контракт, не код.
- **Batchmode:** `Tools/BugCam/run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\<dir>`.

## Core (`Assets/BugCam/Core`, ноль зависимостей от Evidence/Editor)

- `SimulationHarness.cs` — прогон в свежей локальной PhysicsScene (Play Mode), применение возмущения, deterministic fail вне Play Mode; здесь же `BugCamConstants` (FixedStep 0.02f, StateStride 14), `SimulationColliderShape` (Box|Sphere) и `SimulationStaticColliderDefinition`; `StaticColliders == null` в запросе ⇒ легаси-ground башни, non-null ⇒ ровно захваченные статики
- `SceneCapture.cs` — A2-захват открытой сцены → `SceneCaptureResult` (тела/статики/per-object записи/предупреждения/hash)
- `StateRecorder.cs` — плоские массивы `[runs×steps×bodies×14]`
- `RunResult.cs` — результат прогона + метаданные возмущения
- `DivergenceEngine.cs` — по-шаговый скор + AND-гейт (порог, sustained, ≥1 тело)
- `DivergenceResult.cs`, `DivergenceThresholds.cs` — структуры результатов/порогов
- `DivergenceSettings.cs` — ScriptableObject: все пороги/веса, Default*-консты, границы валидации A1
- `EpsilonSearch.cs` — машина фаз Baseline→Ladder→Exponential→Bisection→Fan
- `EpsilonSearchSettings.cs` — struct-вид полей поиска + enum `EpsilonSearchStrategy`
- `EpsilonSearchRunner.cs` — корутинный насос поверх `EpsilonSearch`
- `EpsilonSearchResult.cs` — вердикты/диапазоны/вилка
- `TowerProbeRequestFactory.cs` — процедурная башня: stable ID 1…48 кирпичи, 49 снаряд
- `DeterminismProbe.cs`, `KinematicReplayer.cs`, `TowerCheckpointMetrics.cs` — A/B/A′-проба, реплей, метрики чекпойнта

## Evidence (`Assets/BugCam/Evidence`)

- `GhostEvidenceBuilder.cs` — единый документ улик (fail-closed проверки фанов)
- `GhostEvidenceDocument.cs` — документ + `GhostSearchIdentity`, `GhostSettingsProvenance`, снапшоты окружения/физики
- `GhostEvidenceWriter.cs` / `GhostEvidenceSchema.cs` / `GhostEvidenceReport.cs` — запись бандла, схема/коды ошибок, консольный отчёт
- `GhostRenderer.cs`, `GhostDrawSet.cs`, `GhostTrajectorySampler.cs`, `GhostBodyRanking.cs` — draw-set для Scene View, топ-N тел
- `EvidenceCameras.cs` (+ `EvidenceCameraMath/PlanSchema/PlanWriter`) — детерминированный выбор камер, `camera-plan.json` (рендер — не начат, Block 2.3)

## Editor (`Assets/BugCam/Editor`)

- `GhostVisualizationWindow.cs` — окно (машина состояний IDLE→READY→SEARCHING→DONE, секция «Настройка» A1, причины verbatim)
- `GhostEvidencePlayModeHost.cs` — единый Play Mode-пайплайн поиска, SessionState-персистентность entry, TEMP-раннер
- `GhostSearchEntry.cs` — entry/резолвер/каталог целей (display-name provider) A1
- `GhostVisualizationSession.cs`, `GhostSceneViewDrawer.cs` — сессия Scene View, легенда/маркеры (низ-право)
- `GhostScreenshotCapture.cs` — PNG-виз в бандл улик
- `TowerSceneGenerator.cs`, `TowerProbeRequestFactory.cs` (Editor), `TowerScenePreviewExporter.cs`, `TowerCheckpointAutomation.cs`, `BugCamTestAutomation.cs`, `DeterminismProbeRunner.cs`, `PhysicsSettingsProbe.cs` — генерация башни, автоматизация чекпойнтов, чтение editor-only настроек физики
- `PR4VisualInspectionWindow.cs` — untracked-защищённый (не коммитить)

## Tests

- EditMode (`BugCam.Tests`, reflection-стиль, без ссылок на asmdef'ы): контракты Core, поиск, улики, окно-UX, A1-параметризация
- PlayMode (`BugCam.Tests.PlayMode`): локальные PhysicsScene-прогоны, детерминизм башни, A/B/A′
- `Tests/TowerScene.unity` — демо/тест-сцена (PlayMode-прогон её трогает и откатывает)
- `Tests/DominoScene.unity` — вторая сцена выходного гейта A8: реальные объекты через `SceneCapture` (не фабрика); тестами не регенерируется
