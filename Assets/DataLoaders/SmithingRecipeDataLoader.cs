using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class SmithingRecipeDataLoader : MonoBehaviour, IDataLoader
{

    public void LoadData()
    {
        LoadSmithingRecipesFromJson();
    }

    private void LoadSmithingRecipesFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "SmithingRecipeData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            List<SmithingRecipe> smithingRecipes = JsonConvert.DeserializeObject<List<SmithingRecipe>>(json);

            // Store the loaded smithing recipes in PermaLists or another appropriate data structure
            PermaLists.Instance.SmithingRecipeList = smithingRecipes;

            Debug.Log("All smithing recipes loaded successfully.");
        }
        else
        {
            Debug.LogWarning("SmithingRecipes.json not found in StreamingAssets!");
        }
    }
}

[System.Serializable]
public class SmithingRecipe
{
    public string BodyComponent { get; set; }
    public string HeadComponent { get; set; }
    public string ResultingWeapon { get; set; }
}



/*
 * 
BodyComponents Definitions:
Chain: "A flexible, segmented body component for weapons designed for high mobility and extended reach."

Grip: "A small, ergonomically designed body component for small, easily manoeuvrable weapons."

Guard: "A body component with a protective guard to shield the user's hand, typically found in weapons designed for close combat."

Handle: "A medium weight, medium length body component suitable for one-handed weapons, providing balance and control."

Hilt: "A medium weight, short length body component typically used for bladed weapons, designed for quick, precise movements."

Pole: "A lightweight, long length body component designed for increased reach and leverage, often used in polearms and staff weapons."

Shaft: "A heavy weight, long length body component, providing greater impact and stability, commonly used in heavy melee weapons."

Staff: "A medium weight, long length body component, often used in martial arts and ceremonial weapons, providing versatility and reach."

HeadComponents Definitions:
Axe Head: "A broad, wedge-shaped component designed for chopping and cleaving, often used in heavy, powerful weapons."

Blade: "A versatile component characterized by a flat, sharp-edged surface, used for cutting and slashing."

Claw Head: "A component featuring curved, claw-like extensions, often used for grasping or rending."

Curved Blade: "A blade with a pronounced curve, designed for sweeping slashes and increased cutting efficiency."

Double Axe Head: "Two opposing axe blades mounted on a single weapon, providing balance and increased chopping power."

Hammer Head: "A heavy, blunt component designed for delivering powerful, crushing blows."

Hook: "A curved component, often sharpened, designed for hooking, pulling, or slashing."

Long Blade: "An elongated blade component designed for extended reach and precise thrusting or slashing."

Mace Head: "A heavy, rounded component with protrusions, designed for delivering impactful, crushing strikes."

Pointed Head: "A sharp, tapering component designed for piercing and penetrating armor."

Rod: "A straight, cylindrical component often used for striking or blocking, common in staff and baton weapons."

Saw Blade: "A circular or serrated blade designed for cutting through tough materials with a sawing motion."

Short Blade: "A compact blade component designed for close-quarters combat, emphasizing speed and manoeuvrability."

Spiked Head: "A component featuring one or more spikes, designed for puncturing and inflicting deep wounds."

Thin Blade: "A narrow, lightweight blade component designed for quick, precise strikes and thrusts."

Forked Head: "A component featuring multiple distinct points, often used in weapons designed for trapping or multiple piercing."

*/