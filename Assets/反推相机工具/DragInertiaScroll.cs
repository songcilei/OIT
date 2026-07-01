using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DragInertiaScroll : MonoBehaviour
{
    [Header("滑动参数")]
    public float dragSensitivity = 0.15f;    //拖拽灵敏度
    public float inertiaDamping = 6f;        //惯性衰减速度
    public float maxScrollX = 50f;           //X轴最大移动范围
    public float minScrollX = -50f;
    public float maxScrollY = 30f;            //Y轴最大移动范围
    public float minScrollY = -30f;

    private Vector3 startMousePos;
    private Vector3 currentCamPos;
    private Vector2 velocity;
    private bool isDraging;
    private Vector3 oldPosition;

    void Start()
    {
        currentCamPos = transform.position;
    }

    void Update()
    {
        // 处理拖拽按下
        if (IsPressDown())
        {
            isDraging = true;
            startMousePos = GetScreenInputPos();
            velocity = Vector2.zero;
            oldPosition = transform.position;
        }
        // 拖拽中
        else if (IsPressing() && isDraging)
        {

            
            
            
            Vector3 nowPos = GetScreenInputPos();
            Vector3 offset = startMousePos - nowPos;
            
            Vector2 moveDir = new Vector2(offset.x, offset.y) * dragSensitivity;
            currentCamPos += new Vector3(moveDir.x, 0, moveDir.y);
            
            // 限制移动边界
            currentCamPos.x = Mathf.Clamp(currentCamPos.x, minScrollX, maxScrollX);
            currentCamPos.z = Mathf.Clamp(currentCamPos.z, minScrollY, maxScrollY);
            
            transform.position = currentCamPos;
            startMousePos = nowPos;
        }
        // 松手开启惯性
        else if (!IsPressing() && isDraging)
        {
            isDraging = false;
            velocity = (currentCamPos - oldPosition)* Time.deltaTime;
            oldPosition = currentCamPos;
            
        }

        // 惯性滑行
        if (!isDraging && velocity.magnitude > 0.01f)
        {
            Debug.Log("1111111111");
            velocity = Vector2.Lerp(velocity, Vector2.zero, inertiaDamping * Time.deltaTime);
            currentCamPos += new Vector3(velocity.x, 0, velocity.y);
            
            currentCamPos.x = Mathf.Clamp(currentCamPos.x, minScrollX, maxScrollX);
            currentCamPos.z = Mathf.Clamp(currentCamPos.z, minScrollY, maxScrollY);
            transform.position = currentCamPos;
        }
    }

    // 获取输入坐标 兼容鼠标+触屏
    private Vector3 GetScreenInputPos()
    {
#if UNITY_ANDROID || UNITY_IOS
        return Input.GetTouch(0).position;
#else
        return Input.mousePosition;
#endif
    }

    // 判断按下
    private bool IsPressDown()
    {
#if UNITY_ANDROID || UNITY_IOS
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // 判断持续按住
    private bool IsPressing()
    {
#if UNITY_ANDROID || UNITY_IOS
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved;
#else
        return Input.GetMouseButton(0);
#endif
    }
}