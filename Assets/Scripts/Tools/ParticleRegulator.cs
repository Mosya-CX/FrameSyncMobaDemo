using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class ParticleRegulator : MonoBehaviour
{
    private ParticleSystem[] particles;
    public uint startTick;
    public int PrefabId;

    private void Awake()
    {
        List<ParticleSystem> psCache = new();
        GetControlledParticles(transform, ref psCache);
        particles = psCache.ToArray();
    }

    private void GetControlledParticles(Transform root, ref List<ParticleSystem> psCache)
    {
        if (root.TryGetComponent(out ParticleSystem ps))
            psCache.Add(ps);

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.TryGetComponent(out ParticleRegulator _))
                continue;
            GetControlledParticles(child, ref psCache);
        }
    }

    public void CheckCorrect(in uint currentTick, in fp dt, in float correctThreshold)
    {
        float runTime = (float)((currentTick - startTick) * dt);
        for (int i = 0; i < particles.Length; i++)
        {
            if (Mathf.Abs(runTime - particles[i].time) > correctThreshold)
            {
                particles[i].Simulate(Mathf.Clamp01(runTime / particles[i].main.duration));
                particles[i].Play();
            }
        }
    }

    public void ReplayParticles()
    {
        for (int i = 0; i <= particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Play();
        }
    }

    public void StopParticles()
    {
        for (int i = 0; i <= particles.Length; i++)
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
