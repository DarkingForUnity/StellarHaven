using UnityEngine;

namespace StellarHaven.UI
{
    /// <summary>
    /// UI 类型定义。
    /// </summary>
    public enum UIType
    {
        Main = 0,
        Visitor = 1,
        Build = 2,
        Tech = 3,
        Settings = 4,
        Dialog = 5,
        Loading = 6
    }

    /// <summary>
    /// UI 层级定义。
    /// </summary>
    public enum UILayer
    {
        Background = 0,
        Normal = 1,
        Popup = 2,
        Top = 3
    }

    /// <summary>
    /// UI 抽象基类。
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        [Header("UI 配置")]
        [SerializeField, Tooltip("UI 类型")]
        private UIType type = UIType.Main;

        [SerializeField, Tooltip("UI 层级")]
        private UILayer layer = UILayer.Normal;

        [SerializeField, Tooltip("可选 CanvasGroup（用于显示隐藏与动画）")]
        protected CanvasGroup canvasGroup;

        /// <summary>
        /// UI 类型。
        /// </summary>
        public UIType Type => type;

        /// <summary>
        /// UI 层级。
        /// </summary>
        public UILayer Layer => layer;

        /// <summary>
        /// 打开 UI 时调用。
        /// </summary>
        /// <param name="parameters">打开参数。</param>
        public virtual void OnOpen(params object[] parameters)
        {
        }

        /// <summary>
        /// 关闭 UI 时调用。
        /// </summary>
        public virtual void OnClose()
        {
        }

        /// <summary>
        /// 刷新 UI 时调用。
        /// </summary>
        public virtual void OnRefresh()
        {
        }

        /// <summary>
        /// 显示 UI。
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// 隐藏 UI。
        /// </summary>
        public virtual void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        protected virtual void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
