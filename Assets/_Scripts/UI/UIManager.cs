using System;
using System.Collections;
using System.Collections.Generic;
using StellarHaven.Core;
using UnityEngine;
using UnityEngine.UI;

namespace StellarHaven.UI
{
    /// <summary>
    /// UI 管理器（单例）。
    /// 负责 UI 打开/关闭、分层管理、异步加载、缓存与通用 UI 功能。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        #region 单例

        private static UIManager _instance;

        /// <summary>
        /// 单例实例。
        /// </summary>
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("UIManager");
                        _instance = go.AddComponent<UIManager>();
                    }
                }

                return _instance;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// UI 打开事件。
        /// </summary>
        public event Action<UIType, UIBase> OnUIOpened;

        /// <summary>
        /// UI 关闭事件。
        /// </summary>
        public event Action<UIType> OnUIClosed;

        #endregion

        #region 配置

        [Header("资源加载")]
        [SerializeField, Tooltip("UI 资源路径前缀（Resources 内相对路径）")]
        private string uiResourceRoot = "UI";

        [SerializeField, Tooltip("是否在异步加载时显示 Loading")]
        private bool showLoadingWhenAsyncLoad = true;

        [Header("动画")]
        [SerializeField, Tooltip("打开动画时长")]
        private float openAnimationDuration = 0.2f;

        [SerializeField, Tooltip("关闭动画时长")]
        private float closeAnimationDuration = 0.15f;

        [SerializeField, Tooltip("是否启用滑入滑出动画")]
        private bool enableSlideAnimation = true;

        [SerializeField, Tooltip("滑动偏移（从该偏移滑入）")]
        private Vector2 slideOffset = new Vector2(0f, -80f);

        #endregion

        #region 运行时数据

        private readonly Dictionary<UIType, UIBase> _openedUIs = new Dictionary<UIType, UIBase>();
        private readonly Dictionary<UIType, UIBase> _instanceCache = new Dictionary<UIType, UIBase>();
        private readonly Dictionary<UIType, GameObject> _prefabCache = new Dictionary<UIType, GameObject>();
        private readonly Dictionary<UILayer, RectTransform> _layerRoots = new Dictionary<UILayer, RectTransform>();
        private readonly HashSet<UIType> _loadingUIs = new HashSet<UIType>();

        private Canvas _rootCanvas;
        private RectTransform _rootRect;

        private GameObject _loadingOverlay;
        private Text _loadingText;

        private GameObject _toastGO;
        private Text _toastText;
        private Coroutine _toastCoroutine;

        private GameObject _confirmGO;
        private Text _confirmTitle;
        private Text _confirmContent;
        private Action _confirmAction;
        private Action _cancelAction;

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
            InitializeCanvasAndLayers();
        }

        #endregion

        #region UI 层级管理

        /// <summary>
        /// 打开指定 UI。
        /// </summary>
        /// <param name="type">UI 类型。</param>
        /// <param name="parameters">打开参数。</param>
        public void OpenUI(UIType type, params object[] parameters)
        {
            if (_openedUIs.TryGetValue(type, out UIBase opened))
            {
                opened.Show();
                opened.OnOpen(parameters);
                opened.OnRefresh();
                PlayOpenAnimation(opened);
                OnUIOpened?.Invoke(type, opened);
                return;
            }

            if (_loadingUIs.Contains(type))
            {
                return;
            }

            StartCoroutine(OpenUICoroutine(type, parameters));
        }

        /// <summary>
        /// 关闭指定 UI。
        /// </summary>
        /// <param name="type">UI 类型。</param>
        public void CloseUI(UIType type)
        {
            if (!_openedUIs.TryGetValue(type, out UIBase ui))
            {
                return;
            }

            StartCoroutine(CloseUICoroutine(type, ui));
        }

        /// <summary>
        /// 关闭所有已打开 UI。
        /// </summary>
        public void CloseAllUI()
        {
            List<UIType> keys = new List<UIType>(_openedUIs.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                CloseUI(keys[i]);
            }
        }

        #endregion

        #region UI 缓存

        /// <summary>
        /// 获取已打开的 UI。
        /// </summary>
        /// <typeparam name="T">UI 类型。</typeparam>
        /// <param name="type">UI 标识。</param>
        public T GetUI<T>(UIType type) where T : UIBase
        {
            if (_openedUIs.TryGetValue(type, out UIBase ui))
            {
                return ui as T;
            }

            return null;
        }

        #endregion

        #region 资源加载

        /// <summary>
        /// 异步加载 UI 预制体到缓存。
        /// </summary>
        /// <param name="type">UI 类型。</param>
        public void LoadUIPrefab(UIType type)
        {
            if (_prefabCache.ContainsKey(type))
            {
                return;
            }

            StartCoroutine(LoadUIPrefabCoroutine(type));
        }

        private IEnumerator OpenUICoroutine(UIType type, object[] parameters)
        {
            _loadingUIs.Add(type);

            bool showLoading = showLoadingWhenAsyncLoad && type != UIType.Loading;
            if (showLoading)
            {
                ShowLoading("加载中...");
            }

            yield return LoadUIPrefabCoroutine(type);

            if (!_prefabCache.TryGetValue(type, out GameObject prefab) || prefab == null)
            {
                Debug.LogError($"OpenUI 失败：未找到 UI 预制体，Type={type}");
                if (showLoading)
                {
                    HideLoading();
                }
                _loadingUIs.Remove(type);
                yield break;
            }

            UIBase ui = GetOrCreateUIInstance(type, prefab);
            if (ui == null)
            {
                if (showLoading)
                {
                    HideLoading();
                }
                _loadingUIs.Remove(type);
                yield break;
            }

            _openedUIs[type] = ui;
            ui.Show();
            ui.OnOpen(parameters);
            PlayOpenAnimation(ui);
            OnUIOpened?.Invoke(type, ui);

            if (showLoading)
            {
                HideLoading();
            }

            _loadingUIs.Remove(type);
        }

        private IEnumerator LoadUIPrefabCoroutine(UIType type)
        {
            if (_prefabCache.ContainsKey(type))
            {
                yield break;
            }

            string path = GetUIPrefabPath(type);
            bool done = false;
            GameObject loadedPrefab = null;

            ResourceManager.Instance.LoadResourceAsync<GameObject>(path, prefab =>
            {
                loadedPrefab = prefab;
                done = true;
            });

            while (!done)
            {
                yield return null;
            }

            if (loadedPrefab == null)
            {
                Debug.LogWarning($"UI 预制体加载失败：{path}");
                yield break;
            }

            _prefabCache[type] = loadedPrefab;
        }

        #endregion

        #region 动画系统

        /// <summary>
        /// 播放打开动画。
        /// </summary>
        /// <param name="ui">目标 UI。</param>
        public void PlayOpenAnimation(UIBase ui)
        {
            if (ui == null)
            {
                return;
            }

            StartCoroutine(PlayOpenAnimationCoroutine(ui));
        }

        /// <summary>
        /// 播放关闭动画。
        /// </summary>
        /// <param name="ui">目标 UI。</param>
        public void PlayCloseAnimation(UIBase ui)
        {
            if (ui == null)
            {
                return;
            }

            StartCoroutine(PlayCloseAnimationCoroutine(ui));
        }

        private IEnumerator PlayOpenAnimationCoroutine(UIBase ui)
        {
            CanvasGroup cg = GetOrAddCanvasGroup(ui.gameObject);
            RectTransform rt = ui.transform as RectTransform;
            Vector2 targetPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 startPos = targetPos + (enableSlideAnimation ? slideOffset : Vector2.zero);

            float duration = Mathf.Max(0.01f, openAnimationDuration);
            float timer = 0f;

            cg.alpha = 0f;
            if (rt != null && enableSlideAnimation)
            {
                rt.anchoredPosition = startPos;
            }

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                cg.alpha = t;

                if (rt != null && enableSlideAnimation)
                {
                    rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                }

                yield return null;
            }

            cg.alpha = 1f;
            if (rt != null && enableSlideAnimation)
            {
                rt.anchoredPosition = targetPos;
            }
        }

        private IEnumerator PlayCloseAnimationCoroutine(UIBase ui)
        {
            CanvasGroup cg = GetOrAddCanvasGroup(ui.gameObject);
            RectTransform rt = ui.transform as RectTransform;
            Vector2 originPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 endPos = originPos + (enableSlideAnimation ? slideOffset : Vector2.zero);

            float duration = Mathf.Max(0.01f, closeAnimationDuration);
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                cg.alpha = 1f - t;

                if (rt != null && enableSlideAnimation)
                {
                    rt.anchoredPosition = Vector2.Lerp(originPos, endPos, t);
                }

                yield return null;
            }

            cg.alpha = 0f;
            if (rt != null && enableSlideAnimation)
            {
                rt.anchoredPosition = originPos;
            }
        }

        #endregion

        #region 通用方法

        /// <summary>
        /// 显示 Toast 提示。
        /// </summary>
        /// <param name="message">提示文本。</param>
        /// <param name="duration">显示时长。</param>
        public void ShowToast(string message, float duration)
        {
            EnsureToastUI();

            _toastText.text = string.IsNullOrWhiteSpace(message) ? "..." : message;
            _toastGO.SetActive(true);

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }

            _toastCoroutine = StartCoroutine(HideToastAfter(duration));
        }

        /// <summary>
        /// 显示确认弹窗。
        /// </summary>
        /// <param name="title">标题。</param>
        /// <param name="content">内容。</param>
        /// <param name="onConfirm">确认回调。</param>
        /// <param name="onCancel">取消回调。</param>
        public void ShowConfirmDialog(string title, string content, Action onConfirm, Action onCancel)
        {
            EnsureConfirmDialogUI();
            _confirmAction = onConfirm;
            _cancelAction = onCancel;
            _confirmTitle.text = string.IsNullOrWhiteSpace(title) ? "提示" : title;
            _confirmContent.text = string.IsNullOrWhiteSpace(content) ? string.Empty : content;
            _confirmGO.SetActive(true);
        }

        /// <summary>
        /// 显示 Loading。
        /// </summary>
        /// <param name="message">提示文本。</param>
        public void ShowLoading(string message)
        {
            EnsureLoadingOverlay();
            _loadingText.text = string.IsNullOrWhiteSpace(message) ? "加载中..." : message;
            _loadingOverlay.SetActive(true);
        }

        /// <summary>
        /// 隐藏 Loading。
        /// </summary>
        public void HideLoading()
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.SetActive(false);
            }
        }

        #endregion

        #region 调试

        /// <summary>
        /// 输出当前 UI 状态。
        /// </summary>
        [ContextMenu("输出 UI 状态")]
        private void DebugUIStatus()
        {
            Debug.Log("===== UI 状态 =====");
            Debug.Log($"打开数量：{_openedUIs.Count}");
            foreach (KeyValuePair<UIType, UIBase> kv in _openedUIs)
            {
                Debug.Log($"- {kv.Key}: {(kv.Value != null ? kv.Value.name : "null")}");
            }
            Debug.Log("===================");
        }

        #endregion

        #region 内部实现

        private IEnumerator CloseUICoroutine(UIType type, UIBase ui)
        {
            yield return PlayCloseAnimationCoroutine(ui);
            ui.OnClose();
            ui.Hide();
            _openedUIs.Remove(type);
            OnUIClosed?.Invoke(type);
        }

        private UIBase GetOrCreateUIInstance(UIType type, GameObject prefab)
        {
            if (_instanceCache.TryGetValue(type, out UIBase cached) && cached != null)
            {
                return cached;
            }

            GameObject instance = Instantiate(prefab);
            UIBase ui = instance.GetComponent<UIBase>();
            if (ui == null)
            {
                Debug.LogError($"UI 预制体缺少 UIBase：{prefab.name}");
                Destroy(instance);
                return null;
            }

            RectTransform layerRoot = GetLayerRoot(ui.Layer);
            instance.transform.SetParent(layerRoot, false);
            _instanceCache[type] = ui;
            return ui;
        }

        private string GetUIPrefabPath(UIType type)
        {
            return $"{uiResourceRoot}/{type}UI";
        }

        private void InitializeCanvasAndLayers()
        {
            GameObject root = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);

            _rootRect = root.GetComponent<RectTransform>();
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;

            _rootCanvas = root.GetComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CreateLayerRoot(UILayer.Background);
            CreateLayerRoot(UILayer.Normal);
            CreateLayerRoot(UILayer.Popup);
            CreateLayerRoot(UILayer.Top);
        }

        private void CreateLayerRoot(UILayer layer)
        {
            GameObject layerGO = new GameObject(layer.ToString(), typeof(RectTransform), typeof(Canvas));
            layerGO.transform.SetParent(_rootRect, false);

            RectTransform rt = layerGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Canvas canvas = layerGO.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = GetLayerOrder(layer);

            _layerRoots[layer] = rt;
        }

        private RectTransform GetLayerRoot(UILayer layer)
        {
            if (_layerRoots.TryGetValue(layer, out RectTransform rt))
            {
                return rt;
            }

            return _rootRect;
        }

        private int GetLayerOrder(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Background: return 0;
                case UILayer.Normal: return 100;
                case UILayer.Popup: return 200;
                case UILayer.Top: return 300;
                default: return 100;
            }
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = go.AddComponent<CanvasGroup>();
            }
            return cg;
        }

        private void EnsureLoadingOverlay()
        {
            if (_loadingOverlay != null)
            {
                return;
            }

            _loadingOverlay = CreateSimplePanel("LoadingOverlay", UILayer.Top, new Color(0f, 0f, 0f, 0.6f));
            _loadingText = CreateCenteredText("LoadingText", _loadingOverlay.transform as RectTransform, "加载中...");
            _loadingOverlay.SetActive(false);
        }

        private void EnsureToastUI()
        {
            if (_toastGO != null)
            {
                return;
            }

            _toastGO = CreateSimplePanel("Toast", UILayer.Top, new Color(0f, 0f, 0f, 0.75f));
            RectTransform rt = _toastGO.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.15f);
            rt.anchorMax = new Vector2(0.5f, 0.15f);
            rt.sizeDelta = new Vector2(600f, 90f);
            rt.anchoredPosition = Vector2.zero;

            _toastText = CreateCenteredText("ToastText", rt, string.Empty);
            _toastGO.SetActive(false);
        }

        private void EnsureConfirmDialogUI()
        {
            if (_confirmGO != null)
            {
                return;
            }

            _confirmGO = CreateSimplePanel("ConfirmDialog", UILayer.Popup, new Color(0f, 0f, 0f, 0.7f));
            RectTransform rootRt = _confirmGO.transform as RectTransform;

            GameObject body = new GameObject("Body", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(rootRt, false);
            RectTransform bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRt.sizeDelta = new Vector2(760f, 420f);
            bodyRt.anchoredPosition = Vector2.zero;
            body.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            _confirmTitle = CreateText("Title", bodyRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(680f, 56f), 34);
            _confirmContent = CreateText("Content", bodyRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(680f, 180f), 26);

            Button confirmBtn = CreateButton("Confirm", bodyRt, "确认", new Vector2(-140f, -150f));
            Button cancelBtn = CreateButton("Cancel", bodyRt, "取消", new Vector2(140f, -150f));

            confirmBtn.onClick.AddListener(() =>
            {
                _confirmGO.SetActive(false);
                _confirmAction?.Invoke();
            });
            cancelBtn.onClick.AddListener(() =>
            {
                _confirmGO.SetActive(false);
                _cancelAction?.Invoke();
            });

            _confirmGO.SetActive(false);
        }

        private IEnumerator HideToastAfter(float duration)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, duration));
            if (_toastGO != null)
            {
                _toastGO.SetActive(false);
            }
        }

        private GameObject CreateSimplePanel(string name, UILayer layer, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(GetLayerRoot(layer), false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = color;

            return go;
        }

        private Text CreateCenteredText(string name, RectTransform parent, string value)
        {
            Text text = CreateText(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f), 32);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value;
            return text;
        }

        private Text CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPosition)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(220f, 72f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.95f, 1f);

            Button btn = go.GetComponent<Button>();
            Text txt = CreateCenteredText("Text", rt, label);
            txt.fontSize = 28;

            return btn;
        }

        #endregion
    }
}
