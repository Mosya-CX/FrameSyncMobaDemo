using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public abstract class NetworkSingleton<T> : NetworkBehaviour where T : NetworkSingleton<T>
{
    [SerializeField, LabelText("是否在切换场景时不销毁")]
    private bool isDestoryOnUnloadScene;

    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindObjectOfType<T>();
            if (_instance != null) return _instance;

            // 仅服务器可创建网络对象
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                GameObject go = new GameObject($"{typeof(T).Name} (Network Singleton)");
                T newInstance = go.AddComponent<T>();
                go.AddComponent<NetworkObject>();
                newInstance.NetworkObject.Spawn();
                _instance = newInstance;
            }
            else
            {
                Debug.LogWarning($"[NetworkSingleton] 客户端无法创建 {typeof(T).Name}，请确保服务器已生成。");
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            HandleDuplicate();
            return;
        }

        _instance = this as T;
        if (!isDestoryOnUnloadScene)
            DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (_instance == null)
            _instance = this as T;
        else if (_instance != this)
        {
            if (NetworkManager.Singleton.IsServer)
                NetworkObject.Despawn();
            else
                gameObject.SetActive(false);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (_instance == this) _instance = null;
    }

    private void HandleDuplicate()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (NetworkManager.Singleton.IsServer)
                NetworkObject.Despawn();
            else
                gameObject.SetActive(false);
        }
        else
            Destroy(gameObject);
    }
}
