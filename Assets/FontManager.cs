using UnityEngine;
using TMPro; // Ensure TextMeshPro namespace is included

public class FontManager : MonoBehaviour
{
    public TMP_FontAsset fontAsset; // Assign the font you want to use in the Inspector

    // Function to apply the font style to all TextMeshPro components
    public void ApplyFontStyleToAllTextMeshPro()
    {
        // Find all TextMeshPro components in the scene
        TextMeshPro[] textMeshProComponents = Resources.FindObjectsOfTypeAll<TextMeshPro>();

        foreach (TextMeshPro tmp in textMeshProComponents)
        {
            tmp.font = fontAsset;
            // Editor-specific code
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tmp); // Mark the TMP component as dirty in the editor so changes are saved
#endif
        }

        // Also apply to TextMeshProUGUI components
        TextMeshProUGUI[] textMeshProUGUIComponents = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();

        foreach (TextMeshProUGUI tmpUgui in textMeshProUGUIComponents)
        {
            tmpUgui.font = fontAsset;
            // Editor-specific code
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tmpUgui); // Mark the TMP component as dirty in the editor so changes are saved
#endif
        }

        Debug.Log("Font style applied to all TextMeshPro components in the scene.");
    }
}
