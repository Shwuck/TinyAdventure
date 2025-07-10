using System.Collections.Generic;
using UnityEngine;

public class FactionManager : MonoBehaviour
{
    public static FactionManager Instance { get; private set; }
    public MapGenerator mapGenerator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (PermaLists.Instance.Factions == null)
            {
                PermaLists.Instance.Factions = new List<Faction>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Faction CreateFaction(RaceData raceData)
    {
        Faction existingFaction = GetFactionForRace(raceData);
        if (existingFaction != null)
        {
            return existingFaction;
        }

        // Generate the faction name
        string factionName = GenerateFactionName(raceData);

        // Create a new faction
        Faction newFaction = new Faction
        {
            FactionName = factionName,
            FactionSymbol = GetFactionSymbol(factionName),
            Race = raceData,
            Villages = new List<Village>(),
            FactionColour = GetRandomFactionColour() // Assign a random colour
        };

        // Add the new faction to the list stored in PermaLists
        PermaLists.Instance.Factions.Add(newFaction);
        Debug.Log($"Created new faction: {newFaction.FactionName} for race {raceData.Name} with colour {newFaction.FactionColour} and symbol {newFaction.FactionSymbol}");
        return newFaction;
    }

    private char GetFactionSymbol(string factionName)
    {
        if (!string.IsNullOrEmpty(factionName))
        {
            return char.ToUpper(factionName[0]); // Get the first letter as the symbol
        }
        return '?'; // Fallback symbol if the name is empty
    }

    private string GetRandomFactionColour()
    {
        var colourKeys = new List<string>(ColourPool.AllColours.Keys);
        int randomIndex = Random.Range(0, colourKeys.Count);
        string selectedColourKey = colourKeys[randomIndex];

        // Return the hex code for the selected colour
        return ColourPool.AllColours[selectedColourKey];
    }

    private string GenerateFactionName(RaceData raceData)
    {
        // Generate a name based on the race data, or choose from a predefined list
        return $"{raceData.Name} Kingdom"; // Example, you can make this more complex
    }

    public Faction GetFactionForRace(RaceData raceData)
    {
        return PermaLists.Instance.Factions?.Find(f => f.Race == raceData);
    }

    public void SpreadInfluenceForAllFactions()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator is not assigned.");
            return;
        }

        foreach (var faction in PermaLists.Instance.Factions)
        {
            foreach (var village in faction.Villages)
            {
                int requiredCells = (village.Stats.Prestige + 1) * 8;

                List<Cell> ownedCells = GetOwnedCells(village);
                if (ownedCells.Count >= requiredCells)
                {
                    continue; // Village already owns enough cells
                }

                int cellsToOwn = requiredCells - ownedCells.Count;

                SpreadInfluenceFromVillage(village, cellsToOwn);
            }
        }
    }

    private List<Cell> GetOwnedCells(Village village)
    {
        List<Cell> ownedCells = new List<Cell>();

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell cell = mapGenerator.map[x, y];
                if (cell.IsOwned && cell.OwnedByFaction == village.Faction && cell.Village == village)
                {
                    ownedCells.Add(cell);
                }
            }
        }

        return ownedCells;
    }

    public void AddVillageToFaction(Faction faction, Village village)
    {
        faction.Villages.Add(village);
        Debug.Log($"Village '{village.VillageName}' added to faction '{faction.FactionName}'");
    }

    private void SpreadInfluenceFromVillage(Village village, int cellsToOwn)
    {
        List<Cell> frontierCells = GetFrontierCells(village);
        int initialCellsToOwn = cellsToOwn;
        int cellsClaimed = 0;

        while (cellsToOwn > 0 && frontierCells.Count > 0)
        {
            Cell cellToClaim = frontierCells[Random.Range(0, frontierCells.Count)];
            cellToClaim.IsOwned = true;
            cellToClaim.OwnedBy = village.VillageName;
            cellToClaim.OwnedByFaction = village.Faction;
            frontierCells.Remove(cellToClaim);
            cellsToOwn--;
            cellsClaimed++;

            // Add newly claimed cell's neighbors to the frontier
            frontierCells.AddRange(GetAdjacentUnownedCells(cellToClaim));
        }

        Debug.Log($"Village '{village.VillageName}' in faction '{village.Faction.FactionName}' spread influence to {cellsClaimed} cells (requested {initialCellsToOwn} cells).");
    }

    private List<Cell> GetFrontierCells(Village village)
    {
        List<Cell> frontierCells = new List<Cell>();

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell cell = mapGenerator.map[x, y];
                if (cell.IsOwned && cell.OwnedBy == village.VillageName) // Check if the cell is owned by this specific village
                {
                    List<Cell> adjacentCells = GetAdjacentUnownedCells(cell);
                    foreach (var adjacentCell in adjacentCells)
                    {
                        if (!frontierCells.Contains(adjacentCell))
                        {
                            frontierCells.Add(adjacentCell);
                        }
                    }
                }
            }
        }

        return frontierCells;
    }


    private List<Cell> GetAdjacentUnownedCells(Cell cell)
    {
        List<Cell> adjacentUnownedCells = new List<Cell>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            Vector2Int adjacentPos = cell.Coordinates + dir;
            if (IsValidPosition(adjacentPos))
            {
                Cell adjacentCell = mapGenerator.map[adjacentPos.x, adjacentPos.y];
                if (!adjacentCell.IsOwned)
                {
                    adjacentUnownedCells.Add(adjacentCell);
                }
            }
        }

        return adjacentUnownedCells;
    }

    private bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < mapGenerator.width && position.y >= 0 && position.y < mapGenerator.height;
    }
}

[System.Serializable]
public class Faction
{
    public string FactionName;
    public char FactionSymbol;
    public string Banner; // The banner or flag representing this faction
    public string FactionColour; // The Colour of the Faction
    public RaceData Race;
    public List<Village> Villages; // All villages under this faction's control
}