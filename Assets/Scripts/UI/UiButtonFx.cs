using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Scale-pop on hover/press and hover/click sounds for any UI element.</summary>
public class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private float scale = 1f, target = 1f;
    private bool hover;

    private void OnEnable() { scale = target = 1f; transform.localScale = Vector3.one; hover = false; }

    private void Update()
    {
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; target = 1.05f; GameAudio.Play(Sfx.Hover); }
    public void OnPointerExit(PointerEventData e) { hover = false; target = 1f; }
    public void OnPointerDown(PointerEventData e) { target = 0.95f; GameAudio.Play(Sfx.Click); }
    public void OnPointerUp(PointerEventData e) { target = hover ? 1.05f : 1f; }
}
