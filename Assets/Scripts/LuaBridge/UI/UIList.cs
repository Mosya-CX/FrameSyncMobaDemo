using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// List reuse host (UI design v9.1 7.2/7.4): creates the required UICell
    /// instances, reuses them, calls SetIndex/Bind, hides extras.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIList : MonoBehaviour
    {
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Transform container;

        private readonly List<UICell> _cells =
            new List<UICell>();
        private LuaManager _manager;

        public void SetManager(LuaManager manager)
        {
            _manager = manager;
        }

        public void SetItems(LuaTable items)
        {
            int count =
                items != null ? items.Length : 0;
            EnsureCells(count);
            for (int i = 0; i < _cells.Count; i++)
            {
                if (i >= count)
                {
                    _cells[i].gameObject
                        .SetActive(false);
                    continue;
                }
                _cells[i].gameObject
                    .SetActive(true);
                _cells[i].SetIndex(i);
                _cells[i].Bind(
                    items.Get<LuaTable>(i + 1));
            }
        }

        private void EnsureCells(int count)
        {
            if (cellPrefab == null)
                return;
            Transform parent =
                container != null
                    ? container
                    : transform;
            while (_cells.Count < count)
            {
                GameObject instance =
                    Instantiate(
                        cellPrefab,
                        parent,
                        false);
                if (instance.scene != gameObject.scene)
                    UnityEngine.SceneManagement
                        .SceneManager.MoveGameObjectToScene(
                            instance,
                            gameObject.scene);
                instance.name =
                    $"{cellPrefab.name}_{_cells.Count}";
                UICell cell =
                    instance.GetComponent<UICell>();
                if (cell == null)
                {
                    Destroy(instance);
                    break;
                }
                cell.Build(_manager);
                _cells.Add(cell);
            }
        }
    }
}
