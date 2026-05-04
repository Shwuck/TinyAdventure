using UnityEngine;
using UnityEngine.UI;

#if DOTWEEN
using DG.Tweening;
#endif

[ExecuteAlways]
public class UIJitter : MonoBehaviour
{
    [Header("Targets")]
    public RectTransform target; // if null, uses this RectTransform
    [Tooltip("Optional graphic to flicker slightly (e.g., a full-screen overlay).")]
    public Graphic flickerGraphic;

    [Header("Timing")]
    [Tooltip("Use unscaled time so the effect continues when the game is paused.")]
    public bool useUnscaledTime = true;

    [Header("Position Jitter (pixels)")]
    [Range(0f, 2f)] public float amplitudeX = 0.6f;
    [Range(0f, 2f)] public float amplitudeY = 0.4f;
    [Range(0.1f, 10f)] public float speed = 2.2f;

    [Header("Scale Micro-wobble")]
    [Range(0f, 0.01f)] public float scaleAmplitude = 0.003f;

    [Header("Flicker")]
    [Range(0f, 0.1f)] public float flickerAmplitude = 0.03f;

    [Header("Interop")]
    [Tooltip("If true, skip jitter on frames when DOTween is animating this RectTransform.")]
    public bool suspendWhenTweening = true;

    Vector2 _basePos;
    Vector3 _baseScale;
    float _seedX, _seedY, _seedS, _seedF;
    float _baseFlickerAlpha;

    void Reset()
    {
        target = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        if (target == null) target = GetComponent<RectTransform>();
        if (target == null) return;

        _basePos = target.anchoredPosition;
        _baseScale = target.localScale;

        _seedX = Random.value * 1000f;
        _seedY = Random.value * 1000f;
        _seedS = Random.value * 1000f;
        _seedF = Random.value * 1000f;

        if (flickerGraphic != null)
        {
            _baseFlickerAlpha = flickerGraphic.color.a;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        #if DOTWEEN
        if (suspendWhenTweening && DOTween.IsTweening(target)) return;
        #endif

        float t = useUnscaledTime
            ? (Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup)
            : (Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

        // Position jitter (pixel-snapped)
        float nx = Mathf.PerlinNoise(_seedX, t * speed) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_seedY, t * speed * 1.1f) * 2f - 1f;

        float px = Mathf.Round(_basePos.x + nx * amplitudeX);
        float py = Mathf.Round(_basePos.y + ny * amplitudeY);
        target.anchoredPosition = new Vector2(px, py);

        // Micro scale wobble
        float ns = (Mathf.PerlinNoise(_seedS, t * (speed * 0.6f)) - 0.5f) * 2f;
        float s = 1f + ns * scaleAmplitude;
        target.localScale = new Vector3(s, s, 1f);

        // Subtle flicker (non-drifting)
        if (flickerGraphic != null && flickerAmplitude > 0f)
        {
            float nf = (Mathf.PerlinNoise(_seedF, t * 50f) - 0.5f) * 2f; // fast
            var c = flickerGraphic.color;
            c.a = Mathf.Clamp01(_baseFlickerAlpha + nf * flickerAmplitude);
            flickerGraphic.color = c;
        }
    }

    void OnDisable()
    {
        if (target == null) return;
        target.anchoredPosition = _basePos;
        target.localScale = _baseScale;

        if (flickerGraphic != null)
        {
            var c = flickerGraphic.color;
            c.a = _baseFlickerAlpha;
            flickerGraphic.color = c;
        }
    }
}
