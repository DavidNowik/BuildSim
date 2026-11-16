using UnityEngine;

public class CameraMotor : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 10f;
    public float panSmooth = 0.1f; // Lerp Smooth

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 5f;
    public float maxZoom = 50f;

    private Vector3 targetPosition;
    private Camera cam;

    private Vector3 dragOrigin;
    private float targetZoom;

    void Start()
    {
        cam = Camera.main;
        targetPosition = transform.position;
        targetZoom = cam.orthographicSize;
    }

    void Update()
    {
        HandleMouseDrag();
        HandleZoom();

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, targetPosition, panSmooth);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, panSmooth);
    }

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2)) // Middle Mouse Down
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        if (Input.GetMouseButton(2)) // Holding
        {
            Vector3 current = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 difference = dragOrigin - current;
            difference.z = 0;

            targetPosition += difference;
            dragOrigin = current; // update origin so it doesn’t stack
        }
    }


    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetZoom -= scroll * zoomSpeed; // scroll up = zoom in, down = zoom out
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }
}
