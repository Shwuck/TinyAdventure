# Tiny Adventure JSON Data Flow Review

## Executive summary

The JSON-driven content direction is sound, but the current implementation is not yet robust or truly mod-friendly. The project has a recognizable pipeline:

`StreamingAssets JSON -> loader MonoBehaviour -> PermaLists -> generators/factories/managers -> runtime objects`

That flow is easy to describe at a high level, but it is not consistently enforced in code. Loader ownership is fragmented, loader ordering is inspector-dependent, some loaders are not wired into the central manager, some files referenced by loaders are missing, some files present in `StreamingAssets` are not loaded, and static definitions are frequently mutated after deserialization.

The biggest technical risks are:

- `DataLoaderManager` depends on a manually ordered inspector list.
- `MaterialDataLoader` uses `async void`, so the manager can finish before material data is actually loaded.
- `NPCDataLoader` calls `LoadData()` in `Start()` and is also listed in `DataLoaderManager`, so it can load outside the intended sequence.
- `MonsterDataLoader`, `LootDataLoader`, and `SmithingRecipeDataLoader` appear not to be in the central loader list.
- `LootCreationData.json`, `EntityLootData.json`, and `MonsterCreationData.json` are referenced by loaders but are not present in `Assets/StreamingAssets`.
- `DialogueScripts.json` and `ItemCreationData.json` exist but are not used by the active loaders, and both fail JSON syntax parsing.
- `JsonUtility` is used on models with C# properties and dictionaries, which Unity's serializer generally does not populate reliably. `EventDataLoader` and `LandmarkDataLoader` are therefore suspect.
- WebGL builds will not support the current `File.Exists` / `File.ReadAllText` approach for `StreamingAssets`.

My candid view: the current system is useful for developer-authored JSON content, but it is not yet a stable modding pipeline. It needs a small amount of centralization, validation, and deterministic load sequencing before player-added data will be safe.

## Current architecture overview

Most content is loaded from `Assets/StreamingAssets` by MonoBehaviour loader scripts. Most loaders implement `IDataLoader` and are invoked by `DataLoaderManager.LoadAllData()`. Loaded definitions are stored directly on `PermaLists.Instance`.

Runtime systems then read those lists:

- `ItemGenerator` and `ItemFactory` use item, material, smithing, and naming data.
- `NPCGenerator` uses races, names, backgrounds, loadouts, role data, dialogue scripts, personalities, anatomy, and items.
- `AnimalGenerator` and `AnimalFactory` use animal, material, terrain, and anatomy data.
- `AnatomyGenerator` uses anatomy and body-part data.
- `DialogueManager` caches `PermaLists.Instance.DialogueScripts` after integrity checking.
- `CraftingManager`, `CookingManager`, and `SmithingPanelUI` consume recipe and item definitions.
- `LandmarkGenerator` consumes landmark data.
- `CivilisationManager` consumes village creation data.
- `MonsterGenerator` consumes monster creation data, but that dataset appears not to be loaded by the central path.

`IntegrityChecker.CheckDataIntegrity()` runs after the manager completes and then calls `GameManager.Instance.StartGame()`. That makes data loading part of the start-game gate, but the gate is only as safe as the manager's ordering and synchronous behavior.

## JSON file inventory

Loaded by active central manager list:

