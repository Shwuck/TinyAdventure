using UnityEngine;
using TMPro;

public class CellDebugger : MonoBehaviour
{
    public Vector2Int mainCellCoordinates; // Coordinates of the main cell to debug
    public Vector2Int nestedMapCoordinates; // Coordinates of the nested map to debug
    public TextMeshProUGUI mainCellDebugText; // Reference to the TextMeshPro component for main cell debug information
    public TextMeshProUGUI nestedAreaDebugText; // Reference to the TextMeshPro component for nested area debug information
    public bool showNestedArea = false; // Boolean to control whether to display Cell or Nested Area debug information

    // Reference to the MapGenerator to access the map
    public MapGenerator mapGenerator;

    // Update is called once per frame
    void Update()
    {
        Cell mainCell = mapGenerator.GetCell(mainCellCoordinates);

        if (showNestedArea)
        {
            INestedArea nestedArea = mapGenerator.GetNestedAreaAtCoordinates(nestedMapCoordinates);

            if (nestedArea != null)
            {
                UpdateNestedAreaDebug(nestedArea);
            }
        }
        else if (mainCell != null)
        {
            UpdateMainCellDebug(mainCell);
        }
    }

    private void UpdateMainCellDebug(Cell mainCell)
    {
        string debugInfo = ConstructDebugInfo(mainCell);

        // Update debug text for the main cell
        mainCellDebugText.text = debugInfo;
    }

    private void UpdateNestedAreaDebug(INestedArea nestedArea)
    {
        // Get the cell within the nested map
        Cell cellInNestedMap = nestedArea.GetNestedMap()[mainCellCoordinates.x, mainCellCoordinates.y];

        string debugInfo = ConstructDebugInfo(cellInNestedMap);

        // Update debug text for the nested area
        nestedAreaDebugText.text = debugInfo;
    }

    private string ConstructDebugInfo(Cell cell)
    {
        string debugInfo = $"Coordinates: {cell.Coordinates}\n" +
                           $"Terrain: {cell.Terrain}\n" +
                           $"Adjacent Cell Count: {cell.AdjacentCellCount}\n" +
                           $"Player Present: {cell.isPlayerPresent}\n" +
                           $"Passable: {cell.isPassable}\n" +
                           $"Has Nested Area: {cell.hasNestedArea}\n" +
                           $"Nested Area Visible: {cell.nestedAreaCanBeSeen}\n" +
                           $"NPC Present: {cell.isNPCPresent}\n" +
                           $"NPC Group Present: {cell.isNPCGroupPresent}\n" +
                           $"Fertile: {cell.isFertile}\n" +
                           $"Fertility Value: {cell.FertilityValue}";

        // Append adjacent cell information
        debugInfo += "\nAdjacent Cells:";
        foreach (var direction in cell.AdjacentCells.Keys)
        {
            debugInfo += $"\n\t{direction}: {(cell.AdjacentCells[direction] != null ? cell.AdjacentCells[direction].ToString() : "None")}";
        }

        // Append objects and animals information
        debugInfo += "\nObjects:";
        foreach (var obj in cell.Objects)
        {
            debugInfo += $"\n\t{obj.GetType().Name}";
        }

        debugInfo += "\nAnimals:";
        foreach (var animal in cell.Animals)
        {
            debugInfo += $"\n\t{animal.GetType().Name}";
        }

        return debugInfo;
    }
}
