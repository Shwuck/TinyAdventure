using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class PostFXOrchestrator : MonoBehaviour
{
    public static PostFXOrchestrator Instance { get; private set; }

    [Header("Settings Source")]
    public RetroVisualSettings settings;

    [Header("References")]
    public UniversalRendererData rendererData;       // the URP Renderer that hosts your CRTFeature
    public Material crtMaterial;                     // uses Hidden/CRT_Simple
    public string crtFeatureName = "CRTFeature";     // name in the RendererData
    public Graphic[] multiplyOverlays;               // Images using UI/Multiply Overlay (optional)
    public Material[] panelWearMaterials;            // Materials using UI/Panel Wear (optional)
    public UIJitter[] jitters;                       // Jitter components (optional)

    ScriptableRendererFeature crtFeature;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

	void Start()
	{
		if (settings == null)
		{
			Debug.LogWarning("PostFXOrchestrator: RetroVisualSettings is not assigned. Skipping PostFX setup.");
			return;
		}

		if (rendererData != null)
		{
			crtFeature = rendererData.rendererFeatures
				.FirstOrDefault(f => f != null && f.name == crtFeatureName);
		}

		LoadPlayerPrefs(); // load first
		ApplyAll();        // then apply once

		// Keep UIEffects’ master switch in sync
		if (UIEffects.Instance != null)
			UIEffects.Instance.SetEffectsEnabled(settings.jitterEnabled);
	}


    // --- Public API for menu bindings ---
    public void SetCRTenabled(bool on)  { settings.crtEnabled = on; ApplyCRT(); SavePlayerPrefs(); }
    public void SetPixelSize(float v)   { settings.pixelSize = v;  ApplyCRT(); SavePlayerPrefs(); }
    public void SetScanline(float v)    { settings.scanlineIntensity = v; ApplyCRT(); SavePlayerPrefs(); }
    public void SetCurvature(float v)   { settings.curvature = v;  ApplyCRT(); SavePlayerPrefs(); }
    public void SetVignette(float v)    { settings.vignette = v;   ApplyCRT(); SavePlayerPrefs(); }
    public void SetNoise(float v)       { settings.noise = v;      ApplyCRT(); SavePlayerPrefs(); }

    public void SetPanelWear(bool on)   { settings.panelWearEnabled = on; ApplyPanelWear(); SavePlayerPrefs(); }
    public void SetOverlayOpacity(float a){ settings.overlayOpacity = a;  ApplyPanelWear(); SavePlayerPrefs(); }
    public void SetEdgeDark(float v)    { settings.panelEdgeDark = v;     ApplyPanelWear(); SavePlayerPrefs(); }
    public void SetCornerVig(float v)   { settings.panelCornerVignette = v; ApplyPanelWear(); SavePlayerPrefs(); }
    public void SetCornerRadius(float v){ settings.panelCornerRadius = v; ApplyPanelWear(); SavePlayerPrefs(); }

    public void SetJitter(bool on)      { settings.jitterEnabled = on;    ApplyJitter(); SavePlayerPrefs(); }
    public void SetJitterAmpX(float v)  { settings.jitterAmpX = v;        ApplyJitter(); SavePlayerPrefs(); }
    public void SetJitterAmpY(float v)  { settings.jitterAmpY = v;        ApplyJitter(); SavePlayerPrefs(); }
    public void SetJitterSpeed(float v) { settings.jitterSpeed = v;       ApplyJitter(); SavePlayerPrefs(); }
    public void SetJitterScale(float v) { settings.jitterScaleAmp = v;    ApplyJitter(); SavePlayerPrefs(); }
    public void SetFlicker(float v)     { settings.flickerAmp = v;        ApplyJitter(); SavePlayerPrefs(); }

    // --- Central apply ---
    public void ApplyAll()
    {
        ApplyCRT();
        ApplyPanelWear();
        ApplyJitter();
    }

    void ApplyCRT()
    {
        // Toggle renderer feature
        if (crtFeature is CRTFeature typed)
        {
            typed.SetEnabled(settings.crtEnabled);
        }
        // Push params to material (works even if feature disabled, harmless)
        if (crtMaterial != null)
        {
            crtMaterial.SetFloat("_PixelSize", settings.pixelSize);
            crtMaterial.SetFloat("_ScanlineIntensity", settings.scanlineIntensity);
            crtMaterial.SetFloat("_Curvature", settings.curvature);
            crtMaterial.SetFloat("_Vignette", settings.vignette);
            crtMaterial.SetFloat("_NoiseAmount", settings.noise);

            // Optional bypass if you chose the shader early-out approach
            crtMaterial.SetFloat("_Bypass", settings.crtEnabled ? 0f : 1f);
        }
    }

    void ApplyPanelWear()
    {
        // Multiply overlays: enable/disable and opacity
        if (multiplyOverlays != null)
        {
            foreach (var g in multiplyOverlays)
            {
                if (g == null) continue;
                g.enabled = settings.panelWearEnabled;
                var c = g.color; c.a = settings.overlayOpacity; g.color = c;
            }
        }
        // Procedural wear materials: push parameters
        if (panelWearMaterials != null)
        {
            foreach (var m in panelWearMaterials)
            {
                if (m == null) continue;
                // enable/disable by swapping color alpha, or use a keyword if you added one
                m.SetFloat("_EdgeDark", settings.panelEdgeDark);
                m.SetFloat("_CornerVignette", settings.panelCornerVignette);
                m.SetFloat("_Radius", settings.panelCornerRadius);
            }
        }
    }

    void ApplyJitter()
    {
        if (jitters == null) return;
        foreach (var j in jitters)
        {
            if (j == null) continue;
            j.enabled        = settings.jitterEnabled;
            j.amplitudeX     = settings.jitterAmpX;
            j.amplitudeY     = settings.jitterAmpY;
            j.speed          = settings.jitterSpeed;
            j.scaleAmplitude = settings.jitterScaleAmp;
            j.flickerAmplitude = settings.flickerAmp;
        }
    }

    // --- Persistence ---
    void SavePlayerPrefs()
    {
        PlayerPrefs.SetInt("crtEnabled", settings.crtEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("px", settings.pixelSize);
        PlayerPrefs.SetFloat("scan", settings.scanlineIntensity);
        PlayerPrefs.SetFloat("curv", settings.curvature);
        PlayerPrefs.SetFloat("vig", settings.vignette);
        PlayerPrefs.SetFloat("noi", settings.noise);

        PlayerPrefs.SetInt("wear", settings.panelWearEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("wearAlpha", settings.overlayOpacity);
        PlayerPrefs.SetFloat("edge", settings.panelEdgeDark);
        PlayerPrefs.SetFloat("corner", settings.panelCornerVignette);
        PlayerPrefs.SetFloat("rad", settings.panelCornerRadius);

        PlayerPrefs.SetInt("jit", settings.jitterEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("jx", settings.jitterAmpX);
        PlayerPrefs.SetFloat("jy", settings.jitterAmpY);
        PlayerPrefs.SetFloat("js", settings.jitterSpeed);
        PlayerPrefs.SetFloat("jsc", settings.jitterScaleAmp);
        PlayerPrefs.SetFloat("jf", settings.flickerAmp);

        PlayerPrefs.Save();
    }

    void LoadPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey("crtEnabled")) return; // first run: keep asset defaults

        settings.crtEnabled = PlayerPrefs.GetInt("crtEnabled", 1) == 1;
        settings.pixelSize  = PlayerPrefs.GetFloat("px", settings.pixelSize);
        settings.scanlineIntensity = PlayerPrefs.GetFloat("scan", settings.scanlineIntensity);
        settings.curvature  = PlayerPrefs.GetFloat("curv", settings.curvature);
        settings.vignette   = PlayerPrefs.GetFloat("vig", settings.vignette);
        settings.noise      = PlayerPrefs.GetFloat("noi", settings.noise);

        settings.panelWearEnabled = PlayerPrefs.GetInt("wear", 1) == 1;
        settings.overlayOpacity   = PlayerPrefs.GetFloat("wearAlpha", settings.overlayOpacity);
        settings.panelEdgeDark    = PlayerPrefs.GetFloat("edge", settings.panelEdgeDark);
        settings.panelCornerVignette = PlayerPrefs.GetFloat("corner", settings.panelCornerVignette);
        settings.panelCornerRadius   = PlayerPrefs.GetFloat("rad", settings.panelCornerRadius);

        settings.jitterEnabled = PlayerPrefs.GetInt("jit", 1) == 1;
        settings.jitterAmpX    = PlayerPrefs.GetFloat("jx", settings.jitterAmpX);
        settings.jitterAmpY    = PlayerPrefs.GetFloat("jy", settings.jitterAmpY);
        settings.jitterSpeed   = PlayerPrefs.GetFloat("js", settings.jitterSpeed);
        settings.jitterScaleAmp= PlayerPrefs.GetFloat("jsc", settings.jitterScaleAmp);
        settings.flickerAmp    = PlayerPrefs.GetFloat("jf", settings.flickerAmp);
    }
	
public void SetReduceMotion(bool on)
	{
		settings.jitterEnabled = !on;
		ApplyJitter();

		if (UIEffects.Instance != null)
			UIEffects.Instance.SetEffectsEnabled(!on);

		SavePlayerPrefs(); // persist
	}


}
