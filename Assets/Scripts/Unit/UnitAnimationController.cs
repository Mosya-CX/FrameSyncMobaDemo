using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class UnitAnimationController : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    private UnitCore owner;

    [SerializeField, LabelText("待机动画"), FoldoutGroup("基础动作动画")]
    private AnimationClip Idle;
    [SerializeField, LabelText("移动动画"), FoldoutGroup("基础动作动画")]
    private AnimationClip Move;
    [SerializeField, LabelText("攻击动画"), FoldoutGroup("基础动作动画")]
    private List<AnimationClip> Attack;
    [SerializeField, LabelText("死亡动画"), FoldoutGroup("基础动作动画")]
    private AnimationClip Dead;
    [SerializeField, LabelText("僵直动画"), FoldoutGroup("基础动作动画")]
    private AnimationClip Siffness;

    [SerializeField, LabelText("技能动作动画")]
    private List<UnitAbilityAction> AbilityActionAnimationGroups;

    [System.Serializable]
    public class UnitAbilityAction
    {
        public string Name;
        public AnimationClip Precast;// 前摇动画
        public AnimationClip Channeling;// 引导动画
        public AnimationClip Recovert;// 后摇动画
    }
    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }






}
