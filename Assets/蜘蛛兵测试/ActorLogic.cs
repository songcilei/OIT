using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

public enum AttackPart
{
    leftUp,
    rightUp,
    leftDown,
    rightDown,
    None
}


public enum ClawState
{
    Idle,
    Attack,
}

[Serializable]
public class ClawInfo
{
    public GameObject ArmObj;
    public GameObject ClawObj;
    public GameObject ClawEndPoint;
    // [ReadOnly]
    // public int _Index;
    
    public AttackPart attackPart = AttackPart.rightUp;
    public float _ClawLength;
    public GameObject _HideObject;
    public float Rot;
    public float _Cd;
    [ReadOnly]
    public Vector3 _Dir;
    [ReadOnly]
    public ClawState _State;

    private Animator _ArmAnimator;
    private Transform _ArmTransform;
    // private Renderer _ArmRenderer;
    
    private Animator _ClawAnimator;
    private Transform _ClawTransform;
    private Renderer[] _ClawRenderers;



    public void Init(int index)
    {
        if (ArmObj!=null)
        {
            _ArmAnimator = ArmObj.GetComponent<Animator>();
            _ArmTransform = ArmObj.transform;
            // _ArmRenderer = ArmObj.GetComponent<Renderer>();
        }

        if (ClawObj!=null)
        {
            _ClawAnimator = ClawObj.GetComponent<Animator>();
            _ClawTransform = ClawObj.transform;
            _ClawRenderers = ClawObj.GetComponentsInChildren<Renderer>();
        }
        //
        // _Index = index;
        

    }    
    public void SetDir(Quaternion rot)
    {
        _ClawTransform.rotation = rot;
    }

    public void PlayAttack()
    {
        _ClawRenderers.ForEach((x) => { x.enabled = true;});
        _HideObject.SetActive(false);
        _ClawAnimator.SetTrigger("attack");
        _ArmAnimator.SetTrigger("attack");
        _State = ClawState.Attack;
        // 等待攻击完成
        DOVirtual.DelayedCall(_Cd, () =>
        {
            _State = ClawState.Idle;
            _ClawRenderers.ForEach((x) => { x.enabled = false;});
            _HideObject.SetActive(true);
        });
    }
    public void SetClawLength(float length)
    {
        _ClawTransform.localScale = new Vector3(1, 1, length/_ClawLength);
    }

    public Vector3 GetPostion()
    {
        return _ClawTransform.position;
    }

    public void HideClaw()
    {
        _ClawRenderers.ForEach((x) => { x.enabled = false;});
    }

    public Vector3 GetClawEndPoint()
    {
        if (ClawEndPoint!=null)
        {
            return ClawEndPoint.transform.position;
        }

        return Vector3.zero;
    }
}



//-----------------------------------------------------------------------



public class ActorLogic :MonoBehaviour
{
    
    public Animator _Animator;
    // public List<Animator> _ClawAnimators;
    public List<ClawInfo> _ClawInfos;
    public bool EnableRandomClaw = false;
    // public bool IsPlayAttacking = false;

    private AnimatorStateInfo _lastState;
    private Dictionary<AttackPart, int> SortClawIndex = new Dictionary<AttackPart, int>();
    void Awake()
    {
        _Animator = this.GetComponent<Animator>();
    }

    private void Start()
    {

        for (int i = 0; i < _ClawInfos.Count; i++)
        {
            _ClawInfos[i].Init(i);
            _ClawInfos[i].HideClaw();
            SortClawIndex.Add(_ClawInfos[i].attackPart,i);
        }
    }

    private void Update()
    {
        // AnimatorStateInfo _currenState = _Animator.GetCurrentAnimatorStateInfo(0);
        //
        // if (_lastState.fullPathHash != _currenState.fullPathHash && _lastState.normalizedTime>=1.0f)
        // {
        //     IsPlayAttacking = false;
        // }
        
    }

    public void PlayAttack(Vector3 targetPos)
    {
        // if (!IsPlayAttacking)
        // {
        _Animator.SetTrigger("attack");
        //     IsPlayAttacking = true;
        // }
        if (EnableRandomClaw)
        {
            PlayRandomClaw(targetPos);
        }
        else
        {
            foreach (var claw in _ClawInfos)
            {
                PlayClawAnima((int)claw.attackPart,targetPos);
            }
        }
    }
    
    public void PlayAttack(Vector3 pos, AttackPart part)
    {
        // if (!IsPlayAttacking)
        // {
            _Animator.SetTrigger("attack");
        //     IsPlayAttacking = true;
        // }
        if (EnableRandomClaw)
        {
            PlayRandomClaw(pos);
        }
        else
        {
            var clawIndex = GetIndexFromType(part);
            PlayClawAnima(clawIndex,pos);
        }
    }

/// <summary>
/// 返回部件在列表中的index
/// </summary>
/// <param name="part"></param>
/// <returns></returns>
    private int GetIndexFromType(AttackPart part)
    {
        for (int i = 0; i < _ClawInfos.Count; i++)
        {
            if (part == _ClawInfos[i].attackPart) return i;
        }

        return -1;
    }

    /// <summary>
/// 获取当前爪子头挂点位置
/// </summary>
/// <param name="part"></param>
/// <returns></returns>
    public Vector3 GetClawPointPosition(AttackPart part)
    {
        int index = SortClawIndex[part];
        return _ClawInfos[index].GetClawEndPoint();
    }



    private void PlayRandomClaw(Vector3 pos)
    {
        var claw = GetReadlyClaw();
        if (claw == -1) return;
        PlayClawAnima(claw,pos);
    }

    private int GetReadlyClaw()
    {
        int count = 0;
        while (true)
        {
            int index = Random.Range(0, _ClawInfos.Count);
            if (_ClawInfos[index]._State == ClawState.Idle)
            {
                return index;
            }
            count++;
            if (count>20)
            {
                return -1;
            }
        }
    }

    private void PlayClawAnima(int index,Vector3 pos)
    {
        //判断当前爪子状态
        if (_ClawInfos[index]._State == ClawState.Idle)
        {
            var dir = pos - _ClawInfos[index].GetPostion();

            var roll = Quaternion.AngleAxis(_ClawInfos[index].Rot, dir.normalized);
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
           
            float distance = Vector3.Distance(pos, _ClawInfos[index].GetPostion());
            _ClawInfos[index].SetClawLength(distance);
            _ClawInfos[index].SetDir(roll*lookRot);
            _ClawInfos[index].PlayAttack();
        }
    }


}
