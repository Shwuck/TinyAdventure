using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MonsterManager : MonoBehaviour
{
    public static MonsterManager Instance { get; private set; }

    private List<Monster> allMonsters = new List<Monster>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaceMonstersForNestedArea(INestedArea nestedArea)
    {
        if (nestedArea.GeneratedMonsters == null || nestedArea.GeneratedMonsters.Count == 0)
        {
            Debug.Log("No monsters to place in this nested area.");
            return;
        }

        int maxMonstersToPlace = Mathf.Min(nestedArea.MaxMonstersToPlace, nestedArea.GeneratedMonsters.Count);
        Debug.Log($"Placing up to {maxMonstersToPlace} monsters in the nested area.");

        nestedArea.GeneratedMonsters = nestedArea.GeneratedMonsters.OrderBy(m => UnityEngine.Random.value).ToList();

        int placedMonsters = 0;
        foreach (var monster in nestedArea.GeneratedMonsters.ToList())
        {
            if (placedMonsters >= maxMonstersToPlace)
            {
                Debug.Log($"Reached MaxMonstersToPlace limit: {maxMonstersToPlace}");
                break;
            }

            PlaceMonster(nestedArea, monster);
            placedMonsters++;
        }

        Debug.Log($"{placedMonsters} monsters have been placed in the nested area.");
    }

    public void PlaceMonster(INestedArea nestedArea, Monster monster)
    {
        Debug.Log($"Placing Monster - '{monster.Name}' in nested area.");
        Vector2Int monsterPosition = DetermineMonsterPositionInNestedArea(nestedArea);

        int attempts = 0;
        while (!nestedArea.IsValidPosition(monsterPosition) || !nestedArea.IsPassable(monsterPosition) || HasCollision(nestedArea, monsterPosition))
        {
            monsterPosition = AdjustMonsterPosition(nestedArea, monsterPosition);
            attempts++;
            if (attempts > 5)
            {
                Debug.LogError($"Failed to place '{monster.Name}' after {attempts} attempts.");
                return;
            }
        }

        nestedArea.UpdateCharacterPosition(monster, monsterPosition);
        monster.NestedMapPosition = monsterPosition;
        monster.IsInNestedArea = true;
        monster.CurrentNestedArea = nestedArea;
        monster.CanLeaveArea = false;
        monster.IsActive = true;
        monster.Faction = "Undead";
        monster.MonsterLevel = nestedArea.DangerLevel;
        Debug.Log($"'{monster.Name}' placed at {monsterPosition} within nested area.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "MonsterManager.PlaceMonster placed monster in nested area", $"NestedArea: {nestedArea?.Name} ({nestedArea?.NestedAreaID})", monster);

        if (!TurnOrchestrator.Instance.IsCharacterRegistered(monster))
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogEvent("[REGISTRATION]", "MonsterManager.PlaceMonster before TurnOrchestrator.RegisterCharacter", null, monster);
            TurnOrchestrator.Instance.RegisterCharacter(monster);
            Debug.Log($"Registering Monster '{monster.Name}' with TurnManager.");
        }
        else
        {
            Debug.Log($"Monster '{monster.Name}' is already registered with the TurnManager.");
        }
    }

    public void RemoveMonsterFromNestedArea(Monster monster)
    {
        if (monster.IsInNestedArea && monster.CurrentNestedArea != null)
        {
            INestedArea nestedArea = monster.CurrentNestedArea;
            Cell[,] nestedMap = nestedArea.GetNestedMap();

            if (monster.NestedMapPosition.x >= 0 && monster.NestedMapPosition.x < nestedMap.GetLength(0) &&
                monster.NestedMapPosition.y >= 0 && monster.NestedMapPosition.y < nestedMap.GetLength(1))
            {
                Cell cell = nestedMap[monster.NestedMapPosition.x, monster.NestedMapPosition.y];
                cell.isNPCPresent = false;
                cell.isPassable = true;
                cell.Objects.Remove(monster);
                Debug.Log($"Monster '{monster.Name}' removed from nested area at position {monster.NestedMapPosition}");
            }
            else
            {
                Debug.LogWarning($"Monster '{monster.Name}' position ({monster.NestedMapPosition}) is outside the bounds of the nested area.");
            }

            monster.IsInNestedArea = false;
            monster.CurrentNestedArea = null;
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogEvent("[ENTITY REMOVAL]", "MonsterManager.RemoveMonsterFromNestedArea before TurnOrchestrator.DeregisterCharacter", null, monster);
            TurnOrchestrator.Instance.DeregisterCharacter(monster);
            Debug.Log($"Monster '{monster.Name}' deregistered from turn manager.");

            nestedArea.GeneratedMonsters.Remove(monster);
        }
        else
        {
            Debug.LogWarning($"Monster '{monster.Name}' is not in a nested area.");
        }
    }

    public Monster GetMonsterByID(int monsterID)
    {
        return PermaLists.Instance.AllMonsters.FirstOrDefault(monster => monster.MonsterID == monsterID);
    }

    private Vector2Int DetermineMonsterPositionInNestedArea(INestedArea nestedArea)
    {
        Cell[,] nestedMap = nestedArea.GetNestedMap();
        int width = nestedMap.GetLength(0);
        int height = nestedMap.GetLength(1);

        Vector2Int position;
        do
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            position = new Vector2Int(x, y);
        } while (!nestedArea.IsValidPosition(position) || !nestedArea.IsPassable(position) || HasCollision(nestedArea, position));

        return position;
    }

    private Vector2Int AdjustMonsterPosition(INestedArea nestedArea, Vector2Int monsterPosition)
    {
        int width = nestedArea.GetNestedMap().GetLength(0);
        int height = nestedArea.GetNestedMap().GetLength(1);

        int offsetX = Random.Range(-1, 2);
        int offsetY = Random.Range(-1, 2);
        Vector2Int adjustedPosition = monsterPosition + new Vector2Int(offsetX, offsetY);

        adjustedPosition.x = Mathf.Clamp(adjustedPosition.x, 0, width - 1);
        adjustedPosition.y = Mathf.Clamp(adjustedPosition.y, 0, height - 1);

        return adjustedPosition;
    }

    public bool HasCollision(INestedArea nestedArea, Vector2Int position)
    {
        Cell cell = nestedArea.GetCellAtPosition(position);
        return cell != null && cell.Objects.Any(obj => obj is Monster);
    }
}
