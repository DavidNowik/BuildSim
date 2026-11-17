using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sits on a small handle that is a child of a HUD panel, on the panel's inner side. Click the handle, or drag it toward
/// the screen edge, and the panel slides off that edge leaving only the handle; drag/click again to bring it back.
/// The state is remembered per panel (PlayerPrefs).
/// </summary>
public class Collapsible : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum Edge { Left, Right, Top, Bottom }

    [Tooltip("The panel that slides (usually this object's parent)")]
    public RectTransform panel;
    [Tooltip("Screen edge the panel slides off to")]
    public Edge edge = Edge.Left;
    public Text arrow;
    public float speed = 14f;
    [Tooltip("Dragging the handle further than this (canvas units) toward / away from the edge collapses / expands")]
    public float dragThreshold = 36f;
    [Tooltip("Off for panels that are opened/closed by game logic (e.g. the inspector)")]
    public bool remember = true;
    public bool startCollapsed;

    private bool collapsed, ready;
    private Vector2 expandedPos, collapsedPos;
    private float dragAcc;
    private string Key { get { return "hud.collapsed." + (panel != null ? panel.name : name); } }

    private void Start()
    {
        collapsed = startCollapsed;
        if (remember) { try { collapsed = PlayerPrefs.GetInt(Key, 0) == 1; } catch { } }
    }

    private Vector2 EdgeDir
    {
        get
        {
            switch (edge)
            {
                case Edge.Left: return Vector2.left;
                case Edge.Right: return Vector2.right;
                case Edge.Top: return Vector2.up;
                default: return Vector2.down;
            }
        }
    }

    private void Measure()
    {
        Canvas.ForceUpdateCanvases();
        var canvasRt = (RectTransform)panel.GetComponentInParent<Canvas>().rootCanvas.transform;
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = canvasRt.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        Rect cr = canvasRt.rect;
        Vector2 shift;
        switch (edge)
        {
            case Edge.Left: shift = new Vector2(-(min.x - cr.xMin) - (max.x - min.x), 0f); break;
            case Edge.Right: shift = new Vector2((cr.xMax - max.x) + (max.x - min.x), 0f); break;
            case Edge.Top: shift = new Vector2(0f, (cr.yMax - max.y) + (max.y - min.y)); break;
            default: shift = new Vector2(0f, -(min.y - cr.yMin) - (max.y - min.y)); break;
        }
        expandedPos = panel.anchoredPosition;
        collapsedPos = expandedPos + shift;
        ready = true;
        if (collapsed) panel.anchoredPosition = collapsedPos;
    }

    private void Update()
    {
        if (panel == null) return;
        if (!ready) { Measure(); UpdateArrow(); return; }
        Vector2 target = collapsed ? collapsedPos : expandedPos;
        Vector2 cur = panel.anchoredPosition;
        cur = Vector2.Lerp(cur, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        if ((cur - target).sqrMagnitude < 0.04f) cur = target;
        panel.anchoredPosition = cur;
    }

    public void SetCollapsed(bool value, bool sound = true)
    {
        if (collapsed == value) return;
        collapsed = value;
        if (remember) { try { PlayerPrefs.SetInt(Key, collapsed ? 1 : 0); } catch { } }
        if (sound) GameAudio.Play(Sfx.Click, 0.5f, collapsed ? 0.9f : 1.15f);
        UpdateArrow();
    }

    public void Toggle() { SetCollapsed(!collapsed); }

    private void UpdateArrow()
    {
        if (arrow == null) return;
        bool toEdge = !collapsed;     // the arrow points where a click sends the panel
        switch (edge)
        {
            case Edge.Left: arrow.text = toEdge ? "<" : ">"; break;
            case Edge.Right: arrow.text = toEdge ? ">" : "<"; break;
            case Edge.Top: arrow.text = toEdge ? "^" : "v"; break;
            default: arrow.text = toEdge ? "v" : "^"; break;
        }
    }

    public void OnPointerClick(PointerEventData e) { Toggle(); }

    public void OnBeginDrag(PointerEventData e) { dragAcc = 0f; }

    public void OnDrag(PointerEventData e)
    {
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        dragAcc += Vector2.Dot(e.delta / Mathf.Max(0.01f, scale), EdgeDir);
        if (dragAcc > dragThreshold) { SetCollapsed(true); dragAcc = 0f; }
        else if (dragAcc < -dragThreshold) { SetCollapsed(false); dragAcc = 0f; }
    }

    public void OnEndDrag(PointerEventData e) { }
}
