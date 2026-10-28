using System.Collections.Generic;
using UnityEngine;

namespace Game.Pooling
{
    /// <summary>Marca un objeto como reciclable y recuerda de qué prefab salió.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public GameObject Prefab;
    }

    /// <summary>
    /// Pool de un único prefab. Sin Instantiate/Destroy en runtime:
    /// el primer uso crea, el resto recicla. El objeto nunca se destruye.
    /// </summary>
    public sealed class ObjectPool
    {
        readonly GameObject _prefab;
        readonly Transform _root;
        readonly Stack<PooledObject> _free = new Stack<PooledObject>();

        public ObjectPool(GameObject prefab, Transform root, int prewarm = 0)
        {
            _prefab = prefab;
            _root = root;
            Prewarm(prewarm);
        }

        public int Total { get; private set; }
        public int Active => Total - _free.Count;
        public int Free => _free.Count;

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++) _free.Push(Create());
        }

        PooledObject Create()
        {
            var go = Object.Instantiate(_prefab, _root);
            go.name = _prefab.name + " (pooled)";

            if (!go.TryGetComponent(out PooledObject pooled))
                pooled = go.AddComponent<PooledObject>();
            pooled.Prefab = _prefab;

            go.SetActive(false);
            Total++;
            return pooled;
        }

        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent)
        {
            var pooled = _free.Count > 0 ? _free.Pop() : Create();

            var t = pooled.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            pooled.gameObject.SetActive(true);
            return pooled.gameObject;
        }

        public void Release(PooledObject pooled)
        {
            pooled.gameObject.SetActive(false);
            pooled.transform.SetParent(_root, false);
            _free.Push(pooled);
        }
    }

    /// <summary>
    /// Registro de pools por prefab. Es el único punto de entrada de instanciación
    /// del juego (Spawn/Despawn).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolRegistry : MonoBehaviour
    {
        readonly Dictionary<GameObject, ObjectPool> _pools = new Dictionary<GameObject, ObjectPool>();
        Transform _poolRoot;

        /// <summary>
        /// Raíz propia para los props reciclados: mantiene limpia la jerarquía de la escena.
        /// </summary>
        Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("Pools");
                    go.transform.SetParent(transform, false);
                    _poolRoot = go.transform;
                }
                return _poolRoot;
            }
        }

        public ObjectPool GetPool(GameObject prefab, int prewarm = 0)
        {
            if (prefab == null) return null;
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool(prefab, PoolRoot, prewarm);
                _pools[prefab] = pool;
            }
            else if (prewarm > 0)
            {
                pool.Prewarm(prewarm);
            }
            return pool;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, int prewarm = 0)
        {
            var pool = GetPool(prefab, prewarm);
            return pool == null ? null : pool.Get(position, rotation, parent);
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!instance.TryGetComponent(out PooledObject pooled) || pooled.Prefab == null)
            {
                // Un objeto ajeno al pool nunca se destruye en runtime; se apaga.
                Debug.LogWarning("[Pool] '" + instance.name + "' no viene de un pool. Se desactiva.", instance);
                instance.SetActive(false);
                return;
            }

            GetPool(pooled.Prefab)?.Release(pooled);
        }

        /// <summary>Pre-crea N objetos de un prefab (llamar en carga, no en gameplay).</summary>
        public void Reserve(GameObject prefab, int count)
        {
            GetPool(prefab)?.Prewarm(count);
        }
    }
}
