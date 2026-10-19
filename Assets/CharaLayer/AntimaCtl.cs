using System;
using UnityEngine;





[RequireComponent(typeof(Animator),typeof(AnimaCoreLogic))]
public class AntimaCtl : MonoBehaviour
{

    public float MoveSpeed = 1;
    public bool FastChange = false;
    


    public float AtkDelayTime = 1;

    public float Skill01AtkDelayTime = 1;
    
    public float Skill01RockDelayTime = 1;
    
    public AnimaState _animaState = AnimaState.Idle;
    private DelayTimeState _delayTimeState = DelayTimeState.None;
    private float delayTime = 0;
    private Animator animator;

    private float Hor = 0;
    private float Ver = 0;
    AnimaCoreLogic _animaCoreLogic;


    private void Start()
    {
        _animaCoreLogic = GetComponent<AnimaCoreLogic>();
        animator = GetComponent<Animator>();


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
            _animaCoreLogic.SetHalfAnimaMode(false);
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
//move        
        Hor = Input.GetAxis("Horizontal");
        Ver = Input.GetAxis("Vertical");

        if (FastChange)
        {
            Hor = Input.GetAxisRaw("Horizontal");
            Ver = Input.GetAxisRaw("Vertical");
        }
        _animaCoreLogic.SetMove(Hor,Ver);
     
        
//atk
        if (Input.GetButtonDown("Fire1"))
        {
            _animaCoreLogic.PlayAtk();
            _animaCoreLogic.SetHalfAnimaMode(true);
            _animaState = AnimaState.Atk;
            _delayTimeState = DelayTimeState.AtkDelay;
            delayTime = 0;//强制重置延迟时间
        }

        if (Input.GetButtonDown("Fire2"))
        {
            _animaCoreLogic.PlaySkill01();
            _animaCoreLogic.SetHalfAnimaMode(true);
            delayTime = 0;
            _animaState = AnimaState.Skill01;
            _delayTimeState = DelayTimeState.Skill01AtkDelay;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            _animaCoreLogic.PlayReload();
        }
    }

    private void EndSkill()
    {
        if (_animaState != AnimaState.Atk)//这里是因为放在技能的时候可能尚未中断就触发攻击
        {
            _animaState = AnimaState.Idle;
            _animaCoreLogic.SetHalfAnimaMode(false);
        }
    }

    private void DelayTimeFunc()
    {
        switch (_delayTimeState)
        {
            case DelayTimeState.None:
                break;
            case DelayTimeState.Skill01AtkDelay:
                delayTime += Time.deltaTime;
                if (delayTime > Skill01AtkDelayTime)
                {
                    _delayTimeState = DelayTimeState.Skill01RockDelay;
                    delayTime = 0;
                    _animaCoreLogic.PlaySkill01End();
                }
                break;
            case DelayTimeState.Skill01RockDelay:
                delayTime += Time.deltaTime;
                if (delayTime>Skill01RockDelayTime)
                {
                    _delayTimeState = DelayTimeState.None;
                    delayTime = 0;
                    EndSkill();
                }
                break;
            case DelayTimeState.AtkDelay:
                delayTime += Time.deltaTime;
                if (delayTime > AtkDelayTime)
                {
                    _delayTimeState = DelayTimeState.None;
                    _animaState = AnimaState.Idle;
                    _animaCoreLogic.SetHalfAnimaMode(false);
                    delayTime = 0;
                }
                break;
            default:
                break;
        }
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
            _animaCoreLogic.PlayDeath();
        }
        if(GUILayout.Button("复活 测试",GUILayout.Width(200),GUILayout.Height(100)))
        {
            _animaState =  AnimaState.Idle;
            _animaCoreLogic.PlayLive();
        }

        if (GUILayout.Button("胜利 测试",GUILayout.Width(200),GUILayout.Height(100)))
        {
            _animaState = AnimaState.Win;
            _animaCoreLogic.PlayWin();
        }
    
    }
}