using UnityEngine;

[CreateAssetMenu(fileName = "RetroVisualSettings", menuName = "Game/Retro Visual Settings")]
public class RetroVisualSettings : ScriptableObject
{
    [Header("CRT Full-Screen Pass")]
    public bool crtEnabled = true;
    [Range(1f, 4f)] public float pixelSize = 2f;
    [Range(0f, 1f)] public float scanlineIntensity = 0.2f;
    [Range(0f, 0.5f)] public float curvature = 0.07f;
    [Range(0f, 1f)] public float vignette = 0.2f;
    [Range(0f, 0.1f)] public float noise = 0.015f;

    [Header("Panel Wear / Overlay")]
    public bool panelWearEnabled = true;
    [Range(0f, 1f)] public float panelEdgeDark = 0.22f;
    [Range(0f, 1f)] public float panelCornerVignette = 0.32f;
    [Range(0f, 1f)] public float panelCornerRadius = 0.16f;
    [Range(0f, 1f)] public float overlayOpacity = 0.08f; // your multiply overlay alpha

    [Header("UI Jitter")]
    public bool jitterEnabled = true;
    [Range(0f, 2f)] public float jitterAmpX = 0.6f;
    [Range(0f, 2f)] public float jitterAmpY = 0.4f;
    [Range(0.1f, 10f)] public float jitterSpeed = 2.2f;
    [Range(0f, 0.01f)] public float jitterScaleAmp = 0.003f;
    [Range(0f, 0.1f)] public float flickerAmp = 0.03f;
}
