using System;
using UnityEngine;

public enum DelayTimeState
{
    None,
    Skill01Delay,
}

public enum AnimaState
{
    Idle,
    Atk,
    Skill01,
    Death,
    Win,
}


[RequireComponent(typeof(Animator))]
public class AntimaCtl : MonoBehaviour
{

    public float MoveSpeed = 1;
    public bool FastChange = false;
    public Transform debugObject;
    
    [Range(0f, 1f)]
    public float compensationWeight = 1f;

    public int compensationLayer = 1;

    public float Skill01DelayTime = 1;
    
    public AnimaState _animaState = AnimaState.Idle;
    private DelayTimeState _delayTimeState = DelayTimeState.None;
    private float delayTime = 0;
    private Animator animator;
    private Transform hips;
    private Transform spine;

    private Quaternion referenceHipsLocalRotation;
    private float Hor = 0;
    private float Ver = 0;


    private static readonly int HorizontalId = Animator.StringToHash("Horizontal");
    private static readonly int VerticalId = Animator.StringToHash("Vertical");
    private static readonly int FireId = Animator.StringToHash("Fire");
    private static readonly int Skill01Id = Animator.StringToHash("Skill01");
    private static readonly int WinId = Animator.StringToHash("Win");
    private static readonly int LiveId = Animator.StringToHash("Live");
    private static readonly int DeathId = Animator.StringToHash("Death");
    private static readonly int ReloadId = Animator.StringToHash("Reload");
    private static readonly int Skill01EndId = Animator.StringToHash("Skill01End");
    private void Start()
    {
        animator = GetComponent<Animator>();

        if (!animator.isHuman )
        {
            Debug.LogError("需要 Humanoid Animator 和参考姿势 Hips。", this);
            enabled = false;
            return;
        }

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);

        if (hips == null || spine == null || spine.parent != hips)
        {
            Debug.LogError(
                "这个验证版本要求 Spine 的直接父节点是 Hips。",
                this);

            enabled = false;
            return;
        }

        // 记录已知参考姿势，不假设当前动画骨架处于 T-Pose
        referenceHipsLocalRotation = hips.localRotation;
        Debug.Log(referenceHipsLocalRotation.eulerAngles);
    }

    private void Update()
    {
        CheckDeath();
        InputUpdate();
        
        SimpleTest();
        
        DelayTimeFunc();
    }

    private bool CheckDeath()
    {
        if (_animaState == AnimaState.Death)
        {
            Hor = 0;
            Ver = 0;
            animator.SetBool(Skill01Id,false);
            animator.SetBool(Skill01EndId,false);
            animator.SetLayerWeight(1,0);
            return true;
        }

        return false;
    }
    private void InputUpdate()
    {
        if (CheckDeath())
        {
            return;
        }
        Hor = Input.GetAxis("Horizontal");
        Ver = Input.GetAxis("Vertical");

        if (FastChange)
        {
            Hor = Input.GetAxisRaw("Horizontal");
            Ver = Input.GetAxisRaw("Vertical");
        }
        animator.SetFloat(
            HorizontalId, Hor);

        animator.SetFloat(
            VerticalId, Ver);
        
     
        if (Input.GetButtonDown("Fire1"))
        {
            animator.SetBool(FireId,true);
            animator.SetLayerWeight(1,1);
            _animaState = AnimaState.Atk;
        }
        if (Input.GetButtonUp("Fire1"))
        {
            animator.SetBool(FireId,false);
            animator.SetLayerWeight(1,0);
            _animaState = AnimaState.Idle;
        }

        if (Input.GetButtonDown("Fire2"))
        {
            animator.SetBool(Skill01Id,true);
            animator.SetLayerWeight(1,1);
            _animaState = AnimaState.Skill01;
        }

        if (Input.GetButtonUp("Fire2"))
        {
            animator.SetBool(Skill01Id,false);
            animator.SetBool(Skill01EndId,true);
            _delayTimeState = DelayTimeState.Skill01Delay;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ReloadTest();
        }
    }

    private void EndSkill()
    {
        animator.SetBool(Skill01EndId,false);
        if (_animaState != AnimaState.Atk)//这里是因为放在技能的时候可能尚未中断就触发攻击
        {
            animator.SetLayerWeight(1,0);
            _animaState = AnimaState.Idle;
        }
    }

    private void DelayTimeFunc()
    {
        switch (_delayTimeState)
        {
            case DelayTimeState.None:
                break;

            case DelayTimeState.Skill01Delay:
                delayTime += Time.deltaTime;
                if (delayTime>Skill01DelayTime)
                {
                    _delayTimeState = DelayTimeState.None;
                    delayTime = 0;
                    EndSkill();
                }
                break;
            default:
                break;
        }
    }
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != compensationLayer ||
            hips == null || spine == null)
        {
            return;
        }
        //尝试修复  半身动画公用骨骼导致的旋转错误
        if (_animaState == AnimaState.Atk || _animaState == AnimaState.Skill01)
        {
            // 在同一次回调中读取当前姿势
            Quaternion currentHips = hips.localRotation;
            Quaternion currentSpine = spine.localRotation;
            Quaternion correction =
                Quaternion.Inverse(currentHips)
                * referenceHipsLocalRotation;

            correction = Quaternion.Slerp(
                Quaternion.identity,
                correction,
                compensationWeight);

            animator.SetBoneLocalRotation(
                HumanBodyBones.Spine,
                correction * currentSpine);

            // 如果目的是显示骨盆实际朝向，使用世界旋转
            if (debugObject != null)
                debugObject.rotation = hips.rotation;
        }
    }


    private void ReloadTest()
    {
        animator.Play(ReloadId);
    }

    private void SimpleTest()
    {
        Vector3 dir = new Vector3(Hor, 0, Ver);
        float speed = Mathf.Min(1, Mathf.Abs(Hor*Hor) + Mathf.Abs(Ver*Ver));
        transform.Translate(dir.normalized * MoveSpeed*speed * Time.deltaTime, Space.World);
    }


    private void OnGUI()
    {
        if (GUILayout.Button("死亡 测试 ",GUILayout.Width(200),GUILayout.Height(100)))
        {
            _animaState = AnimaState.Death;
            animator.SetBool(LiveId,false);
            
        }
        if(GUILayout.Button("复活 测试",GUILayout.Width(200),GUILayout.Height(100)))
        {
            _animaState =  AnimaState.Idle;
            animator.SetBool(LiveId,true);
        }

        if (GUILayout.Button("胜利 测试",GUILayout.Width(200),GUILayout.Height(100)))
        {
            _animaState = AnimaState.Win;
            animator.SetBool(WinId,true);
        }
    
    }
}