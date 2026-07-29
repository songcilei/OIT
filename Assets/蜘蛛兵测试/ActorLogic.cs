using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ClawState
{
    Idle,
    Attack,
}

[Serializable]
public class ClawInfo
{
    public Animator _Animator;
    public Transform _Transform;
    public int _Index;
    public ClawState _State;
    public float _Cd;
    [ReadOnly]
    public Vector3 _Dir;
    public float _ClawLength;
    
    public void SetDir(Vector3 dir)
    {
        _Animator.gameObject.transform.forward = dir.normalized;
    }

    public void PlayAttack()
    {
        _Animator.SetTrigger("attack");
        _State = ClawState.Attack;
        // 等待攻击完成
        DOVirtual.DelayedCall(_Cd, () =>
        {
            _State = ClawState.Idle;
        });
    }
    public void SetClawLength(float length)
    {
        _Transform.localScale = new Vector3(1, 1, length/_ClawLength);
    }
}

public class ActorLogic :MonoBehaviour
{
    
    public Animator _Animator;
    public int _ClawIndex;
    // public List<Animator> _ClawAnimators;
    public List<ClawInfo> _ClawInfos;
    void Awake()
    {
        _Animator = this.GetComponent<Animator>();
        // _ClawInfos = new List<ClawInfo>();
        // for (int i = 0; i < _ClawAnimators.Count; i++)
        // {
        //     ClawInfo clawInfo = new ClawInfo();
        //     clawInfo._Animator = _ClawAnimators[i];
        //     clawInfo._Index = i;
        //     clawInfo._Dir = Vector3.zero;
        //     _ClawInfos.Add(clawInfo);
        // }
    }

    public void PlayAttack(Vector3 pos)
    {
        var dir = pos - transform.position;
        float dist = Vector3.Distance(pos, transform.position);
        _Animator.SetTrigger("attack");
        PlayClawAnima(_ClawIndex, dir,dist);
    }
    
    [Button(ButtonSizes.Gigantic)]
    public void PlayAttack()
    {
        _Animator.SetTrigger("attack");
        PlayClawAnima(_ClawIndex, new Vector3(0,0,0),9);
    }


    private void PlayClawAnima(int index,Vector3 dir,float distance)
    {
        //判断当前爪子状态
        if (_ClawInfos[index]._State == ClawState.Idle)
        {
            _ClawInfos[index].SetClawLength(distance);
            _ClawInfos[index].SetDir(dir);
            _ClawInfos[index].PlayAttack();
        }
    }
}
