using UnityEngine;
using UnityEngine.UI;

public class FloatingText
{
    public GameObject gameObject;
    public bool active;
    public GameObject go;
    public Text txt;
    public float duration;
    public float lastShown;
    private Vector3 target;

    public void Show(Vector3 targetTransform)
    {
        target = targetTransform;
        active = true;
        lastShown = Time.time;
        go.SetActive(active);
    }
    public void Hide()
    {
        active = false;
        go.SetActive(active);
    }

    public void UpdateFloatingText()
    {
        if (!active)
            return;
        if (Time.time - lastShown > duration)
            Hide();

        go.transform.position = Camera.main.WorldToScreenPoint(target); //Transform WorldSpace (Player) to ScreenSpace (UI)
        target.y += 0.001f;
    }

}
