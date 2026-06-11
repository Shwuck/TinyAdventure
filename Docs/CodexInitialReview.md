# Codex Initial Technical Review

Date: 2026-06-11

Scope: read-only repository review. No code, settings, packages, scenes, prefabs, assets, or JSON data were changed during the review.

## 1. Unity Version

Unity version from `ProjectSettings/ProjectVersion.txt`:

- `2021.3.25f1`
- Revision: `2021.3.25f1 (68ef2c4f8861)`

## 2. Enabled Build Scenes

From `ProjectSettings/EditorBuildSettings.asset`:

- Enabled: `Assets/Scenes/SampleScene.unity`

Additional scene present but not enabled in build settings:

- `Assets/Scenes/TitleScreen.unity`

Assumption: `SampleScene.unity` is currently the main playable/development scene because it is the only enabled build scene and is much larger than `TitleScreen.unity`.

## 3. Main Project Folders

The repository is a compact Unity project with most gameplay scripts under `Assets`.

- `Assets/`: many root-level gameplay, manager, and UI scripts.
- `Assets/Actions/`: action manager.
- `Assets/DataLoaders/`: JSON loaders for items, NPCs, animals, dialogue, loot, villages, anatomy, materials, recipes, landmarks, and events.
- `Assets/Editor/`: editor utility shortcuts.
- `Assets/Generators/`: world, map, item, NPC, animal, dungeon, cave, camp, terrain, road, river, forest, swamp, civilisation, and village generation.
- `Assets/Inventories/`: base inventory and character inventory.
- `Assets/NestedAreas/`: nested-area types such as villages, dungeons, caves, forests, deserts, camps, swamps, tundra, mountains, sand, salt flats, underground, and generic land.
- `Assets/Objects/`: domain model for characters, NPCs, animals, monsters, items, plants, interactables, item actions, and interaction definitions.
- `Assets/Plugins/Demigiant/DOTween/`: DOTween plugin files.
- `Assets/Resources/`: includes `DOTweenSettings.asset`.
- `Assets/Scenes/`: Unity scenes.
- `Assets/Shaders/`: shader-related assets.
- `Assets/Sounds/`: audio assets.
- `Assets/StreamingAssets/`: JSON game data.
- `Assets/TextMesh Pro/`: TextMeshPro resources.
- `Assets/UI/`: map, inventory, and map generation UI.
- `Assets/UI Panels/`: trade, donation, and container panel UI.

Important visible assets:

- Scenes: `SampleScene.unity`, `TitleScreen.unity`.
- Prefabs: `Button.prefab`, `ResponseWithButton.prefab`, `ResponseWithoutButton.prefab`.
- ScriptableObject type: `RetroVisualSettings.cs` defines `RetroVisualSettings`.
- Data: many JSON files under `StreamingAssets`, including item, NPC, animal, dialogue, crafting, smithing, village, material, anatomy, loot, event, landmark, and changelog data.

## 4. Important Packages

From `Packages/manifest.json`:

- `com.unity.render-pipelines.universal`: `12.1.11`
- `com.unity.textmeshpro`: `3.0.6`
- `com.unity.nuget.newtonsoft-json`: `3.2.1`
- `com.unity.ugui`: `1.0.0`
- `com.unity.visualscripting`: `1.8.0`
- `com.unity.timeline`: `1.6.5`
- `com.unity.test-framework`: `1.1.31`
- IDE packages: Rider, Visual Studio, VS Code.
- 2D feature package and standard Unity modules.

DOTween appears to be included directly under `Assets/Plugins/Demigiant/DOTween`, not via Package Manager.

## 5. Main Scripts And Responsibilities

### Core Entry And Global State

- `GameManager.cs`: central singleton. Owns game seed, global counters, start state, map settings, debug flags, and startup flow. Starts `DataLoaderManager.LoadAllData()`.
- `PermaLists.cs`: global singleton data registry. Holds map cells, nested areas, characters, NPCs, animals, monsters, items, recipes, races, dialogue, factions, terrain counts, and other runtime data.
- `DataLoaderManager.cs`: inspector-driven loader runner. Loads `IDataLoader` implementations in sequence, then calls `IntegrityChecker`.
- `IntegrityChecker.cs`: validates some loaded data.
- `GameDebugger.cs`: central logging helper.
- `CallTrace.cs`: optional call tracing/debug reporting.

### Player And Turn Flow