| JSON file | Loader | Target storage |
|---|---|---|
| `Assets/StreamingAssets/ChangeLog/ChangeLog.json` | `ChangeLogDataLoader` | static `ChangeLogDataLoader.changelogData` |
| `Assets/StreamingAssets/NPCCreationData.json` | `NPCDataLoader` | names, `Races`, `Backgrounds`, `Loadouts`, `RoleData` in `PermaLists` |
| `Assets/StreamingAssets/MaterialCreationData.json` | `MaterialDataLoader` | `PermaLists.ObjectMaterials` |
| `Assets/StreamingAssets/LandmarkCreationData.json` | `LandmarkDataLoader` | `PermaLists.LandmarkCreationData` |
| `Assets/StreamingAssets/VillageCreationData.json` | `VillageDataLoader` | `PermaLists.VillageCreationData` |
| `Assets/StreamingAssets/EventCreationData.json` | `EventDataLoader` | `PermaLists.EventCreationData` |
| `Assets/StreamingAssets/Weapons.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/Tools.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/Apparel.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/Miscellaneous.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/FruitAndVeg.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/FleshAndBone.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/CookedMeals.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/Constructables.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/Components.json` | `ItemDataLoader` | merged into `PermaLists.ItemCreationData` |
| `Assets/StreamingAssets/ItemNameData.json` | `ItemDataLoader` | `PermaLists.ItemNamingData` |
| `Assets/StreamingAssets/AnimalCreationData.json` | `AnimalDataLoader` | `PermaLists.AnimalCreationData` |
| `Assets/StreamingAssets/PersonalityCreationData.json` | `PersonalityDataLoader` | `PermaLists.Personalities` |
| `Assets/StreamingAssets/DialogueScriptsVillager.json` | `DialogueDataLoader` | merged into `PermaLists.DialogueScripts` |
| `Assets/StreamingAssets/DialogueScriptsTrader.json` | `DialogueDataLoader` | merged into `PermaLists.DialogueScripts` |
| `Assets/StreamingAssets/DialogueScriptsBlacksmith.json` | `DialogueDataLoader` | merged into `PermaLists.DialogueScripts` |
| `Assets/StreamingAssets/DialogueScriptsWarior.json` | `DialogueDataLoader` | merged into `PermaLists.DialogueScripts` |
| `Assets/StreamingAssets/CookingRecipeData.json` | `CookingRecipeDataLoader` | `PermaLists.CookingRecipeList` |
| `Assets/StreamingAssets/CraftingRecipeData.json` | `CraftingRecipeDataLoader` | `PermaLists.CraftingRecipeList` |
| `Assets/StreamingAssets/AnatomyData.json` | `AnatomyDataLoader` | `PermaLists.AnatomyData` |
| `Assets/StreamingAssets/BodyPartCreationData.json` | `AnatomyDataLoader` | `PermaLists.BodyPartData` |

Referenced by loaders but not found in `Assets/StreamingAssets`:

| JSON file | Loader | Impact |
|---|---|---|
| `MonsterCreationData.json` | `MonsterDataLoader` | monster generation cannot use JSON monster definitions unless the file exists and loader runs |
| `LootCreationData.json` | `LootDataLoader` | `LootGenerator.GenerateLoot()` has no loot data |
| `EntityLootData.json` | `LootDataLoader` | entity-specific loot data has no source |

Present but apparently not loaded by the active pipeline:

| JSON file | Status |
|---|---|
| `Assets/StreamingAssets/ItemCreationData.json` | not read by `ItemDataLoader`; JSON syntax check fails |
| `Assets/StreamingAssets/ItemCreationData - Copy.json` | not read |
| `Assets/StreamingAssets/DialogueScripts.json` | not read by `DialogueDataLoader`; JSON syntax check fails |
| `Assets/StreamingAssets/DialogueScriptsWarrior.json` | not read; loader reads misspelled `DialogueScriptsWarior.json` |
| `Assets/StreamingAssets/SmithingRecipeData.json` | loader exists, but `SmithingRecipeDataLoader` does not appear in the manager list |

Direct JSON reads outside the content-loader path:

| File | Purpose |
|---|---|
| `SaveSystem.cs` | saves/loads map data from `Application.persistentDataPath` with `JsonUtility` |
| `PlayerProgress.cs` | saves/loads player progress from `Application.persistentDataPath` with Newtonsoft |

These are save-state systems, not static content loaders, but they are still JSON file IO paths.

## Loader inventory

Central manager-compatible loaders:

- `ChangeLogDataLoader`
- `CookingRecipeDataLoader`
- `MonsterDataLoader`
- `AnimalDataLoader`
- `AnatomyDataLoader`
- `CraftingRecipeDataLoader`
- `DialogueDataLoader`
- `EventDataLoader`
- `ItemDataLoader`
- `LandmarkDataLoader`
- `LootDataLoader`
- `MaterialDataLoader`
- `NPCDataLoader`
- `PersonalityDataLoader`
- `SmithingRecipeDataLoader`
- `VillageDataLoader`

Problematic or inconsistent loaders:

- `DungeonDataLoader` does not implement `IDataLoader`, calls `LoadData()` from `Start()`, and its `LoadDungeonCreationDataFromJson()` immediately returns. It is effectively a stub.
- `NPCDataLoader` implements `IDataLoader` but also calls `LoadData()` from `Start()`.
- `MaterialDataLoader` implements `IDataLoader` but internally uses `async void`, so completion is not awaitable by `DataLoaderManager`.
- `CookingRecipeDataLoader`, `MonsterDataLoader`, and `ChangeLogDataLoader` live at `Assets` root rather than `Assets/DataLoaders`.
- `EventDataLoader` and `LandmarkDataLoader` use `JsonUtility` despite model classes containing properties and dictionaries.

