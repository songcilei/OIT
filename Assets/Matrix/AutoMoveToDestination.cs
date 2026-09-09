using UnityEngine;
using UnityEngine.AI;

public class AutoMoveToDestination : MonoBehaviour
{
    public Transform target; // 在Inspector面板拖入目标位置的物体
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // 启动时自动设置目标
        if (target != null)
        {
            // 验证目标点是否在导航网格上
            if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                Debug.LogError("目标点不在导航网格上！");
            }
        }
        else
        {
            Debug.LogError("请给target参数赋值一个目标物体！");
        }
    }

    // 删除Update中的鼠标点击逻辑
}