- `PlayerController.cs`: very large controller for keyboard input, movement, map and nested-area transitions, facing, action menu generation, player actions, UI updates, and turn progress.
- `PlayerStats.cs`: singleton for current player state, location, current cell/nested area, current character, stats, combat values, hunger, money, AP/MP, death, and visibility.
- `PlayerCharacter.cs`: player character model and factory logic.
- `PlayerCharacterGenerator.cs`: player character generation helper.
- `TurnOrchestrator.cs`: active turn coordinator. Switches between `MainMap`, `Exploration`, and `Combat`, and registers/deregisters characters into active turn managers.
- `BaseTurnManager.cs`: shared turn-cycle implementation for registration, speed sorting, player/NPC turn dispatch, cycle auditing, and cycle-end hooks.
- `CombatTurnManager.cs`: combat-specific turn behavior, AP/MP reset, hostile delay, and turn UI updates.
- `ExplorationTurnManager.cs`: exploration turn behavior with immediate turns and continuous cycle restart.
- `TurnManager.cs`: legacy/archived turn manager wrapped in `#if false`; not compiled as-is.

### World And Map Generation

- `MapGenerator.cs`: central map generator and `Cell` definition. Handles terrain, noise, regions, start cell, fog, danger, nested-area list, and cell access.
- `NestedAreaGenerator.cs`: creates nested areas from map cells and nested-area entrances.
- `BaseNestedArea.cs`: large base implementation for nested maps, cell/object/NPC/animal placement, and nested-area state.
- Specific nested areas: `VillageNestedArea`, `DungeonNestedArea`, `CaveNestedArea`, `ForestNestedArea`, `DesertNestedArea`, `CampNestedArea`, `SwampNestedArea`, `TundraNestedArea`, `MountainNestedArea`, `SandNestedArea`, `SaltFlatsNestedArea`, `UndergroundNestedArea`, `LandNestedArea`.
- Terrain and content generators: `ForestGenerator`, `SwampGenerator`, `MountainGenerator`, `RiverGenerator`, `RoadGenerator`, `DesertGenerator`, `NuanceGenerator`, `TerrainTypeGenerator`, `TerrainPainterTool`, `CivilisationGenerator`, `VillageGenerator`, `CampGenerator`, `DungeonGenerator`, `CaveGenerator`, `LandmarkGenerator`, `GraveSiteGenerator`.

### Characters, Objects, Items, And Interactions

- `Character.cs`: large base character model and behavior.
- `NPC.cs`, `Animal.cs`, `Monster.cs`: concrete character types.
- `NPCGenerator.cs`, `AnimalGenerator.cs`, `MonsterGenerator.cs`: entity generation.
- `IInteractions.cs`: very large interaction/action file. Includes inspect, talk, trade, gather, chop, mine, containers, dungeon/cave entry, combat, construction, farming, fishing, and other actions.
- `Items.cs`: item model and item-related interfaces.
- `Item Actions.cs`: item interaction actions such as consume, equip, unequip, activate, deactivate, drop, and deseed.
- `Objects.cs`: interactable world objects, containers, blocks, doors, walls, campfires, etc.
- `Plants.cs`: plant lifecycle/object types.
- `Inventories/Inventory.cs`, `Inventories/CharacterInventory.cs`, `ContainerInventory.cs`, `PlayerInventory.cs`: inventory systems.

### UI

- `UIController.cs`: central UI singleton. Manages panels, colors, map updates, inventory, trade/dialogue/donation/container panels, character panel, splash, greyout, death panel, and turn order.
- Panel scripts: `MainMenuPanelUI`, `CharacterCreationUI`, `CustomGameSetupPanelUI`, `CraftingPanelUI`, `CookingPanelUI`, `SmithingPanelUI`, `DialoguePanelUI`, `TradePanelUI`, `DonationPanelUI`, `ContainerPanelUI`, `VillageInfoPanel`, `HintPanelUI`, `DeathPanelUI`, `MultipurposePopupPanelUI`.
- Map/inventory UI: `MapDisplayUI`, `MapGeneratorUI`, `MapPreviewUI`, `InventoryUI`.
- Visual/audio: `PostFXOrchestrator`, `CRTFeature`, `RetroVisualSettings`, `UIEffects`, `UIJitter`, `AudioController`, `FontManager`.

### Data And Config

- JSON loaders populate `PermaLists.Instance`.
- `DataLoaderManager.dataLoaders` is assigned in the inspector and likely order-sensitive.
- Some loaders use Newtonsoft JSON; some use Unity `JsonUtility`.

## 6. Dependency Map

High-level runtime shape:

```text
SampleScene
  -> GameManager
      -> DataLoaderManager
          -> IDataLoader implementations
              -> PermaLists
          -> IntegrityChecker
  -> UIController
      -> many UI panels
      -> PlayerStats / PlayerController / TurnOrchestrator / TimeManager
  -> PlayerController
      -> PlayerStats
      -> MapGenerator
      -> NestedAreaGenerator
      -> AreaEntryCoordinator
      -> UIController
      -> interaction classes
  -> MapGenerator
      -> GameManager settings
      -> PermaLists
      -> terrain/road/river/desert/etc generators
  -> NestedAreaGenerator / BaseNestedArea
      -> object/NPC/animal/monster generators
      -> PermaLists
  -> TurnOrchestrator
      -> CombatTurnManager
      -> ExplorationTurnManager
      -> PlayerStats
  -> Characters/interactions
      -> UIController
      -> PlayerStats
      -> PermaLists
      -> managers/generators
```

Main coupling patterns:

- Heavy singleton usage across most systems.
- Many `DontDestroyOnLoad` managers.
- Broad public fields and inspector wiring.
- Shared mutable global state through `PermaLists`.
- UI calls from gameplay/action classes.
- Data-loader order is hidden in scene inspector configuration.

## 7. Most Central Or Risky Scripts

Most central/risky scripts to change:

- `PermaLists.cs`: global runtime registry. Breakage affects almost every system.
- `PlayerController.cs`: movement, input, transitions, interactions, UI updates, and turn progress.
- `MapGenerator.cs`: core map data model and generation.
- `BaseNestedArea.cs`: shared nested-area logic used by many area types.
- `IInteractions.cs`: many player/world/combat actions in one large file.
- `UIController.cs`: central panel and UI coordinator with many references.
- `GameManager.cs`: startup, counters, settings, seed, and global state.
- `BaseTurnManager.cs`, `TurnOrchestrator.cs`, `CombatTurnManager.cs`, `ExplorationTurnManager.cs`: active turn behavior.

## 8. Definite Problems Found

- `TitleScreen.unity` exists but is not enabled in build settings. Current build starts with `SampleScene.unity`.
- `TurnManager.cs` is dead/archived code under `#if false`. It is not currently a compile issue, but it is confusing and appears in searches.
- `NewBehaviourScript1.cs` and `NewBehaviourScript2.cs` are empty template scripts and are cleanup candidates if no scene or prefab references exist.
- Several classes are very large, increasing risk and reducing readability:
  - `IInteractions.cs`: 2095 lines.
  - `MapGenerator.cs`: 2004 lines.
  - `BaseNestedArea.cs`: 1909 lines.
  - `Character.cs`: 1872 lines.
  - `PlayerController.cs`: 1467 lines.
  - `Objects.cs`: 1464 lines.
  - `UIController.cs`: 1090 lines.
- Heavy singleton and global state usage is a definite maintainability risk.
- `DataLoaderManager` relies on manual inspector assignment/order for loaders.
- There are two similar dialogue files: `DialogueScriptsWarior.json` and `DialogueScriptsWarrior.json`. This may be intentional, but it is suspicious.
- DOTween is included directly under `Assets/Plugins`; package/plugin provenance should be documented before any future upgrade.

Mitigated finding:

- There are two `CharacterTurnData` declarations in source, but the legacy one is inside `TurnManager.cs` under `#if false`, so it should not compile or conflict as-is.
- `FontManager.cs` references `UnityEditor.EditorUtility`, but those calls are wrapped in `#if UNITY_EDITOR`, so that part is build-safe.

## 9. Possible Concerns Requiring Unity Validation

These require opening Unity or running a compile/play-mode test:

- Missing inspector references on `GameManager`, `DataLoaderManager`, `UIController`, `TurnOrchestrator`, `MapGenerator`, `PostFXOrchestrator`, and panel scripts.
- Startup order issues between singleton `Awake`/`Start` methods.
- Stale references or duplicate singleton behavior from many `DontDestroyOnLoad` managers.
- URP renderer-feature wiring for `CRTFeature` and `PostFXOrchestrator`.
- Whether a `RetroVisualSettings` asset exists and is assigned correctly.
- JSON schema drift or loader ordering issues.
- Whether all modern C# syntax used by scripts compiles cleanly under this Unity installation.
- Whether `TitleScreen.unity` is intentionally excluded or simply forgotten.

## 10. Refactoring And Streamlining Opportunities

Practical, low-drama improvements:

- Expand `README.md` with Unity version, first scene, package expectations, startup flow, and how to run the project.
- Document `DataLoaderManager` load order before changing loader code.
- Remove or archive confirmed-unused template/dead scripts after checking scene/prefab references in Unity.
- Add clearer startup diagnostics for missing critical references.
- Gradually organize scripts into folders only when touching related files; avoid a broad move.
- Split the largest files along obvious responsibilities:
  - `IInteractions.cs`: combat, containers, crafting/cooking/smithing, gathering/world actions, travel/nested-area interactions.
  - `PlayerController.cs`: input, movement, nested-area transitions, adaptive action UI.
  - `UIController.cs`: panel routing, visual/theme updates, map UI updates.
  - `MapGenerator.cs`: eventually separate `Cell`/map data from generation steps.
