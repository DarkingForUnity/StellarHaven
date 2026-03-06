using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace StellarHaven.Core
{
    /// <summary>
    /// 资源管理器 - 单例模式
    /// 负责资源的加载、卸载、缓存和异步加载
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        #region 单例模式
        
        private static ResourceManager _instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static ResourceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ResourceManager>();
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ResourceManager");
                        _instance = go.AddComponent<ResourceManager>();
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region 资源缓存
        
        /// <summary>
        /// 已加载的资源缓存 (路径 -> 资源)
        /// </summary>
        private Dictionary<string, Object> _resourceCache = new Dictionary<string, Object>();
        
        /// <summary>
        /// 异步加载任务
        /// </summary>
        private Dictionary<string, ResourceRequest> _asyncLoadTasks = new Dictionary<string, ResourceRequest>();

        /// <summary>
        /// 异步加载回调队列（同一路径合并）
        /// </summary>
        private Dictionary<string, List<System.Action<Object>>> _asyncCallbacks = new Dictionary<string, List<System.Action<Object>>>();
        
        #endregion
        
        #region Unity 生命周期
        
        private void Awake()
        {
            // 确保单例唯一性
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("📦 ResourceManager 初始化完成！");
        }
        
        private void OnDestroy()
        {
            // 清理所有缓存资源
            ClearAllCache();
        }
        
        #endregion
        
        #region 同步加载
        
        /// <summary>
        /// 加载资源 (带缓存)
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径 (相对于 Resources 文件夹)</param>
        /// <returns>资源实例</returns>
        public T LoadResource<T>(string path) where T : Object
        {
            // 检查缓存
            if (_resourceCache.TryGetValue(path, out Object cached))
            {
                if (cached is T resource)
                {
                    Debug.Log($"✅ 从缓存加载：{path}");
                    return resource;
                }
            }
            
            // 从 Resources 加载
            T loadedResource = Resources.Load<T>(path);
            
            if (loadedResource != null)
            {
                // 添加到缓存
                _resourceCache[path] = loadedResource;
                Debug.Log($"📥 新加载：{path}");
            }
            else
            {
                Debug.LogError($"❌ 资源加载失败：{path}");
            }
            
            return loadedResource;
        }
        
        /// <summary>
        /// 加载资源并实例化
        /// </summary>
        /// <typeparam name="T">预制体类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="parent">父节点</param>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <returns>实例化的 GameObject</returns>
        public T LoadAndInstantiate<T>(string path, Transform parent = null, Vector3 position = default, Quaternion rotation = default) where T : Component
        {
            T prefab = LoadResource<T>(path);
            
            if (prefab != null)
            {
                T instance = Instantiate(prefab, position, rotation);
                
                if (parent != null)
                {
                    instance.transform.SetParent(parent);
                }
                
                Debug.Log($"🎮 实例化：{path}");
                return instance;
            }
            
            return null;
        }
        
        #endregion
        
        #region 异步加载
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="onComplete">加载完成回调</param>
        public void LoadResourceAsync<T>(string path, System.Action<T> onComplete) where T : Object
        {
            System.Action<Object> callback = (asset) =>
            {
                if (asset is T typedAsset)
                {
                    onComplete?.Invoke(typedAsset);
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            };

            // 检查缓存
            if (_resourceCache.TryGetValue(path, out Object cached))
            {
                if (cached is T resource)
                {
                    Debug.Log($"✅ 从缓存加载：{path}");
                    onComplete?.Invoke(resource);
                    return;
                }
            }
            
            // 检查是否已在加载
            if (_asyncLoadTasks.ContainsKey(path))
            {
                if (!_asyncCallbacks.ContainsKey(path))
                {
                    _asyncCallbacks[path] = new List<System.Action<Object>>();
                }

                _asyncCallbacks[path].Add(callback);
                Debug.Log($"⏳ 资源已在加载，回调已合并：{path}");
                return;
            }

            _asyncCallbacks[path] = new List<System.Action<Object>> { callback };
            
            // 开始异步加载
            StartCoroutine(LoadResourceAsyncCoroutine(path));
        }
        
        /// <summary>
        /// 异步加载协程
        /// </summary>
        private IEnumerator LoadResourceAsyncCoroutine(string path)
        {
            Debug.Log($"🔄 开始异步加载：{path}");
            
            ResourceRequest request = Resources.LoadAsync<Object>(path);
            _asyncLoadTasks[path] = request;
            
            // 等待加载完成
            yield return request;
            
            // 移除任务
            _asyncLoadTasks.Remove(path);
            
            if (request.asset != null)
            {
                // 添加到缓存
                _resourceCache[path] = request.asset;
                
                Debug.Log($"✅ 异步加载完成：{path}");
            }
            else
            {
                Debug.LogError($"❌ 异步加载失败：{path}");
            }

            if (_asyncCallbacks.TryGetValue(path, out List<System.Action<Object>> callbacks))
            {
                Object asset = request.asset;
                foreach (System.Action<Object> callback in callbacks)
                {
                    callback?.Invoke(asset);
                }

                _asyncCallbacks.Remove(path);
            }
        }
        
        /// <summary>
        /// 异步加载并实例化
        /// </summary>
        public void LoadAndInstantiateAsync<T>(string path, Transform parent = null, System.Action<T> onComplete = null) where T : Component
        {
            LoadResourceAsync<T>(path, (resource) =>
            {
                if (resource != null && resource is T prefab)
                {
                    T instance = Instantiate(prefab);
                    
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent);
                    }
                    
                    onComplete?.Invoke(instance);
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            });
        }
        
        #endregion
        
        #region 资源卸载
        
        /// <summary>
        /// 卸载单个资源
        /// </summary>
        /// <param name="path">资源路径</param>
        public void UnloadResource(string path)
        {
            if (_resourceCache.ContainsKey(path))
            {
                _resourceCache.Remove(path);
                Debug.Log($"🗑️ 已卸载资源：{path}");
            }
            
            // 卸载未使用的资源
            Resources.UnloadUnusedAssets();
        }
        
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            _resourceCache.Clear();
            _asyncLoadTasks.Clear();
            _asyncCallbacks.Clear();
            Resources.UnloadUnusedAssets();
            Debug.Log("🗑️ 已清空所有资源缓存");
        }
        
        /// <summary>
        /// 强制垃圾回收
        /// </summary>
        public void ForceGarbageCollection()
        {
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
            Debug.Log("♻️ 已执行垃圾回收");
        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 获取缓存资源数量
        /// </summary>
        public int GetCacheCount()
        {
            return _resourceCache.Count;
        }
        
        /// <summary>
        /// 检查资源是否在缓存中
        /// </summary>
        public bool IsResourceCached(string path)
        {
            return _resourceCache.ContainsKey(path);
        }
        
        /// <summary>
        /// 预加载多个资源
        /// </summary>
        public void PreloadResources(string[] paths)
        {
            Debug.Log($"📦 预加载 {paths.Length} 个资源...");
            
            foreach (string path in paths)
            {
                LoadResource<Object>(path);
            }
            
            Debug.Log("✅ 预加载完成！");
        }
        
        #endregion
        
        #region 调试功能
        
        /// <summary>
        /// 输出资源缓存状态
        /// </summary>
        [ContextMenu("输出缓存状态")]
        private void DebugCacheStatus()
        {
            Debug.Log("===== 资源缓存状态 =====");
            Debug.Log($"缓存数量：{_resourceCache.Count}");
            Debug.Log($"异步任务：{_asyncLoadTasks.Count}");
            Debug.Log($"回调队列：{_asyncCallbacks.Count}");
            
            foreach (var kvp in _resourceCache)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value.name}");
            }
            
            Debug.Log("======================");
        }
        
        #endregion
    }
}
