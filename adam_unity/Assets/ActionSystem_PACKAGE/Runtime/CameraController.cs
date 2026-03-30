using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float verticalSpeed = 3f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("Controls")]
    [SerializeField] private KeyCode moveUpKey = KeyCode.E;
    [SerializeField] private KeyCode moveDownKey = KeyCode.Q;
    [SerializeField] private KeyCode lockCursorKey = KeyCode.Escape;
    
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool cursorLocked = true;
    
    void Start()
    {
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Get initial rotation
        rotationX = transform.eulerAngles.y;
        rotationY = -transform.eulerAngles.x;
    }
    
    void Update()
    {
        HandleCursorLock();
        HandleMouseLook();
        HandleMovement();
    }
    
    void HandleCursorLock()
    {
        if (Input.GetKeyDown(lockCursorKey))
        {
            cursorLocked = !cursorLocked;
            
            if (cursorLocked)
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
    
    void HandleMouseLook()
    {
        if (!cursorLocked) return;
        
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Calculate rotation
        rotationX += mouseX;
        rotationY -= mouseY;
        rotationY = Mathf.Clamp(rotationY, -maxLookAngle, maxLookAngle);
        
        // Apply rotation
        transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }
    
    void HandleMovement()
    {
        if (!cursorLocked) return;
        
        // Get input axes
        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right arrows
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down arrows
        
        // Calculate movement direction
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        // Remove Y component to keep movement horizontal
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        // Calculate horizontal movement
        Vector3 horizontalMovement = (forward * vertical + right * horizontal) * moveSpeed * Time.deltaTime;
        
        // Calculate vertical movement
        Vector3 verticalMovement = Vector3.zero;
        if (Input.GetKey(moveUpKey))
        {
            verticalMovement = Vector3.up * verticalSpeed * Time.deltaTime;
        }
        else if (Input.GetKey(moveDownKey))
        {
            verticalMovement = Vector3.down * verticalSpeed * Time.deltaTime;
        }
        
        // Apply movement
        transform.position += horizontalMovement + verticalMovement;
    }
} 