- Reduce direct UI calls from interaction classes over time. Have interactions return results or requests, then let controller/UI code present panels.
- Prefer private serialized fields for new Unity references while avoiding mass changes to existing public fields.
- Keep singletons for now, but reduce new dependencies on them where practical.

## 11. Suggested Safe Plan

1. Baseline open and compile in Unity `2021.3.25f1`.
   - Benefit: establishes real errors before refactoring.
   - Risk: low, if no package upgrades are accepted.
   - Files likely affected: none.
   - Test: open project, let it import, record Console errors/warnings, enter Play Mode in `SampleScene`.

2. Verify scene wiring.
   - Benefit: catches missing inspector references that source review cannot prove.
   - Risk: low.
   - Files likely affected: none unless later approved.
   - Test: inspect `GameManager`, `DataLoaderManager`, `UIController`, `TurnOrchestrator`, `MapGenerator`, and `PostFXOrchestrator`.

3. Document current startup/data flow.
   - Benefit: makes the project understandable before code changes.
   - Risk: low.
   - Files likely affected if approved later: `README.md`.
   - Test: no gameplay test needed; compare docs against actual Play Mode startup.

4. Remove or archive confirmed-unused dead/template scripts.
   - Benefit: reduces search noise and false leads.
   - Risk: low if Unity confirms no scene/prefab references.
   - Files likely affected: `NewBehaviourScript1.cs`, `NewBehaviourScript2.cs`, possibly `TurnManager.cs` later.
   - Test: recompile, open `SampleScene`, enter Play Mode.

5. Stabilize startup null checks and loader diagnostics.
   - Benefit: failures become clear instead of cascading null references.
   - Risk: low-medium because startup order is sensitive.
   - Files likely affected: `GameManager.cs`, `DataLoaderManager.cs`, `IntegrityChecker.cs`, maybe loader scripts.
   - Test: Play Mode startup, verify all JSON loads, map/game setup still works.

6. Clean up data-loader ordering.
   - Benefit: reduces hidden inspector-order dependency.
   - Risk: medium because data dependencies are broad.
   - Files likely affected: `DataLoaderManager.cs`, loader scripts only if needed.
   - Test: confirm races/backgrounds/items/NPCs/dialogue/recipes load; create character; generate map.

7. Isolate player movement and nested-area transition logic.
   - Benefit: makes `PlayerController` safer to work in.
   - Risk: medium-high due to gameplay behavior.
   - Files likely affected: `PlayerController.cs`, maybe `AreaEntryCoordinator.cs`.
   - Test: main map movement, entering/exiting nested areas, dungeon/cave entry, UI updates, turn completion.

8. Split interaction groups gradually.
   - Benefit: improves readability without changing behavior.
   - Risk: medium because many actions live in one file.
   - Files likely affected: `IInteractions.cs`, possibly new interaction files.
   - Test: inspect, talk, trade, gather/chop/mine, open containers, combat actions, construction/farming actions.

## 12. Prioritised Action List

1. Open in Unity `2021.3.25f1` and capture Console errors before changing code.
2. Verify `SampleScene` inspector references for `GameManager`, `DataLoaderManager`, `UIController`, `TurnOrchestrator`, `MapGenerator`, and `PostFXOrchestrator`.
3. Document startup flow and data-loader order in `README.md`.
4. Remove confirmed-unused template/dead scripts only after Unity reference check.
5. Add small defensive diagnostics around startup and loader failures.
6. Refactor only one large area at a time, starting with low-risk extraction from `IInteractions.cs` or `PlayerController.cs` after behavior is understood.

## Follow-Up Triage Note: Missing InteractableGenerator Script

During Unity testing in `Assets/Scenes/SampleScene.unity`, Unity reported a missing script on GameObject `InteractableGenerator`.

- GameObject: `InteractableGenerator`
- Scene object fileID: `885596245`
- Missing MonoBehaviour fileID: `885596246`
- Missing script GUID: `25b18f5dbdb5871459d764e43445a332`
- Current repository check: no matching `.cs.meta` GUID and no current `InteractableGenerator` class found.
- Serialized fields on the missing component: none visible beyond the missing script reference.

Recommendation: do not delete the component yet. First check old backups/source history for a script or `.meta` with GUID `25b18f5dbdb5871459d764e43445a332`. If it cannot be restored, verify whether current systems such as `ObjectPlacementFactory`, `NestedAreaManager`, or nested-area placement code replaced it, then remove the stale scene component only after that confirmation.