Current `SampleScene` manager list, based on serialized file IDs and scene object order:

1. `ChangeLogDataLoader`
2. `NPCDataLoader`
3. `MaterialDataLoader`
4. `LandmarkDataLoader`
5. `VillageDataLoader`
6. `EventDataLoader`
7. `ItemDataLoader`
8. `AnimalDataLoader`
9. `PersonalityDataLoader`
10. `DialogueDataLoader`
11. `CookingRecipeDataLoader`
12. `CraftingRecipeDataLoader`
13. `AnatomyDataLoader`

Notably absent from that list:

- `MonsterDataLoader`
- `LootDataLoader`
- `SmithingRecipeDataLoader`

## Data flow map

Items:

- Source: split item files such as `Weapons.json`, `Tools.json`, `Apparel.json`, etc.
- Loader: `ItemDataLoader`.
- Model: `ItemCreationData`, plus `ItemNamingData`.
- Storage: `PermaLists.ItemCreationData`, `PermaLists.ItemNamingData`.
- Consumers: `ItemGenerator`, `ItemFactory`, `NPCGenerator`, `PlayerCharacter`, `CraftingManager`, `CookingManager`, `SmithingPanelUI`, `PlantFlowerManager`, object interactions, inventories.
- Reachability: yes for split item files. No for `ItemCreationData.json`, which appears orphaned and invalid.

Materials:

- Source: `MaterialCreationData.json`.
- Loader: `MaterialDataLoader`.
- Model: `ObjectMaterial`.
- Storage: `PermaLists.ObjectMaterials`.
- Consumers: `ItemFactory`, `AnimalGenerator.GenerateMaterialsFromNativeAnimals()`, smithing/item creation.
- Reachability: likely, but unsafe because load completion is async and not guaranteed before integrity checks or item generation.

NPC/race/background/job/loadout/name data:

- Source: `NPCCreationData.json`.
- Loader: `NPCDataLoader`.
- Model: `NPCcreationData`, `Race`, `SubRace`, `Background`, `Loadout`, `NPCRoleData`.
- Storage: multiple `PermaLists` fields.
- Consumers: `NPCGenerator`, `CharacterCreationUI`, `PlayerCharacter`, `RaceManager`, `CivilisationGenerator`, `NPCManager`, `PlayerProgress`, `IntegrityChecker`.
- Reachability: yes, but `NPCDataLoader` can self-load outside manager order.

Personalities and dialogue:

- Sources: `PersonalityCreationData.json`, role-specific dialogue JSON files.
- Loaders: `PersonalityDataLoader`, `DialogueDataLoader`.
- Models: `Personality`, `DialogueScript`, `PersonalityDialogue`, `DialogueLines`.
- Storage: `PermaLists.Personalities`, `PermaLists.DialogueScripts`.
- Consumers: `NPCGenerator.GetRandomPersonality()`, `DialogueManager`.
- Reachability: yes for the four role-specific dialogue files currently named in code. `DialogueScriptsWarrior.json` is ignored; `DialogueScriptsWarior.json` is loaded.

Anatomy and body parts:

- Sources: `AnatomyData.json`, `BodyPartCreationData.json`.
- Loader: `AnatomyDataLoader`.
- Models: `AnatomyData`, `BodyPartData`.
- Storage: `PermaLists.AnatomyData`, `PermaLists.BodyPartData`.
- Consumers: `AnatomyGenerator`, `NPCGenerator`, `AnimalFactory`, `Monster` construction, `PlayerCharacter`.
- Reachability: likely yes, but current manager order loads anatomy last. This is only safe if no generator runs before `IntegrityChecker` finishes and `StartGame()` is called.

Animals:

- Source: `AnimalCreationData.json`.
- Loader: `AnimalDataLoader`.
- Model: `AnimalCreationDataList`, `AnimalCreationData`.
- Storage: `PermaLists.AnimalCreationData`.
- Consumers: `AnimalGenerator`, `AnimalFactory`, `MapGenerator`, nested areas, player interactions, `IntegrityChecker`.
- Reachability: yes. Loader mutates data after load by lowercasing symbols, inferring diet, and setting mountability.

