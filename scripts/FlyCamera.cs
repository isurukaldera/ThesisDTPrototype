using UnityEngine;

public class FlyCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float fastSpeed = 20f;
    public float mouseSensitivity = 2f;
    
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool cursorLocked = true;

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        // Toggle cursor lock with ESC key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            cursorLocked = !cursorLocked;
            LockCursor(cursorLocked);
        }

        if (cursorLocked)
        {
            HandleMouseLook();
            HandleMovement();
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);
    }

    void HandleMovement()
    {
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : moveSpeed;

        // Forward/backward
        if (Input.GetKey(KeyCode.W)) transform.position += transform.forward * currentSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) transform.position -= transform.forward * currentSpeed * Time.deltaTime;
        
        // Left/right
        if (Input.GetKey(KeyCode.D)) transform.position += transform.right * currentSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) transform.position -= transform.right * currentSpeed * Time.deltaTime;
        
        // Up/down
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) transform.position += Vector3.up * currentSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) transform.position -= Vector3.up * currentSpeed * Time.deltaTime;
    }

    void LockCursor(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}