using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFlyController : MonoBehaviour
{
    [Header("基础移动速度")]
    public float moveSpeed = 5f;

    [Header("加速倍率 (按住Shift)")]
    public float boostSpeed = 2f;

    [Header("鼠标灵敏度")]
    public float mouseSensitivity = 2f;

    [Header("滚轮速度调节")]
    public float scrollSpeed = 2f;
    public float minSpeed = 1f;
    public float maxSpeed = 20f;

    private float yaw;   // 水平视角
    private float pitch; // 垂直视角

    void Start()
    {
        // 记录初始视角
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // 鼠标右键旋转视角
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f); // 防止视角翻转
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        // 滚轮调节速度
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            moveSpeed = Mathf.Clamp(moveSpeed + scroll * scrollSpeed, minSpeed, maxSpeed);
        }

        // WASD 移动
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;

        // 归一化防止斜着走更快
        if (dir.magnitude > 1f) dir.Normalize();

        // Shift 加速
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= boostSpeed;

        transform.position += dir * speed * Time.deltaTime;
    }
}