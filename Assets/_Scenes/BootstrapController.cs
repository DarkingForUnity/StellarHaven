using UnityEngine;
using StellarHaven.Core;

namespace StellarHaven.Scenes
{
    /// <summary>
    /// 引导场景控制器
    /// 负责游戏启动时的初始化和场景过渡
    /// </summary>
    public class BootstrapController : MonoBehaviour
    {
        [Header("场景设置")]
        [SerializeField] private string mainSceneName = "Main";
        
        [Header("加载设置")]
        [SerializeField] private float minLoadTime = 2f;
        [SerializeField] private float fadeDuration = 1f;
        
        private bool _isInitialized = false;
        
        private void Start()
        {
            Debug.Log("🚀 引导场景启动...");
            InitializeGame();
        }
        
        /// <summary>
        /// 初始化游戏
        /// </summary>
        private async void InitializeGame()
        {
            if (_isInitialized) return;
            
            // 等待 GameManager 初始化
            await System.Threading.Tasks.Task.Delay(100);
            
            // 预加载必要资源
            PreloadEssentialResources();
            
            // 模拟加载时间
            await System.Threading.Tasks.Task.Delay((int)(minLoadTime * 1000));
            
            _isInitialized = true;
            
            Debug.Log("✅ 初始化完成，加载主场景...");
            
            // 加载主场景
            LoadMainScene();
        }
        
        /// <summary>
        /// 预加载必要资源
        /// </summary>
        private void PreloadEssentialResources()
        {
            Debug.Log("📦 预加载必要资源...");
            
            // 预加载 UI 资源
            ResourceManager.Instance.LoadResource<GameObject>("UI/MainCanvas");
            
            // 预加载音频资源
            ResourceManager.Instance.LoadResource<AudioClip>("Audio/BGM/MainTheme");
            
            Debug.Log("✅ 预加载完成");
        }
        
        /// <summary>
        /// 加载主场景
        /// </summary>
        private void LoadMainScene()
        {
            if (!GameManager.Instance.TryLoadScene(mainSceneName))
            {
                Debug.LogError($"主场景加载失败：{mainSceneName}。请先创建场景并加入 Build Settings。");
            }
        }
        
        #region 调试功能
        
        /// <summary>
        /// 跳过加载直接进入主场景（仅调试）
        /// </summary>
        [ContextMenu("跳过加载")]
        private void SkipLoading()
        {
            Debug.LogWarning("⚠️ 跳过加载（调试模式）");
            LoadMainScene();
        }
        
        #endregion
    }
}
