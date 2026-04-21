using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Pool;

public class AudioManager : MonoSingleton<AudioManager>
{
    private AudioSource template;
    private ObjectPool<AudioSource> audioSourcePool;

    private List<AudioRuntime> spawnedAudios;

    private uint localTick;

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    protected override void Awake()
    {
        base.Awake();
        template = GetComponent<AudioSource>();
        template.loop = false;
        audioSourcePool = new ObjectPool<AudioSource>(
            () => new GameObject("音频播放器").AddComponent<AudioSource>(),
            (audioSource) => 
            { 
                audioSource.gameObject.SetActive(true);
                CopyAudioSource(audioSource, template);
            },
            (audioSource) =>
            {
                audioSource.gameObject.SetActive(false);
                CopyAudioSource(audioSource, template);
            },
            (audioSource) => Destroy(audioSource.gameObject),
            false, 10, 100);
    }

    public void CreateAudioRequest(AudioClip clip)
    {
        var source = audioSourcePool.Get();
        source.clip = clip;

        spawnedAudios.Add(new AudioRuntime
        {
            source = source,
            startTick = localTick + 1,
            timer = (fp)clip.length,
        });

        source.Play();
    }

    public void CreateLoopAudioRequest(AudioClip clip, fp loopDuration)
    {
        var source = audioSourcePool.Get();
        source.loop = true;
        source.clip = clip;

        spawnedAudios.Add(new AudioRuntime
        {
            source = source,
            startTick = localTick + 1,
            timer = loopDuration,
        });

        source.Play();
    }

    public void Tick(uint currentTick)
    {
        localTick = currentTick;

        for (int i = spawnedAudios.Count - 1; i >= 0; i--)
        {
            spawnedAudios[i].timer -= DeltaTime;
            if (spawnedAudios[i].timer <= 0)
            {
                spawnedAudios[i].source.Stop();
                audioSourcePool.Release(spawnedAudios[i].source);
                spawnedAudios.RemoveAt(i);
            }
        }
    }

    public void Clean()
    {
        for (int i = 0; i < spawnedAudios.Count; i++)
        {
            spawnedAudios[i].source.Stop();
            audioSourcePool.Release(spawnedAudios[i].source);
        }
        spawnedAudios.Clear();
    }

    public void Rollback(uint rollbackTick)
    {
        for (int i = spawnedAudios.Count - 1; i >= 0; i--)
        {
            if (spawnedAudios[i].startTick >= rollbackTick)
            {
                spawnedAudios[i].source.Stop();
                audioSourcePool.Release(spawnedAudios[i].source);
                spawnedAudios.RemoveAt(i);
            }
        }
    }

    public void Correct(uint currentTick)
    {
        for (int i = 0; i < spawnedAudios.Count; i++)
        {
            var source = spawnedAudios[i].source;
            var playDuration = (float)((currentTick - spawnedAudios[i].startTick) * DeltaTime);
            source.time = source.loop ? playDuration % source.clip.length : playDuration;
        }
    }

    public void CopyAudioSource(AudioSource target, AudioSource source)
    {
        // 基本设置
        target.clip = source.clip;
        target.outputAudioMixerGroup = source.outputAudioMixerGroup;
        target.mute = source.mute;
        target.bypassEffects = source.bypassEffects;
        target.bypassListenerEffects = source.bypassListenerEffects;
        target.bypassReverbZones = source.bypassReverbZones;
        target.playOnAwake = source.playOnAwake;
        target.loop = source.loop;

        // 3D 声音设置
        target.spatialBlend = source.spatialBlend;
        target.spatialize = source.spatialize;
        target.spatializePostEffects = source.spatializePostEffects;
        target.reverbZoneMix = source.reverbZoneMix;
        target.dopplerLevel = source.dopplerLevel;
        target.spread = source.spread;
        target.rolloffMode = source.rolloffMode;
        target.minDistance = source.minDistance;
        target.maxDistance = source.maxDistance;

        // 其他常用属性
        target.priority = source.priority;
        target.volume = source.volume;
        target.pitch = source.pitch;
        target.panStereo = source.panStereo;
    }

    [System.Serializable]
    public class AudioRuntime
    {
        public AudioSource source;
        public fp timer;
        public uint startTick;
    }
}
