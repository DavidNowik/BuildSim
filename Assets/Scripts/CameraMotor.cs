using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pixel-perfect RTS camera for 16-PPU art.
///  - Zoom happens in INTEGER pixel scales (1x..6x) so every sprite pixel is a whole number of screen pixels.
///  - Zoom is toward the cursor, smoothing is frame-rate independent (unscaled time, works while paused).
///  - Pan: WASD / arrows, middle-mouse drag (optional edge scrolling). Clamped to the map. Home recentres.
///  - Rendered position is snapped to the pixel grid so nothing shimmers.
/// </summary>
public class CameraMotor : MonoBehaviour
{
    public static CameraMotor Instance;

    [Header("Pan")]
    public float panSpeedUnits = 14f;      // world units per second at ortho size 10
    public float smoothing = 14f;          // higher = snappier
    public bool edgePan = false;
    public float edgeSizePixels = 6f;
    public float boundsPadding = 4f;

    [Header("Zoom (integer screen pixels per sprite pixel)")]
    public float pixelsPerUnit = 16f;
    public int minZoomLevel = 1;
    public int maxZoomLevel = 6;
    public float zoomSmoothing = 16f;
    [Tooltip("Off: the wheel only zooms (around the screen centre). On: the point under the cursor stays put while zooming.")]
    public bool zoomToCursor = false;

    private Camera cam;
    private Vector3 targetPos, pos;
    private Vector2 velocity;
    private float zoomCur;
    private int zoomTarget;
    private Vector3 dragOrigin;
    private bool dragging;
    private bool zoomFocusActive;
    private Vector3 zoomFocusWorld;
    private float shakeAmp, shakeTime, shakeDur;
    private float camZ;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        camZ = transform.position.z;

        // start fitted to the map: biggest integer zoom that still shows the whole grid height
        var g = GridManager.instance;
        if (g != null)
        {
            int fit = Mathf.FloorToInt(Screen.height / (pixelsPerUnit * (g.height * g.tileSpacing + 5f)));
            zoomTarget = Mathf.Clamp(fit, minZoomLevel, maxZoomLevel);
            Vector3 c = new Vector3(g.WorldBounds.center.x, g.WorldBounds.center.y, camZ);
            targetPos = pos = c;
        }
        else
        {
            zoomTarget = 2;
            targetPos = pos = transform.position;
        }
        zoomCur = zoomTarget;
        ApplyOrtho();
        transform.position = new Vector3(pos.x, pos.y, camZ);
    }

    private bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void Update()
    {
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;

        HandleZoomInput();
        HandleDrag();
        HandleKeyboard(dt);

        // smooth zoom toward the integer target
        float k = 1f - Mathf.Exp(-zoomSmoothing * dt);
        zoomCur = Mathf.Lerp(zoomCur, zoomTarget, k);
        if (Mathf.Abs(zoomCur - zoomTarget) < 0.004f) { zoomCur = zoomTarget; }
        ApplyOrtho();

        if (zoomFocusActive)
        {
            // keep the world point that was under the cursor under the cursor while zooming
            Vector2 mouse = Input.mousePosition;
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            float pxPerWorld = pixelsPerUnit * zoomCur;
            Vector2 offset = (mouse - center) / pxPerWorld;
            targetPos = pos = new Vector3(zoomFocusWorld.x - offset.x, zoomFocusWorld.y - offset.y, camZ);
            if (zoomCur == zoomTarget) zoomFocusActive = false;
        }

        ClampTarget();
        if (!dragging && !zoomFocusActive)
            pos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Exp(-smoothing * dt));

        // shake
        Vector2 shake = Vector2.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= dt;
            float a = shakeAmp * Mathf.Clamp01(shakeTime / shakeDur);
            shake = Random.insideUnitCircle * a;
        }

        Vector3 final = new Vector3(pos.x + shake.x, pos.y + shake.y, camZ);
        // pixel snap (only meaningful at integer zoom)
        if (Mathf.Approximately(zoomCur, Mathf.Round(zoomCur)))
        {
            float unit = 1f / (pixelsPerUnit * zoomCur);
            final.x = Mathf.Round(final.x / unit) * unit;
            final.y = Mathf.Round(final.y / unit) * unit;
        }
        transform.position = final;
    }

    private void ApplyOrtho()
    {
        cam.orthographicSize = Screen.height / (2f * pixelsPerUnit * Mathf.Max(0.5f, zoomCur));
    }

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f && !PointerOverUI())
        {
            int newTarget = Mathf.Clamp(zoomTarget + (scroll > 0 ? 1 : -1), minZoomLevel, maxZoomLevel);
            if (newTarget != zoomTarget)
            {
                zoomFocusWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                zoomFocusWorld.z = 0f;
                zoomFocusActive = zoomToCursor;
                zoomTarget = newTarget;
            }
        }
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(2) && !PointerOverUI())
        {
            dragging = true;
            zoomFocusActive = false;
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(2)) dragging = false;

        if (dragging && Input.GetMouseButton(2))
        {
            Vector3 current = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 diff = dragOrigin - current;
            diff.z = 0f;
            targetPos += diff;
            pos = targetPos;
            // the camera moved, so re-sample the world point under the cursor
            transform.position = new Vector3(pos.x, pos.y, camZ);
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }
    }

    private void HandleKeyboard(float dt)
    {
        Vector2 input = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;

        if (edgePan && Application.isFocused && !dragging)
        {
            Vector3 m = Input.mousePosition;
            if (m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
            {
                if (m.x < edgeSizePixels) input.x -= 1f;
                if (m.x > Screen.width - edgeSizePixels) input.x += 1f;
                if (m.y < edgeSizePixels) input.y -= 1f;
                if (m.y > Screen.height - edgeSizePixels) input.y += 1f;
            }
        }
        if (DebugConsole.IsOpen) input = Vector2.zero;
        if (input.sqrMagnitude > 0f) zoomFocusActive = false; // manual panning wins over the zoom-focus lock
        if (input.sqrMagnitude > 1f) input.Normalize();

        float speed = panSpeedUnits * (cam.orthographicSize / 10f);
        velocity = Vector2.Lerp(velocity, input * speed, 1f - Mathf.Exp(-12f * dt));
        if (velocity.sqrMagnitude > 0.0001f) targetPos += (Vector3)(velocity * dt);

        if (Input.GetKeyDown(KeyCode.Home) && GridManager.instance != null)
        {
            var c = GridManager.instance.WorldBounds.center;
            targetPos = new Vector3(c.x, c.y, camZ);
        }
    }

    private void ClampTarget()
    {
        var g = GridManager.instance;
        if (g == null) return;
        Rect b = g.WorldBounds;
        float minX = b.xMin - boundsPadding, maxX = b.xMax + boundsPadding;
        float minY = b.yMin - boundsPadding, maxY = b.yMax + boundsPadding;
        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
    }

    /// <summary>Jumps to an integer zoom level (1 = whole map ... 6 = closest). Used by tests and debug commands.</summary>
    public void SetZoomLevel(int level)
    {
        zoomTarget = Mathf.Clamp(level, minZoomLevel, maxZoomLevel);
        zoomCur = zoomTarget;
    }

    public void Shake(float amplitude, float duration)
    {
        shakeAmp = Mathf.Max(shakeAmp * (shakeTime > 0f ? 1f : 0f), amplitude);
        shakeDur = duration;
        shakeTime = duration;
    }
}
