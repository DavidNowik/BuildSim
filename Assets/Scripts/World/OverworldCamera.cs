using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pan (drag / WASD), zoom (wheel) and shake for the overworld. A click that did not drag is a click.</summary>
public class OverworldCamera : MonoBehaviour
{
    public float minSize = 3.5f;
    public float maxSize = 16f;
    public float smoothing = 14f;
    public float dragThresholdPixels = 6f;

    /// <summary>World rectangle the camera centre may move in.</summary>
    public Rect bounds = new Rect(-12, -14, 24, 22);

    /// <summary>True while the current press has moved far enough to count as a drag (not a click).</summary>
    public bool DragMoved { get; private set; }

    private Camera cam;
    private Vector3 pos;            // camera centre without shake
    private Vector3 targetPos;
    private float targetSize;
    private Vector3 pressWorld;
    private Vector2 pressScreen;
    private bool pressing;
    private float shakeAmp, shakeTime, shakeDur;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        pos = targetPos = transform.position;
        targetSize = cam.orthographicSize;
    }

    /// <summary>Frame a world rectangle (snaps, no smoothing).</summary>
    public void Fit(Rect r)
    {
        if (cam == null) cam = GetComponent<Camera>();
        float h = r.height * 0.5f, w = r.width * 0.5f / Mathf.Max(0.1f, cam.aspect);
        targetSize = Mathf.Clamp(Mathf.Max(h, w), minSize, maxSize);
        cam.orthographicSize = targetSize;
        pos = targetPos = new Vector3(r.center.x, r.center.y, transform.position.z);
        transform.position = pos;
    }

    public void Shake(float amplitude, float duration) { shakeAmp = amplitude; shakeDur = shakeTime = duration; }

    private bool OverUI() { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f && !OverUI())
            targetSize = Mathf.Clamp(targetSize * Mathf.Exp(-scroll * 4f), minSize, maxSize);

        // drag to pan with left or middle button; small movement still counts as a click
        if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)) && !OverUI())
        {
            pressing = true; DragMoved = false;
            pressScreen = Input.mousePosition;
            pressWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        }
        bool dragging = false;
        if (pressing && (Input.GetMouseButton(0) || Input.GetMouseButton(2)))
        {
            if (!DragMoved && Vector2.Distance(pressScreen, Input.mousePosition) > dragThresholdPixels) DragMoved = true;
            if (DragMoved)
            {
                transform.position = pos;                                  // measure without the shake offset
                Vector3 diff = pressWorld - cam.ScreenToWorldPoint(Input.mousePosition);
                diff.z = 0f;
                pos += diff; targetPos += diff;
                dragging = true;
            }
        }
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(2)) pressing = false;

        Vector2 k = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) k.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) k.y += 1f;
        if (DebugConsole.IsOpen) k = Vector2.zero;
        targetPos += (Vector3)(k * cam.orthographicSize * 1.2f * dt);

        targetPos.x = Mathf.Clamp(targetPos.x, bounds.xMin, bounds.xMax);
        targetPos.y = Mathf.Clamp(targetPos.y, bounds.yMin, bounds.yMax);

        float f = 1f - Mathf.Exp(-smoothing * dt);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, f);
        if (!dragging) pos = Vector3.Lerp(pos, targetPos, f);

        Vector2 shake = Vector2.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= dt;
            shake = Random.insideUnitCircle * shakeAmp * Mathf.Clamp01(shakeTime / shakeDur);
        }
        transform.position = new Vector3(pos.x + shake.x, pos.y + shake.y, transform.position.z);
    }
}
