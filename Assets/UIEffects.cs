using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class UIEffects : MonoBehaviour
{
    public static UIEffects Instance;

    [Header("Global Switches")]
    [Tooltip("Master kill-switch for all UI tweens (for Accessibility: Reduce Motion).")]
    public bool effectsEnabled = true;

    [Tooltip("Scales durations/strengths. 0 = off, 1 = normal, 2 = double.")]
    [Range(0f, 2f)] public float intensity = 1f;

    [Tooltip("Use unscaled time so UI animates even when the game is paused.")]
    public bool useUnscaledTime = true;

    // Optional persistence across scenes:
    [SerializeField] bool dontDestroyOnLoad = true;

    // Track long-running tweens (e.g., pulses) so we can stop/replace them cleanly
    private readonly Dictionary<Transform, Tween> _activePulses = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        // Make sure DOTween is in a sane state
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
    }

    // --- Helpers ------------------------------------------------------------

    private bool CanPlay() => effectsEnabled && intensity > 0.0001f;
	public void SetEffectsEnabled(bool on) => effectsEnabled = on;


    private Tween Configure(Tween t)
    {
        if (t == null) return null;
        if (useUnscaledTime) t.SetUpdate(true); // true = use unscaled time
        return t;
    }

    private float ScaleDuration(float baseDuration) => Mathf.Max(0.0001f, baseDuration / Mathf.Max(0.0001f, intensity));
    private float ScaleStrength(float baseStrength) => baseStrength * intensity;

    // --- Effects ------------------------------------------------------------

    /// <summary>Shake anchored position of a RectTransform.</summary>
    public void ShakeUI(RectTransform rectTransform, float duration = 0.2f, float strength = 5f, int vibrato = 15, float randomness = 90f)
    {
        if (!CanPlay() || rectTransform == null) return;

        duration = ScaleDuration(duration);
        strength = ScaleStrength(strength);

        // Kill any existing anchoredPos tween to avoid stacking
        rectTransform.DOKill(true);

        Configure(rectTransform.DOShakeAnchorPos(duration, strength, vibrato, randomness, snapping: true));
    }

    /// <summary>Fade a CanvasGroup alpha.</summary>
    public void FadeUI(CanvasGroup canvasGroup, float targetAlpha, float duration = 0.5f, Ease ease = Ease.InOutSine)
    {
        if (canvasGroup == null) return;
        if (!CanPlay()) { canvasGroup.alpha = targetAlpha; return; }

        duration = ScaleDuration(duration);
        canvasGroup.DOKill(true);
        Configure(canvasGroup.DOFade(targetAlpha, duration).SetEase(ease));
    }

    /// <summary>Move anchored position to a target pixel-snapped position.</summary>
    public void MoveUI(RectTransform rectTransform, Vector2 targetPosition, float duration = 0.5f, Ease ease = Ease.OutExpo)
    {
        if (rectTransform == null) return;
        if (!CanPlay()) { rectTransform.anchoredPosition = RoundToPixels(targetPosition); return; }

        duration = ScaleDuration(duration);
        rectTransform.DOKill(true);
        Configure(rectTransform.DOAnchorPos(RoundToPixels(targetPosition), duration, snapping: true).SetEase(ease));
    }

    /// <summary>Start a pulsing scale on a UI element. Call StopPulse to cancel.</summary>
    public void StartPulse(GameObject uiElement, float scaleFactor = 1.08f, float duration = 0.45f, int loops = -1, Ease ease = Ease.InOutSine)
    {
        if (uiElement == null) return;
        StopPulse(uiElement); // ensure only one pulse per element

        if (!CanPlay()) return;

        duration = ScaleDuration(duration);
        var tr = uiElement.transform;

        // Base scale is remembered implicitly; we yo-yo around it
        var t = tr.DOScale(scaleFactor, duration)
                  .SetEase(ease)
                  .SetLoops(loops, LoopType.Yoyo);

        Configure(t);
        _activePulses[tr] = t;
    }

    public void StopPulse(GameObject uiElement, bool revertScale = true)
    {
        if (uiElement == null) return;

        var tr = uiElement.transform;
        if (_activePulses.TryGetValue(tr, out var t) && t.IsActive())
        {
            t.Kill(true);
        }
        _activePulses.Remove(tr);

        if (revertScale) tr.localScale = Vector3.one;
    }

    /// <summary>Panel glow by tweening the Image color.</summary>
    public void PanelGlow(Image panelImage, Color glowColor, float duration = 0.5f, int loops = 2, Ease ease = Ease.InOutSine)
    {
        if (panelImage == null) return;
        if (!CanPlay()) return;

        duration = ScaleDuration(duration);
        var original = panelImage.color;

        panelImage.DOKill(true);
        Configure(panelImage.DOColor(glowColor, duration)
            .SetEase(ease)
            .SetLoops(Mathf.Max(1, loops), LoopType.Yoyo)
            .OnComplete(() => panelImage.color = original));
    }

    /// <summary>Border glow for Outline (DOTween has no direct extension, so we tween effectColor manually).</summary>
    public void BorderGlow(Outline outline, Color glowColor, float duration = 0.5f, int loops = 2, Ease ease = Ease.InOutSine)
    {
        if (outline == null) return;
        if (!CanPlay()) return;

        duration = ScaleDuration(duration);
        Color original = outline.effectColor;

        // Kill prior manual tween by ID (the Outline itself)
        DOTween.Kill(outline, complete: false);

        var t = DOTween.To(() => outline.effectColor, c => outline.effectColor = c, glowColor, duration)
                       .SetEase(ease)
                       .SetLoops(Mathf.Max(1, loops), LoopType.Yoyo)
                       .SetId(outline)
                       .OnComplete(() => outline.effectColor = original);

        Configure(t);
    }

    /// <summary>Adjust TextMeshPro alpha with tween; instant if effects disabled.</summary>
    public void AdjustTextBrightness(TMP_Text tmp, float targetAlpha, float duration = 0.3f, Ease ease = Ease.OutSine)
    {
        if (tmp == null) return;

        if (!CanPlay())
        {
            var c = tmp.color; c.a = targetAlpha; tmp.color = c;
            return;
        }

        duration = ScaleDuration(duration);
        tmp.DOKill(true);
        Configure(tmp.DOFade(targetAlpha, duration).SetEase(ease));
    }

    /// <summary>Tween alpha for all TMP children under given panels.</summary>
    public void AdjustTextBrightnessInPanels(List<GameObject> panels, float targetAlpha, float duration = 0.3f)
    {
        if (panels == null || panels.Count == 0) return;

        foreach (var panel in panels)
        {
            if (panel == null) continue;
            var tmps = panel.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps) AdjustTextBrightness(t, targetAlpha, duration);
        }
    }

    // --- Utilities ----------------------------------------------------------

    private static Vector2 RoundToPixels(Vector2 pos)
    {
        // assuming 1 Canvas unit = 1 pixel in Screen Space - Overlay; still helps with retro look
        pos.x = Mathf.Round(pos.x);
        pos.y = Mathf.Round(pos.y);
        return pos;
    }

    /// <summary>Global kill for all active tweens started by this manager.</summary>
    public void KillAll()
    {
        foreach (var kv in _activePulses) if (kv.Value != null && kv.Value.IsActive()) kv.Value.Kill(true);
        _activePulses.Clear();
        DOTween.Kill(this, complete: false);
    }
}
