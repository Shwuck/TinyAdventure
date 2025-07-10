using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class MapEditorPanel : MonoBehaviour
{
    public TMP_InputField gameSeedInput;
    public TMP_Dropdown forestDensityDropdown;
    public TMP_Dropdown forestSizeDropdown;
    public TMP_Dropdown swampDensityDropdown;
    public TMP_Dropdown swampSizeDropdown;
    public TMP_Dropdown climateDropdown;
    public TMP_Dropdown mountainousnessDropdown;
    public TMP_Dropdown waterLevelDropdown;
    public TMP_Dropdown civilisationCountDropdown;
    public TMP_Dropdown raceCountDropdown;
    public Button generateMapButton;
    public MapPreviewUI mapPreviewUI;

    void Start()
    {
        // Populate the climate dropdown with enum names
        climateDropdown.ClearOptions();
        climateDropdown.AddOptions(System.Enum.GetNames(typeof(Climate)).ToList());

        // Populate the density dropdowns with enum names
        forestDensityDropdown.ClearOptions();
        forestDensityDropdown.AddOptions(System.Enum.GetNames(typeof(Density)).ToList());

        swampDensityDropdown.ClearOptions();
        swampDensityDropdown.AddOptions(System.Enum.GetNames(typeof(Density)).ToList());

        // Populate the size dropdowns with enum names
        forestSizeDropdown.ClearOptions();
        forestSizeDropdown.AddOptions(System.Enum.GetNames(typeof(Size)).ToList());

        swampSizeDropdown.ClearOptions();
        swampSizeDropdown.AddOptions(System.Enum.GetNames(typeof(Size)).ToList());

        // Populate the mountainousness and water level dropdowns
        mountainousnessDropdown.ClearOptions();
        mountainousnessDropdown.AddOptions(System.Enum.GetNames(typeof(TerrainMountainousness)).ToList());

        waterLevelDropdown.ClearOptions();
        waterLevelDropdown.AddOptions(System.Enum.GetNames(typeof(TerrainWaterLevel)).ToList());

        // Populate the civilisation count and race count dropdowns with numbers 0 to 10
        civilisationCountDropdown.ClearOptions();
        civilisationCountDropdown.AddOptions(Enumerable.Range(0, 11).Select(n => n.ToString()).ToList());

        raceCountDropdown.ClearOptions();
        raceCountDropdown.AddOptions(Enumerable.Range(0, 11).Select(n => n.ToString()).ToList());

        // Set initial values
        gameSeedInput.text = GameManager.Instance.GameSeed.ToString();
        climateDropdown.value = (int)GameManager.Instance.climate;
        forestDensityDropdown.value = (int)GameManager.Instance.forestDensity;
        forestSizeDropdown.value = (int)GameManager.Instance.forestSize;
        swampDensityDropdown.value = (int)GameManager.Instance.swampDensity;
        swampSizeDropdown.value = (int)GameManager.Instance.swampSize;
        mountainousnessDropdown.value = (int)GameManager.Instance.mountainousness;
        waterLevelDropdown.value = (int)GameManager.Instance.waterLevel;
        civilisationCountDropdown.value = GameManager.Instance.CivilisationCount;
        raceCountDropdown.value = GameManager.Instance.RaceCount;

        // Add listeners
        gameSeedInput.onEndEdit.AddListener(UpdateGameSeed);
        climateDropdown.onValueChanged.AddListener(UpdateClimate);
        forestDensityDropdown.onValueChanged.AddListener(UpdateForestDensity);
        forestSizeDropdown.onValueChanged.AddListener(UpdateForestSize);
        swampDensityDropdown.onValueChanged.AddListener(UpdateSwampDensity);
        swampSizeDropdown.onValueChanged.AddListener(UpdateSwampSize);
        mountainousnessDropdown.onValueChanged.AddListener(UpdateMountainousness);
        waterLevelDropdown.onValueChanged.AddListener(UpdateWaterLevel);
        civilisationCountDropdown.onValueChanged.AddListener(UpdateCivilisationCount);
        raceCountDropdown.onValueChanged.AddListener(UpdateRaceCount);
        generateMapButton.onClick.AddListener(GenerateMapPreview);
    }

    void UpdateGameSeed(string seed)
    {
        if (int.TryParse(seed, out int newSeed))
        {
            GameManager.Instance.GameSeed = newSeed;
        }
    }

    void UpdateClimate(int index)
    {
        GameManager.Instance.climate = (Climate)index;
    }

    void UpdateForestDensity(int index)
    {
        GameManager.Instance.forestDensity = (Density)index;
    }

    void UpdateForestSize(int index)
    {
        GameManager.Instance.forestSize = (Size)index;
    }

    void UpdateSwampDensity(int index)
    {
        GameManager.Instance.swampDensity = (Density)index;
    }

    void UpdateSwampSize(int index)
    {
        GameManager.Instance.swampSize = (Size)index;
    }

    void UpdateMountainousness(int index)
    {
        GameManager.Instance.mountainousness = (TerrainMountainousness)index;
    }

    void UpdateWaterLevel(int index)
    {
        GameManager.Instance.waterLevel = (TerrainWaterLevel)index;
    }

    void UpdateCivilisationCount(int index)
    {
        GameManager.Instance.CivilisationCount = index;
    }

    void UpdateRaceCount(int index)
    {
        GameManager.Instance.RaceCount = index;
    }

    void GenerateMapPreview()
    {
        mapPreviewUI.UpdateMapPreview();
        GameManager.Instance.MapSet = true;
    }
}
