using UnityEngine;

public class CursorAutoHide : MonoBehaviour
{
    [SerializeField]
    private float hideDelay = 1f;

    private Vector3 lastMousePosition;
    private float idleTimer;

    private void Start()
    {
        lastMousePosition = Input.mousePosition;
    }

    private void Update()
    {
        Vector3 currentPosition = Input.mousePosition;

        if(currentPosition != lastMousePosition) {
            lastMousePosition = currentPosition;
            idleTimer = 0f;
            Cursor.visible = true;
            return;
        }

        idleTimer += Time.unscaledDeltaTime;

        if (idleTimer >= hideDelay)
            Cursor.visible = false;
    }
}
