using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "CompositeBuffModule", menuName = "SkillSystem/Buff/Composite Buff Module")]
public sealed class CompositeBuffModule : BuffBaseModule
{
    [LabelText("子模块")]
    public BuffBaseModule[] Modules;

    public override void Apply(BuffCallbackContext context)
    {
        if (Modules == null)
            return;

        for (int i = 0; i < Modules.Length; i++)
        {
            var module = Modules[i];
            if (module == null)
                continue;

            module.Apply(context);
        }
    }
}