Villages:

- Source: `VillageCreationData.json`.
- Loader: `VillageDataLoader`.
- Model: `VillageCreationDataWrapper`, `VillageCreationData`.
- Storage: `PermaLists.VillageCreationData`.
- Consumers: `CivilisationManager`, `NPCGenerator`, `IntegrityChecker`.
- Reachability: yes.

Landmarks:

- Source: `LandmarkCreationData.json`.
- Loader: `LandmarkDataLoader`.
- Model: `LandmarkCreationDataList`, `LandmarkCreationData`.
- Storage: `PermaLists.LandmarkCreationData`.
- Consumers: `LandmarkGenerator`, `LandmarkManager`.
- Reachability: uncertain because `JsonUtility` likely will not populate property-backed fields and dictionaries correctly.

Events:

- Source: `EventCreationData.json`.
- Loader: `EventDataLoader`.
- Model: `EventCreationDataList`, `EventCreationData`.
- Storage: `PermaLists.EventCreationData`.
- Consumers: not clearly found in the reviewed generation path.
- Reachability: uncertain; same `JsonUtility` property issue.

Crafting/cooking/smithing recipes:

- Sources: `CraftingRecipeData.json`, `CookingRecipeData.json`, `SmithingRecipeData.json`.
- Loaders: `CraftingRecipeDataLoader`, `CookingRecipeDataLoader`, `SmithingRecipeDataLoader`.
- Models: `CraftingRecipe`, `CookingRecipe`, `SmithingRecipe`.
- Storage: `PermaLists.CraftingRecipeList`, `PermaLists.CookingRecipeList`, `PermaLists.SmithingRecipeList`.
- Consumers: `CraftingManager`, `CookingManager`, `SmithingPanelUI`, `ItemFactory`.
- Reachability: crafting and cooking yes. Smithing likely no because the loader is not in the manager list.

Monsters:

- Source expected: `MonsterCreationData.json`.
- Loader: `MonsterDataLoader`.
- Model: `MonsterCreationDataList`, `MonsterCreationData`.
- Storage: `PermaLists.MonsterCreationData`.
- Consumers: `MonsterGenerator`, nested areas.
- Reachability: no evidence of central load. The expected JSON file is missing.

Loot:

- Sources expected: `LootCreationData.json`, `EntityLootData.json`.
- Loader: `LootDataLoader`.
- Models: `LootCreationDataWrapper`, `EntityLootDataWrapper`.
- Storage: `PermaLists.LootCreationData`, `PermaLists.EntityLootData`.
- Consumers: `LootGenerator`, possibly object/corpse interactions indirectly.
- Reachability: no evidence of central load. Expected JSON files are missing.

## DataLoaderManager assessment

`DataLoaderManager` is very simple: it has a public `List<MonoBehaviour> dataLoaders`, casts entries to `IDataLoader` in `Awake()`, then runs each loader in list order.

Strengths:

- Simple and understandable.
- Catches exceptions thrown synchronously by `LoadData()`.
- Logs each loader's success or failure.
- Provides one obvious place to run final integrity checks.

Weaknesses:

- Discovery is manual inspector assignment, not code discovery.
- Order is entirely inspector-dependent.
- Dependencies are hidden. Nothing says that item data should load after materials or before loadout validation.
- Missing loaders are easy to miss. The scene currently appears to omit monster, loot, and smithing recipe loaders.
- `LoadAllData()` treats every loader as synchronous, but `MaterialDataLoader` is not synchronous in practice.
- A loader can catch its own errors, log them, and return without throwing, causing `DataLoaderManager` to log "data loaded successfully" even when data did not load.
- Success is not tied to count, required files, schema validation, or dependency validation.
- There is no result object such as `LoadResult { success, errors, warnings, count }`.

Failure behavior:

- Missing JSON file: usually logged as warning or error, but the manager may still continue and start the game.
- Malformed JSON: some loaders catch Newtonsoft exceptions; others do not. Manager catches synchronous exceptions only.
- Duplicate ID/name/key: mostly not checked. List-based stores allow duplicates; dictionary stores can silently skip or overwrite depending on implementation.
- Schema mismatch: weakly detected. Newtonsoft will often default missing values silently. `JsonUtility` is especially risky here.
- Null nested data: some generators handle nulls, but many assume lists/dictionaries are non-null.

## PermaLists assessment

