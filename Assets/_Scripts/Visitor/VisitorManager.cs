using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarHaven.Visitor
{
    /// <summary>
    /// 旅客管理器（单例）。
    /// 负责旅客对象池、生成、回收、事件派发与查询。
    /// </summary>
    public class VisitorManager : MonoBehaviour
    {
        #region 单例

        private static VisitorManager _instance;

        /// <summary>
        /// 单例实例。
        /// </summary>
        public static VisitorManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VisitorManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("VisitorManager");
                        _instance = go.AddComponent<VisitorManager>();
                    }
                }

                return _instance;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 旅客到达事件。
        /// </summary>
        public event Action<Visitor> OnVisitorArrived;

        /// <summary>
        /// 旅客离开事件。
        /// </summary>
        public event Action<Visitor> OnVisitorDeparted;

        /// <summary>
        /// 旅客服务完成事件（附带满意度）。
        /// </summary>
        public event Action<Visitor, float> OnVisitorServed;

        #endregion

        #region 配置

        [Header("生成设置")]
        [SerializeField, Tooltip("场景中允许同时存在的最大旅客数量")]
        private int maxActiveVisitors = 20;

        [SerializeField, Tooltip("是否启用自动生成旅客")]
        private bool enableAutoSpawn = false;

        [SerializeField, Tooltip("自动生成间隔（秒）")]
        private float autoSpawnInterval = 5f;

        [Header("配置列表")]
        [SerializeField, Tooltip("可用于随机生成的旅客配置")]
        private List<VisitorConfig> visitorConfigs = new List<VisitorConfig>();

        [Header("对象池")]
        [SerializeField, Tooltip("启动时预热的旅客实例数量")]
        private int initialPoolSize = 8;

        [SerializeField, Tooltip("旅客对象的父节点（为空则使用管理器节点）")]
        private Transform visitorRoot;

        #endregion

        #region 运行时数据

        private readonly List<Visitor> _activeVisitors = new List<Visitor>(64);
        private readonly Stack<Visitor> _inactivePool = new Stack<Visitor>(64);
        private readonly Dictionary<int, Visitor> _visitorById = new Dictionary<int, Visitor>(128);
        private readonly Dictionary<VisitorType, List<Visitor>> _visitorsByType = new Dictionary<VisitorType, List<Visitor>>();

        private float _spawnTimer;
        private int _nextVisitorId = 1;
        private bool _isInitialized;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateAllVisitors(deltaTime);

            if (!enableAutoSpawn)
            {
                return;
            }

            _spawnTimer += deltaTime;
            if (_spawnTimer >= autoSpawnInterval)
            {
                _spawnTimer = 0f;
                SpawnRandomVisitor();
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化管理器与对象池。
        /// </summary>
        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            if (visitorRoot == null)
            {
                visitorRoot = transform;
            }

            _visitorsByType.Clear();
            Array types = Enum.GetValues(typeof(VisitorType));
            for (int i = 0; i < types.Length; i++)
            {
                VisitorType type = (VisitorType)types.GetValue(i);
                _visitorsByType[type] = new List<Visitor>(16);
            }

            for (int i = 0; i < Mathf.Max(0, initialPoolSize); i++)
            {
                Visitor visitor = CreateVisitorInstance();
                visitor.gameObject.SetActive(false);
                _inactivePool.Push(visitor);
            }

            _isInitialized = true;
            Debug.Log("✅ VisitorManager 初始化完成");
        }

        #endregion

        #region 旅客池管理

        /// <summary>
        /// 按配置生成一个旅客实例。
        /// </summary>
        /// <param name="config">旅客配置。</param>
        /// <returns>生成后的旅客；若失败返回 null。</returns>
        public Visitor SpawnVisitor(VisitorConfig config)
        {
            if (config == null)
            {
                Debug.LogError("SpawnVisitor 失败：config 为空。");
                return null;
            }

            if (_activeVisitors.Count >= maxActiveVisitors)
            {
                Debug.LogWarning("旅客数量已达上限，跳过生成。");
                return null;
            }

            Visitor visitor = (_inactivePool.Count > 0) ? _inactivePool.Pop() : CreateVisitorInstance();
            visitor.gameObject.SetActive(true);
            visitor.transform.SetParent(visitorRoot, false);

            int visitorId = _nextVisitorId++;
            visitor.Initialize(config, visitorId);
            BindVisitorEvents(visitor);

            _activeVisitors.Add(visitor);
            _visitorById[visitorId] = visitor;

            if (!_visitorsByType.TryGetValue(config.type, out List<Visitor> list))
            {
                list = new List<Visitor>(8);
                _visitorsByType[config.type] = list;
            }
            list.Add(visitor);

            OnVisitorArrived?.Invoke(visitor);
            return visitor;
        }

        /// <summary>
        /// 回收一个旅客实例到对象池。
        /// </summary>
        /// <param name="visitor">旅客实例。</param>
        public void DespawnVisitor(Visitor visitor)
        {
            InternalDespawnVisitor(visitor, true);
        }

        /// <summary>
        /// 获取当前激活旅客数量。
        /// </summary>
        public int GetActiveVisitorCount()
        {
            return _activeVisitors.Count;
        }

        #endregion

        #region 旅客生成

        /// <summary>
        /// 从配置列表随机生成一个旅客。
        /// </summary>
        /// <returns>生成后的旅客；若失败返回 null。</returns>
        public Visitor SpawnRandomVisitor()
        {
            if (visitorConfigs == null || visitorConfigs.Count == 0)
            {
                Debug.LogWarning("SpawnRandomVisitor 失败：未配置 VisitorConfig 列表。");
                return null;
            }

            if (_activeVisitors.Count >= maxActiveVisitors)
            {
                return null;
            }

            int index = UnityEngine.Random.Range(0, visitorConfigs.Count);
            VisitorConfig config = visitorConfigs[index];
            return SpawnVisitor(config);
        }

        #endregion

        #region 旅客更新

        /// <summary>
        /// 每帧更新所有旅客（管理器层面的维护）。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间。</param>
        public void UpdateAllVisitors(float deltaTime)
        {
            for (int i = _activeVisitors.Count - 1; i >= 0; i--)
            {
                Visitor visitor = _activeVisitors[i];
                if (visitor == null)
                {
                    _activeVisitors.RemoveAt(i);
                }
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 按 ID 获取旅客。
        /// </summary>
        /// <param name="id">旅客 ID。</param>
        /// <returns>旅客实例；若不存在返回 null。</returns>
        public Visitor GetVisitorById(int id)
        {
            _visitorById.TryGetValue(id, out Visitor visitor);
            return visitor;
        }

        /// <summary>
        /// 获取指定类型的所有激活旅客。
        /// </summary>
        /// <param name="type">旅客类型。</param>
        /// <returns>旅客列表副本。</returns>
        public List<Visitor> GetAllVisitorsByType(VisitorType type)
        {
            if (!_visitorsByType.TryGetValue(type, out List<Visitor> list))
            {
                return new List<Visitor>(0);
            }

            return new List<Visitor>(list);
        }

        /// <summary>
        /// 清空所有激活旅客并回收至对象池。
        /// </summary>
        public void ClearAllVisitors()
        {
            for (int i = _activeVisitors.Count - 1; i >= 0; i--)
            {
                InternalDespawnVisitor(_activeVisitors[i], false);
            }

            _activeVisitors.Clear();
            _visitorById.Clear();

            foreach (KeyValuePair<VisitorType, List<Visitor>> kvp in _visitorsByType)
            {
                kvp.Value.Clear();
            }
        }

        #endregion

        #region 调试

        /// <summary>
        /// 输出当前旅客运行状态。
        /// </summary>
        [ContextMenu("输出旅客状态")]
        private void DebugVisitorStatus()
        {
            Debug.Log("===== 旅客状态 =====");
            Debug.Log($"激活数量：{_activeVisitors.Count}/{maxActiveVisitors}");
            Debug.Log($"池中数量：{_inactivePool.Count}");

            foreach (KeyValuePair<VisitorType, List<Visitor>> kvp in _visitorsByType)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value.Count}");
            }

            Debug.Log("===================");
        }

        #endregion

        #region 内部实现

        private Visitor CreateVisitorInstance()
        {
            GameObject go = new GameObject("Visitor");
            go.transform.SetParent(visitorRoot != null ? visitorRoot : transform, false);
            return go.AddComponent<Visitor>();
        }

        private void BindVisitorEvents(Visitor visitor)
        {
            visitor.OnServiceCompleted += HandleVisitorServed;
            visitor.OnLeave += HandleVisitorLeave;
        }

        private void UnbindVisitorEvents(Visitor visitor)
        {
            visitor.OnServiceCompleted -= HandleVisitorServed;
            visitor.OnLeave -= HandleVisitorLeave;
        }

        private void HandleVisitorServed(Visitor visitor)
        {
            OnVisitorServed?.Invoke(visitor, visitor != null ? visitor.Satisfaction : 0f);
        }

        private void HandleVisitorLeave(Visitor visitor)
        {
            InternalDespawnVisitor(visitor, true);
        }

        private void InternalDespawnVisitor(Visitor visitor, bool invokeEvent)
        {
            if (visitor == null)
            {
                return;
            }

            if (!_activeVisitors.Remove(visitor))
            {
                return;
            }

            UnbindVisitorEvents(visitor);
            _visitorById.Remove(visitor.Id);

            VisitorConfig config = visitor.Config;
            if (config != null && _visitorsByType.TryGetValue(config.type, out List<Visitor> sameTypeList))
            {
                sameTypeList.Remove(visitor);
            }

            visitor.SetState(VisitorState.Left);
            visitor.gameObject.SetActive(false);
            visitor.transform.SetParent(visitorRoot, false);
            _inactivePool.Push(visitor);

            if (invokeEvent)
            {
                OnVisitorDeparted?.Invoke(visitor);
            }
        }

        #endregion
    }
}
