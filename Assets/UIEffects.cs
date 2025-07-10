using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using System.Collections.Generic;
using DG.Tweening; // Import DOTween

public class UIEffects : MonoBehaviour
{
    public static UIEffects Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Shake UI Element
    public void ShakeUI(RectTransform rectTransform, float duration = 0.2f, float strength = 5f)
    {
        rectTransform.DOShakeAnchorPos(duration, strength);
    }

    // Fade UI Element (CanvasGroup)
    public void FadeUI(CanvasGroup canvasGroup, float targetAlpha, float duration = 0.5f)
    {
        canvasGroup.DOFade(targetAlpha, duration);
    }

    // Move UI Element
    public void MoveUI(RectTransform rectTransform, Vector2 targetPosition, float duration = 0.5f)
    {
        rectTransform.DOAnchorPos(targetPosition, duration).SetEase(Ease.OutExpo);
    }

    // Pulse UI Element
    public void PulseUI(GameObject uiElement, float scaleFactor = 1.1f, float duration = 0.5f)
    {
        uiElement.transform.DOScale(scaleFactor, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo); // Infinite ping-pong effect
    }

    // Panel Glow Effect
    public void PanelGlow(Image panelImage, Color glowColor, float duration = 0.5f, int loops = -1)
    {
        Color originalColor = panelImage.color;

        panelImage.DOColor(glowColor, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(loops, LoopType.Yoyo)
            .OnComplete(() => panelImage.color = originalColor);
    }

    // Border Glow Effect (UI Outline Component)
    public void BorderGlow(Outline outline, Color glowColor, float duration = 0.5f, int loops = -1)
    {
        Color originalColor = outline.effectColor;

        outline.DOColor(glowColor, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(loops, LoopType.Yoyo)
            .OnComplete(() => outline.effectColor = originalColor);
    }

    // Adjusts Text Brightness for a Specific UI Element
    public void AdjustTextBrightness(TMP_Text textMeshPro, float targetAlpha, float duration = 0.5f)
    {
        if (textMeshPro == null) return;
        Color currentColor = textMeshPro.color;
        textMeshPro.color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);
    }

    // Adjusts All Text Brightness in a Group of UI Panels
    public void AdjustTextBrightnessInPanels(List<GameObject> panels, float targetAlpha, float duration = 0.5f)
    {
        if (panels == null || panels.Count == 0) return;

        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                foreach (TMP_Text textElement in panel.GetComponentsInChildren<TMP_Text>())
                {
                    AdjustTextBrightness(textElement, targetAlpha, duration);
                }
            }
        }
    }
}