`PermaLists` is currently both a static content registry and a runtime world-state registry.

Static loaded definitions stored there include:

- item creation data
- item naming data
- object materials
- recipes
- race/background/loadout/role/name data
- personalities
- dialogue scripts
- animal creation data
- monster creation data
- village creation data
- landmark/event creation data
- anatomy/body-part data

Runtime state stored there includes:

- all characters
- NPCs
- animals
- monsters
- villages
- objects
- map cells
- nested areas
- generated native animals/materials
- terrain counts
- regions

This is too broad for a mod-friendly architecture. It works as a convenient global registry, but it couples unrelated systems and makes it hard to tell whether a list contains immutable definitions or live mutable game state.

Specific risks:

- Most collections are publicly mutable.
- Definitions are changed after load: item excluded materials, weapon slots, weapon damage types, animal diet/mountability, race unlock state, default body types, loadout cleanup.
- Runtime systems append generated data back into content-adjacent lists, for example `AnimalGenerator.GenerateMaterialsFromNativeAnimals()` appends new materials to `ObjectMaterials`.
- Lookup style is inconsistent: some systems use name strings, some enums, some dictionaries, some list searches.
- Duplicates are not centrally rejected. Duplicate item names, race names, material names, or body-part names can cause ambiguous lookups.
- `PermaLists` is hard to test because almost everything depends on a singleton.

`PermaLists` can remain useful, but it should not be the long-term home for every content type and every runtime object in the same mutable surface.

## Generator/Factory consumption map

Item generation:

- Uses `PermaLists.ItemCreationData`, `ObjectMaterials`, `SmithingRecipeList`, and `ItemNamingData`.
- Copies definitions into new `Item` instances via `ItemFactory`.
- Also mutates loaded item definitions in `ItemDataLoader`.
- Falls back in some places, but can fail or throw if materials are empty because `GetRandomMaterial()` assumes a non-empty list before `Last()`.
- Uses `UnityEngine.Random`; not consistently isolated from other generation systems.
- Hardcoded generation logic remains extensive: modifier categories, on-hit effects, damage/resistance weighting, level point ranges, excluded item types.

NPC generation:

- Uses race/name/loadout/role/personality/dialogue/anatomy/item data from `PermaLists`.
- Good separation in that it creates runtime `NPC` objects rather than using raw definitions directly.
- Falls back to `John Doe`, default `Human`, and default personality in some cases.
- Hidden dependencies are heavy: village data, race data, dialogue, personalities, loadouts, items, materials, smithing, and anatomy must all be valid.
- Weighted generation depends on hardcoded constants and string race names.
- Seeded generation uses `UnityEngine.Random.InitState(GameManager.Instance.GameSeed + npcCounter)`, which can preserve determinism locally but also resets Unity's global RNG and can affect other generation if interleaved.

Animal generation:

- Uses `PermaLists.AnimalCreationData`, terrain counts, native animal dictionaries, object materials, and anatomy.
- Runtime animal objects are created by `AnimalFactory`.
- It uses both `UnityEngine.Random` and `System.Random`; `System.Random` is not seeded from the game seed in several places, weakening deterministic generation.
- Adds generated hide materials into `PermaLists.ObjectMaterials`, mixing generated runtime-derived content into loaded static material definitions.

Anatomy generation:

- Uses `PermaLists.AnatomyData` and `BodyPartData`.
- Reasonably separated from loading.
- Fails noisily when data is missing, but callers may only receive null and continue.
- Body-part dictionary uses body-part name as key. Duplicate body-part names in JSON are silently ignored by `StoreBodyPartWithSubparts()` because it only adds if missing.

Dialogue:

- Loader merges role-specific files into `PermaLists.DialogueScripts`.
- `DialogueManager` caches the list via `SetDialogueScripts()`, called by `IntegrityChecker`.
- There are fallbacks to default role/personality dialogue.
- Schema consistency is fragile. Some old/unloaded files use different key styles (`roles` vs `Roles`, `type/dialogue/greetings` vs `Personality/Dialogue/Introduction`).
- `DialogueScriptsWarrior.json` exists, but loader uses misspelled `DialogueScriptsWarior.json`.

Loot:

- `LootGenerator` expects `PermaLists.LootCreationData`.
- Loader and JSON files are not currently wired/present, so this path likely does not work through JSON.
- Other monster loot paths in `ItemGenerator.GenerateMonsterLoot()` are hardcoded by item type and do not use `LootCreationData`.

