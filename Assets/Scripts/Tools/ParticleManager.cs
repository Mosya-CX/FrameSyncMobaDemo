using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Pool;

public class ParticleManager : MonoSingleton<ParticleManager>
{
    [SerializeField, LabelText("Á£×ÓÐÞÕýãÐÖµ"), Range(0.01f, 0.2f)]
    public float ParticleCorrectThreshold = 0.1f;

    private readonly Dictionary<int, ObjectPool<GameObject>> particleFactory = new();
    private readonly List<ParticleRuntime> spawnedParticles = new();

    private uint localTick;

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    public void CreateParticleControlHandleRequest(GameObject prefab, Vector3 position, Quaternion rotation, fp duration)
    {
        var prefabId = prefab.GetInstanceID();
        if (!TryGetPool(prefabId, out var pool))
            pool = CreateNewPool(prefab);

        if (pool == null)
            return;

        var instance = pool.Get();
        instance.transform.position = position;
        instance.transform.rotation = rotation;

        if (!instance.TryGetComponent(out ParticleRegulator regulator))
            regulator = instance.AddComponent<ParticleRegulator>();

        regulator.startTick = localTick + 1;
        regulator.PrefabId = prefabId;

        spawnedParticles.Add(new ParticleRuntime(regulator, duration));
    }

    public void DespawnParticle(int prefabId, GameObject instance)
    {
        if (particleFactory.TryGetValue(prefabId, out var pool))
            pool.Release(instance);
        else
            Destroy(instance.gameObject);
    }

    private bool TryGetPool(int prefabId, out ObjectPool<GameObject> pool)
    {
        return particleFactory.TryGetValue(prefabId, out pool);
    }

    private ObjectPool<GameObject> CreateNewPool(GameObject prefab)
    {
        if (particleFactory.ContainsKey(prefab.gameObject.GetInstanceID()))
            return particleFactory[prefab.GetInstanceID()];

        ObjectPool<GameObject> pool = new(
            () => Instantiate(prefab),
            (obj) => obj.gameObject.SetActive(true),
            (obj) => obj.gameObject.SetActive(false),
            (obj) => Destroy(obj),
            false, 5, 30);

        particleFactory.Add(prefab.GetInstanceID(), pool);

        return pool;
    }

    public void Tick(uint currentTick)
    {
        localTick = currentTick;
        
        for (int i = spawnedParticles.Count - 1; i >= 0; i--)
        {
            var runtime = spawnedParticles[i];
            runtime.timer -= DeltaTime;
            if (runtime.timer <= 0)
            {
                if (TryGetPool(runtime.instance.PrefabId, out var pool))
                    pool.Release(runtime.instance.gameObject);
                else
                    Destroy(runtime.instance.gameObject);

                spawnedParticles.RemoveAt(i);
            }
        }
    }

    [Serializable]
    public class ParticleRuntime
    {
        public ParticleRegulator instance;
        public fp timer;

        public ParticleRuntime(ParticleRegulator particle, fp duration)
        {
            instance = particle;
            timer = duration;
        }
    }

    public void Clean()
    {
        for (int i = 0; i < spawnedParticles.Count; i++)
        {
            var runtime = spawnedParticles[i];
            if (TryGetPool(runtime.instance.PrefabId, out var pool))
                pool.Release(runtime.instance.gameObject);
            else
                Destroy(runtime.instance.gameObject);
        }
        spawnedParticles.Clear();
    }

    public void Rollback(uint rollbackTick)
    {
        for (int i = spawnedParticles.Count - 1; i >= 0; i--)
        {
            var runtime = spawnedParticles[i];
            if (runtime.instance.startTick >= rollbackTick)
            {
                if (TryGetPool(runtime.instance.PrefabId, out var pool))
                    pool.Release(runtime.instance.gameObject);
                else
                    Destroy(runtime.instance.gameObject);
            }
        }
    }

    public void Correct(uint currentTick)
    {
        for (int i = 0; i < spawnedParticles.Count; i++)
            spawnedParticles[i].instance.CheckCorrect(currentTick, DeltaTime, ParticleCorrectThreshold);
    }
}