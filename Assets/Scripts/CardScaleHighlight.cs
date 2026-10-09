using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardScaleHighlight : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("缩放设置")]
    public float pressedScale = 1.1f;
    public float hoverScale = 1.05f;
    public float scaleSpeed = 10f;

    [Header("高亮设置")]
    public float pressedBrightness = 1.3f;
    public float hoverBrightness = 1.15f;
    public float normalBrightness = 1f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private float targetBrightness;
    private Image cardImage;
    private bool isPointerInside;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        targetBrightness = normalBrightness;
        cardImage = GetComponent<Image>();
    }

    void Update()
    {
        // Smooth scale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        // Smooth brightness
        if (cardImage != null)
        {
            float currentBrightness = Mathf.Lerp(cardImage.color.r, targetBrightness, Time.deltaTime * scaleSpeed);
            cardImage.color = new Color(currentBrightness, currentBrightness, currentBrightness, 1f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originalScale * pressedScale;
        targetBrightness = pressedBrightness;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isPointerInside)
        {
            targetScale = originalScale * hoverScale;
            targetBrightness = hoverBrightness;
        }
        else
        {
            targetScale = originalScale;
            targetBrightness = normalBrightness;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        targetScale = originalScale * hoverScale;
        targetBrightness = hoverBrightness;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        targetScale = originalScale;
        targetBrightness = normalBrightness;
    }
}