Village/settlement generation:

- `VillageDataLoader` feeds `CivilisationManager.GetVillageCreationData()`.
- `NPCGenerator` uses village race distributions.
- `VillageGenerator` placement itself is terrain/scoring logic, not primarily JSON-driven.
- Modding village types is limited by enums such as `VillageType` and `TerrainType`.

Crafting/cooking/smithing:

- Crafting and cooking managers consume loaded recipe lists directly.
- Smithing UI and `ItemFactory` expect `PermaLists.SmithingRecipeList`, but the loader does not appear to be in the active manager list.
- Recipe schemas use mixed key styles: crafting by item name, cooking by `ItemType`, smithing by component names.

Monsters:

- `MonsterGenerator` can consume `PermaLists.MonsterCreationData`.
- Current content path appears incomplete because the loader is not in the central list and source JSON is missing.
- Several undead/special monster creation methods are hardcoded in `MonsterGenerator`.

## Modularity assessment

The current approach is partially modular for developer-authored data, but not yet modular enough for user-added content.

What works:

- Many major content types already have serializable data models.
- Most generators consume definitions from `PermaLists` rather than fully hardcoding content.
- Split item and dialogue files show the beginning of a modular content-folder approach.
- Newtonsoft JSON supports dictionaries and properties, which is useful for moddable schemas.

What blocks real modding:

- Files are hardcoded by name. Adding a new item JSON file will do nothing unless `ItemDataLoader` is edited.
- Dialogue files are hardcoded by role. Adding a new role dialogue file will do nothing unless `DialogueDataLoader` is edited.
- Enums limit user-added types. New races, jobs, terrain types, village types, item types, material types, and roles cannot be freely added if systems require enum values.
- There is no manifest, content pack discovery, schema versioning, or load priority.
- There is no duplicate policy.
- There is no stable validation report for modders.
- Public mutable `PermaLists` makes accidental mutation likely.
- WebGL/browser support is not compatible with current synchronous file reads from `StreamingAssets`.

Bottom line: the project is moving in the right direction, but the current pipeline is closer to "JSON-configured game data" than "modding architecture."

## WebGL/StreamingAssets concerns

This is a serious platform risk.

Most loaders use:

- `File.Exists(path)`
- `File.ReadAllText(path)`
- `File.ReadAllTextAsync(path)`

That is fine in many desktop/editor contexts, but Unity WebGL `StreamingAssets` are served over HTTP-like URLs and generally need `UnityWebRequest`. Direct `System.IO.File` access to `Application.streamingAssetsPath` will not work reliably in WebGL/browser builds.

If WebGL is a target, content loading should be abstracted behind a platform-aware file provider:

- desktop/editor: `File.ReadAllText`
- WebGL: `UnityWebRequest.Get`
- future mod folders: explicit external directory provider where supported

## Definite problems found

- `MaterialDataLoader.LoadMaterialCreationDataFromJson()` is `async void`, so `DataLoaderManager` cannot wait for it.
- `NPCDataLoader.Start()` calls `LoadData()` even though the loader is also intended for `DataLoaderManager`.
- `DungeonDataLoader` does not implement `IDataLoader`, self-starts, and currently loads nothing.
- `MonsterDataLoader` exists and implements `IDataLoader`, but `MonsterCreationData.json` is missing and the loader does not appear in the central manager list.
- `LootDataLoader` exists and implements `IDataLoader`, but `LootCreationData.json` and `EntityLootData.json` are missing and the loader does not appear in the central manager list.
- `SmithingRecipeDataLoader` exists, `SmithingRecipeData.json` exists, and consumers use `PermaLists.SmithingRecipeList`, but the loader does not appear in the central manager list.
- `DialogueDataLoader` loads `DialogueScriptsWarior.json` while `DialogueScriptsWarrior.json` also exists.
- `DialogueScripts.json` is present but invalid JSON and apparently unused.
- `ItemCreationData.json` is present but invalid JSON and apparently unused.
- `EventDataLoader` and `LandmarkDataLoader` use `JsonUtility` with models that use properties and dictionaries, which is not appropriate for reliable deserialization.
- Loader success logs can be misleading because many loaders handle errors internally without signaling failure to the manager.
- `IntegrityChecker` mutates loaded data, including removing missing loadout items and defaulting body types.
- Static content definitions and runtime state are mixed together in `PermaLists`.

