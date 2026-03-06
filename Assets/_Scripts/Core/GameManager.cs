using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarHaven.Core
{
    /// <summary>
    /// 游戏管理器 - 单例模式
    /// 负责游戏状态管理、场景切换、全局数据管理
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 单例模式
        
        private static GameManager _instance;
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        _instance = go.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region 游戏状态
        
        /// <summary>
        /// 游戏是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }
        
        /// <summary>
        /// 游戏是否暂停
        /// </summary>
        public bool IsPaused { get; private set; }
        
        /// <summary>
        /// 当前游戏场景名称
        /// </summary>
        public string CurrentScene { get; private set; }
        
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
            
            Initialize();
        }
        
        private void Start()
        {
            // 游戏启动后的初始化
            Debug.Log("🎮 星际港湾 - 游戏启动！");
        }
        
        private void Update()
        {
            // 每帧更新逻辑
            if (IsPaused) return;
            
            // 测试：按 P 键暂停/继续
            if (Input.GetKeyDown(KeyCode.P))
            {
                TogglePause();
            }
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化游戏管理器
        /// </summary>
        private void Initialize()
        {
            if (IsInitialized) return;
            
            Debug.Log("🔧 GameManager 初始化中...");
            
            IsInitialized = true;
            IsPaused = false;
            CurrentScene = "Bootstrap";
            
            Debug.Log("✅ GameManager 初始化完成！");
        }
        
        #endregion
        
        #region 游戏控制
        
        /// <summary>
        /// 暂停/继续游戏
        /// </summary>
        public void TogglePause()
        {
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0 : 1;
            
            Debug.Log($"游戏{(IsPaused ? "已暂停" : "已继续")}");
        }
        
        /// <summary>
        /// 设置游戏暂停状态
        /// </summary>
        /// <param name="paused">是否暂停</param>
        public void SetPause(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0 : 1;
        }
        
        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("退出游戏...");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
        #endregion
        
        #region 场景管理
        
        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        public void LoadScene(string sceneName)
        {
            TryLoadScene(sceneName);
        }

        /// <summary>
        /// 安全加载场景，失败时返回 false
        /// </summary>
        public bool TryLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("场景名称为空，无法加载。");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"场景不可加载（未加入 Build Settings 或不存在）：{sceneName}");
                return false;
            }

            Debug.Log($"加载场景：{sceneName}");
            CurrentScene = sceneName;

            // TODO: 使用 SceneManager.LoadSceneAsync 异步加载
            SceneManager.LoadScene(sceneName);
            return true;
        }
        
        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public void ReloadScene()
        {
            if (string.IsNullOrWhiteSpace(CurrentScene))
            {
                Debug.LogError("当前场景为空，无法重载。");
                return;
            }

            LoadScene(CurrentScene);
        }
        
        #endregion
        
        #region 调试功能
        
        /// <summary>
        /// 输出游戏状态到控制台
        /// </summary>
        [ContextMenu("输出游戏状态")]
        private void DebugGameStatus()
        {
            Debug.Log("===== 游戏状态 =====");
            Debug.Log($"已初始化：{IsInitialized}");
            Debug.Log($"已暂停：{IsPaused}");
            Debug.Log($"当前场景：{CurrentScene}");
            Debug.Log("==================");
        }
        
        #endregion
    }
}
