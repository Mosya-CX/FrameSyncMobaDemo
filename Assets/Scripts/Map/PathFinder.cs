using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RVO;
using Sirenix.OdinInspector;

public class PathFinder : MonoBehaviour
{
    private UnitCore core;

    [SerializeField, LabelText("自身流场价值")]
    private int flowFieldValue = 100;

    #region A寻路部分
    // TODO
    // 存储当前路径

    // 根据路径、当前位置和当前速度获得当前移动方向 

    // 根据目的地和当前位置更新路径

    #endregion

    #region 流场寻路部分
    // TODO
    // 根据当前位置和周遭格子的价值获取当前移动方向

    // 给根据当前位置更新当前格子的Modifier(即仅给当前格子的cost增加flowFieldValue)

    #endregion
}
