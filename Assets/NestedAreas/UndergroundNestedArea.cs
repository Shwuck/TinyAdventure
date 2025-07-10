using UnityEngine;
using System.Collections.Generic;

public class UndergroundNestedArea : BaseNestedArea
{
    public bool HasExit { get; set; }

    // Constructor
    public UndergroundNestedArea()
    {
        EntrancePosition = PlayerStats.Instance.FacingCellCoordinates;
        Initialize();
    }

    // Initialize the nested area map
    public override void Initialize()
    {
        NestedAreaID = GameManager.Instance.GetNestedAreaID();

        AreaMap = new Cell[Size, Size];

        // Initialize all cells as Dirt initially
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID(); // Use base class method
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Dirt)
                {
                    isIndoors = true // Set the IsIndoors property to true for each cell
                };
            }
        }


        // Add a Rope to the entrance cell
        AddRopeToEntrance();

        // Generate random dirt blocks throughout the area
        GenerateDirtBlocks();
    }

    private void AddRopeToEntrance()
    {
        // Assuming Rope is an IInteractable that's been defined elsewhere in your project
        IInteractable rope = new Rope(); // Create a new instance of Rope

        // Retrieve the entrance cell
        Cell entranceCell = GetCellAtPosition(EntrancePosition);

        if (entranceCell != null)
        {
            entranceCell.Objects.Add(rope); // Add the rope to the entrance cell
            HasExit = true;
        }
        else
        {
            Debug.LogError("Entrance cell is null. Cannot add Rope.");
        }
    }

    private void GenerateDirtBlocks()
    {
        int numberOfBlocks = Random.Range(15, 28); // Example: Generate between 15 and 28 Dirt Blocks
        List<Vector2Int> blockPositions = new List<Vector2Int>(); // To keep track of block positions

        for (int i = 0; i < numberOfBlocks; i++)
        {
            Vector2Int position;
            do
            {
                position = new Vector2Int(Random.Range(0, Size), Random.Range(0, Size));
            } while (blockPositions.Contains(position) || position == EntrancePosition); // Ensure unique and not on the entrance

            blockPositions.Add(position);

            DirtBlock block = new DirtBlock
            {
                Position = position,
                NestedMapPosition = position,
                CurrentNestedArea = this,
                IsInNestedArea = true,
                IsActive = true
            };

            AreaMap[position.x, position.y].Objects.Add(block); // Add the block to the cell's objects
        }
    }

    // Override methods from the base class where necessary

    public override bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < Size && position.y >= 0 && position.y < Size;
    }

    public override bool IsPassable(Vector2Int position)
    {
        // Check if the position is passable based on objects in the cell
        Cell cell = GetCellAtPosition(position);
        return cell != null && cell.isPassable;
    }

    public override void UpdatePlayerPosition(Vector2Int newPosition)
    {
        Cell newCellPosition = GetCellAtPosition(newPosition);
        if (newCellPosition != null)
        {
            PlayerStats.Instance.UpdateCurrentCellID(newCellPosition.CellID);
            PlayerStats.Instance.UpdateParentNestedAreaID(newCellPosition.ParentAreaID);
        }
    }

    public override void UpdateCharacterPosition(Character character, Vector2Int newPosition)
    {
        if (IsPassable(newPosition))
        {
            // Remove character from current cell
            Cell currentCell = GetCellAtPosition(character.NestedMapPosition);
            if (currentCell != null)
            {
                currentCell.Objects.Remove(character);
                currentCell.isPassable = true;
            }

            // Update character position
            character.NestedMapPosition = newPosition;
            character.CurrentNestedArea = this;

            // Add character to new cell
            Cell newCell = GetCellAtPosition(newPosition);
            if (newCell != null)
            {
                newCell.Objects.Add(character);
                newCell.isPassable = false;
            }
        }
    }

    public override Cell GetCellAtPosition(Vector2Int position)
    {
        if (position.x >= 0 && position.x < AreaMap.GetLength(0) && position.y >= 0 && position.y < AreaMap.GetLength(1))
        {
            return AreaMap[position.x, position.y];
        }
        return null;
    }

    public override void HandlePlayerExit(MapGenerator mapGenerator)
    {
        // Handle any logic needed when the player exits this underground area
    }
}
