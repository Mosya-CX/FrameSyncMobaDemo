using UnityEngine;

// 过渡占位文件。
// 你既然已经决定砍掉 SkillValueDef，这个文件最终应删除。
// 这里保留一个空壳只是为了先消掉编译错误。
public sealed class FixedSkillValueDef : ScriptableObject
{
    public float Value = 0f;
}
