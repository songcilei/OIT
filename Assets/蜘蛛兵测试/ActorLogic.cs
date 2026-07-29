using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;


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
    public Renderer _Renderer;
    public int _Index;
    public ClawState _State;
    public float _Cd;
    [ReadOnly]
    public Vector3 _Dir;
    public float _ClawLength;
    public GameObject _HideObject;

    
    public void SetDir(Vector3 dir)
    {
        _Animator.gameObject.transform.forward = dir.normalized;
    }

    public void PlayAttack()
    {
        _Renderer.enabled = true;
        _HideObject.SetActive(false);
        _Animator.SetTrigger("attack");
        _State = ClawState.Attack;
        // 等待攻击完成
        DOVirtual.DelayedCall(_Cd, () =>
        {
            _State = ClawState.Idle;
            _Renderer.enabled = false;
            _HideObject.SetActive(true);
        });
    }
    public void SetClawLength(float length)
    {
        _Transform.localScale = new Vector3(1, 1, length/_ClawLength);
    }
}



//-----------------------------------------------------------------------



public class ActorLogic :MonoBehaviour
{
    
    public Animator _Animator;
    public int _ClawIndex;
    // public List<Animator> _ClawAnimators;
    public List<ClawInfo> _ClawInfos;
    public bool EnableRandomClaw = false;
    void Awake()
    {
        _Animator = this.GetComponent<Animator>();
    }

    private void Start()
    {
        for (int i = 0; i < _ClawInfos.Count; i++)
        {
            _ClawInfos[i]._Renderer.enabled = false;
        }
    }

    public void PlayAttack(Vector3 pos)
    {
        _Animator.SetTrigger("attack");
        if (EnableRandomClaw)
        {
            PlayRandomClaw(pos);
        }
        else
        {
            PlayClawAnima(0,pos);
            PlayClawAnima(1,pos);
            PlayClawAnima(2,pos);
            PlayClawAnima(3,pos);
        }
    }
    
    // [Button(ButtonSizes.Gigantic)]
    // public void PlayAttack()
    // {
    //     _Animator.SetTrigger("attack");
    //     PlayClawAnima(_ClawIndex, );
    // }


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
            var dir = pos - _ClawInfos[index]._Transform.position;
            float distance = Vector3.Distance(pos, _ClawInfos[index]._Transform.position);
            _ClawInfos[index].SetClawLength(distance);
            _ClawInfos[index].SetDir(dir);
            _ClawInfos[index].PlayAttack();
        }
    }
}
