using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum DelayTimeState
{
    None,
    AtkDelay,
    Skill01AtkDelay,
    Skill01RockDelay,
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
public class AnimaCoreLogic : MonoBehaviour
{
    
    [Range(0f, 1f)]
    public float compensationWeight = 1f;

    public int compensationLayer = 1;
    public Transform debugObject;

    public float LeftHandIK = 1;
    public float RightHandIK = 1;
    
    private Transform hips;
    private Transform spine;

    private Quaternion referenceHipsLocalRotation;
    private bool EnableHalfMode = false;
    private static readonly int HorizontalId = Animator.StringToHash("Horizontal");
    private static readonly int VerticalId = Animator.StringToHash("Vertical");
    private static readonly int FireId = Animator.StringToHash("Fire");
    private static readonly int Skill01Id = Animator.StringToHash("Skill01");
    private static readonly int WinId = Animator.StringToHash("Win");
    private static readonly int LiveId = Animator.StringToHash("Live");
    private static readonly int DeathId = Animator.StringToHash("Death");
    private static readonly int ReloadId = Animator.StringToHash("Reload");
    private static readonly int Skill01EndId = Animator.StringToHash("Skill01End");

    private Animator _animator;

    private void Start()
    {
        _animator = this.GetComponent<Animator>();
        if (!_animator.isHuman )
        {
            Debug.LogError("需要 Humanoid Animator 和参考姿势 Hips。", this);
            enabled = false;
            return;
        }

        hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = _animator.GetBoneTransform(HumanBodyBones.Spine);

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
    }

    public void PlayAtk()
    {
        _animator.SetTrigger(FireId);
    }



    public void SetMove(float Hor,float Ver)
    {
        _animator.SetFloat(
            HorizontalId, Hor);

        _animator.SetFloat(
            VerticalId, Ver);
    }
    
    public void PlayWin()
    {
        _animator.SetTrigger(WinId);
    }
    public void PlayDeath()
    {
        _animator.SetTrigger(DeathId);
    }
    public void PlayReload()
    {
        _animator.SetTrigger(ReloadId);
    }
    public void PlaySkill01End()
    {
        _animator.SetTrigger(Skill01EndId);
    }
    public void PlaySkill01()
    {
        _animator.SetTrigger(Skill01Id);
    }
    
    public void PlayLive()
    {
        _animator.SetTrigger(LiveId);
    }

    // 设置半身动画模式，true为开启，false为关闭
    public void SetHalfAnimaMode(bool state)
    {
        _animator.SetLayerWeight(1,state?1:0);
        EnableHalfMode = state;
    }
    
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != compensationLayer ||
            hips == null || spine == null)
        {
            return;
        }
        
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, LeftHandIK);
        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, RightHandIK);
        
        
        //尝试修复  半身动画公用骨骼导致的旋转错误
        if (EnableHalfMode)
        {
            Debug.Log("11111111111");
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

            _animator.SetBoneLocalRotation(
                HumanBodyBones.Spine,
                correction * currentSpine);

            // 如果目的是显示骨盆实际朝向，使用世界旋转
            if (debugObject != null)
                debugObject.rotation = hips.rotation;
        }
    }
    
}
