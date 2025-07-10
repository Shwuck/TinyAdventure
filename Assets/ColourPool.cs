using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public static class ColourPool
{
    public static readonly IReadOnlyDictionary<string, string> AllColours = new Dictionary<string, string>
    {
        { "Blue", "#0000FF" },
        { "Brown", "#A52A2A" },
        { "Cyan", "#00FFFF" },
        { "Crimson", "#DC143C" },
        { "Dark Blue", "#00008B" },
        { "Dark Green", "#006400" },
        { "Dark Red", "#8B0000" },
        { "Gold", "#FFD700" },
        { "Green", "#00FF00" },
        { "Grey", "#808080" },
        { "Indigo", "#4B0082" },
        { "Lime", "#00FF00" },
        { "Magenta", "#FF00FF" },
        { "Maroon", "#800000" },
        { "Navy", "#000080" },
        { "Olive", "#808000" },
        { "Orange", "#FFA500" },
        { "Pink", "#FFC0CB" },
        { "Purple", "#800080" },
        { "Red", "#FF0000" },
        { "Silver", "#C0C0C0" },
        { "Sky Blue", "#87CEEB" },
        { "Slate Gray", "#708090" },
        { "Teal", "#008080" },
        { "Turquoise", "#40E0D0" },
        { "Violet", "#8A2BE2" },
        { "Yellow", "#FFFF00" },
        { "Aquamarine", "#7FFFD4" },   // New colour
        { "Beige", "#F5F5DC" },        // New colour
        { "Coral", "#FF7F50" },        // New colour
        { "Khaki", "#F0E68C" },        // New colour
        { "Lavender", "#E6E6FA" },     // New colour
        { "Peach", "#FFDAB9" },        // New colour
        { "Periwinkle", "#CCCCFF" },   // New colour
        { "Salmon", "#FA8072" },       // New colour
        { "Black", "#000000" },
        { "White", "#FFFFFF" }
    };

    public static bool IsValidHexColour(string hex)
    {
        return Regex.IsMatch(hex, "^#([0-9A-Fa-f]{6})$");
    }
}