## Possible problems requiring Unity Play Mode validation

- Whether `GameManager` always calls `DataLoaderManager.LoadAllData()` before any generator runs.
- Whether `NPCDataLoader.Start()` causes duplicate or premature load behavior in the active scene.
- Whether `MaterialDataLoader` completes before `ItemFactory` first needs materials.
- Whether `EventCreationData` and `LandmarkCreationData` are actually populated in Play Mode despite `JsonUtility`.
- Whether smithing works in the current scene, given the apparent missing loader.
- Whether monsters ever load from JSON, given the missing JSON file and manager omission.
- Whether loot generation is reachable in gameplay and whether missing loot data causes null drops only or broader failures.
- Whether the misspelled warrior dialogue file is intentional and which warrior dialogue file is actually used.
- Whether duplicate item names or body part names already exist in JSON.
- Whether any `PermaLists` singleton initialization order issue occurs when generated singleton objects are created dynamically.

## Recommendations

Low-risk improvements:

- Remove self-loading from `NPCDataLoader.Start()` and any other data loaders that should be manager-controlled.
- Add `SmithingRecipeDataLoader`, `MonsterDataLoader`, and `LootDataLoader` to the manager only after confirming the required source files exist.
- Rename or archive unused/invalid `ItemCreationData.json` and `DialogueScripts.json`, or make the loaders read them intentionally after fixing syntax.
- Standardize all content loaders on Newtonsoft unless a model is explicitly Unity-serializer-compatible.
- Replace `async void` material loading with synchronous loading for now, or make `IDataLoader` support coroutine/task completion.
- Add count-based success logs: file path, records loaded, destination list count.
- Add null checks after every deserialization before assigning into `PermaLists`.
- Add duplicate-name checks for items, races, materials, body parts, personalities, dialogue roles, recipes, and village types.
- Make missing required files fail the start gate instead of just logging.

Medium-risk refactors:

- Replace `IDataLoader.LoadData()` with a result-returning API, for example `LoadResult LoadData()`.
- Move loader order from inspector list to code/config with explicit dependencies.
- Add a `ContentDatabase` or `GameDataRegistry` for immutable loaded definitions, separate from runtime world state.
- Convert `PermaLists` definition collections to read-only surfaces after load.
- Add a validation pass that reports all content errors before starting the game.
- Support folder-based discovery for content categories, for example all `StreamingAssets/Items/*.json`.
- Introduce stable string IDs separate from display names.
- Add schema version fields to major JSON files.

High-risk architectural changes to avoid for now:

- Do not replace all enums with fully dynamic string taxonomies in one pass. That would touch too much gameplay code at once.
- Do not remove `PermaLists` outright yet. Too many systems depend on it.
- Do not build a full mod manager UI before the loading, validation, and ID policy are stable.
- Do not migrate everything to Addressables or ScriptableObjects as a first move. That may help packaging, but it does not solve schema validation, dependency order, or mod compatibility by itself.
- Do not rewrite all generators while loader correctness is still uncertain.

## Suggested next steps

1. Decide which JSON files are canonical and remove or quarantine stale invalid files.
2. Fix the central loader list so every intended dataset is loaded exactly once.
3. Make all loaders manager-owned; remove `Start()`-triggered content loading.
4. Make loader completion synchronous or explicitly awaitable.
5. Add a single validation report before `GameManager.StartGame()`.
6. Add duplicate ID/name checks and required-file checks.
7. Separate static definitions from runtime state inside or alongside `PermaLists`.
8. Add folder-based discovery for item and dialogue files once validation is reliable.
9. Add a WebGL-compatible content file reader if browser builds are a real target.

## If I were improving this next

1. Fix the manager wiring and self-starting loaders first. That is the safest, highest-value change.
2. Fix or quarantine invalid/orphan JSON files so the data directory stops lying about what is active.
3. Replace `MaterialDataLoader`'s `async void` path so the start gate is trustworthy.
4. Add `LoadResult` and make `DataLoaderManager` refuse to start the game if required content fails.
5. Standardize JSON deserialization on Newtonsoft for these data models.
6. Add duplicate and cross-reference validation for items, loadouts, recipes, races, body types, dialogue roles, and materials.
7. Split immutable loaded definitions from runtime state, while keeping `PermaLists` as a compatibility facade during migration.
8. Add folder discovery and simple content-pack manifests only after the base pipeline is deterministic and validated.
