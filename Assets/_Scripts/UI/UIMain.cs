using System;
using System.Collections;
using System.Collections.Generic;
using StellarHaven.Core;
using StellarHaven.Visitor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StellarHaven.UI
{
    /// <summary>
    /// 主界面 UI。
    /// 负责顶部状态栏、空间站视图、底部导航与悬浮提示。
    /// </summary>
    public class UIMain : UIBase, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        #region TopStatusBar

        [Header("TopStatusBar")]
        [SerializeField, Tooltip("金币文本")]
        private Text goldText;

        [SerializeField, Tooltip("能量文本")]
        private Text energyText;

        [SerializeField, Tooltip("旅客数量文本")]
        private Text visitorCountText;

        [SerializeField, Tooltip("等级文本")]
        private Text levelText;

        [SerializeField, Tooltip("经验进度条")]
        private Image levelProgressImage;

        #endregion

        #region BottomNavBar

        [Header("BottomNavBar")]
        [SerializeField, Tooltip("导航按钮（顺序：空间站、建造、员工、科技、探索、仓库）")]
        private Button[] navButtons;

        [SerializeField, Tooltip("导航按钮高亮图标（可选，数量需与 navButtons 一致）")]
        private Image[] navHighlights;

        [SerializeField, Tooltip("导航选中颜色")]
        private Color navSelectedColor = new Color(0.32f, 0.69f, 1f, 1f);

        [SerializeField, Tooltip("导航未选中颜色")]
        private Color navUnselectedColor = Color.white;

        #endregion

        #region StationView

        [Header("StationView")]
        [SerializeField, Tooltip("空间站视图容器")]
        private RectTransform stationViewRect;

        [SerializeField, Tooltip("设施节点容器")]
        private RectTransform facilityNodeRoot;

        [SerializeField, Tooltip("设施节点预制体（可选）")]
        private GameObject facilityNodePrefab;

        [SerializeField, Tooltip("缩放最小值")]
        private float minZoom = 0.7f;

        [SerializeField, Tooltip("缩放最大值")]
        private float maxZoom = 2.0f;

        [SerializeField, Tooltip("双指缩放灵敏度")]
        private float pinchZoomSpeed = 0.005f;

        #endregion

        #region FloatingTips

        [Header("FloatingTips")]
        [SerializeField, Tooltip("悬浮提示根节点")]
        private RectTransform floatingTipsRoot;

        [SerializeField, Tooltip("提示文本预制体（可选，建议挂 CanvasGroup）")]
        private Text floatingTipTextPrefab;

        [SerializeField, Tooltip("提示持续时间（秒）")]
        private float tipDuration = 3f;

        [SerializeField, Tooltip("提示上升距离")]
        private float tipMoveY = 80f;

        #endregion

        #region Runtime

        private readonly Dictionary<ResourceType, int> _resourceValues = new Dictionary<ResourceType, int>();
        private readonly Dictionary<string, FacilityNodeView> _facilityNodes = new Dictionary<string, FacilityNodeView>();
        private readonly Dictionary<Text, Coroutine> _numberTweenCoroutines = new Dictionary<Text, Coroutine>();

        private Coroutine _pinchCoroutine;
        private int _selectedTab = -1;
        private Vector2 _lastDragPosition;
        private bool _isBoundEvents;
        private VisitorManager _visitorManager;

        #endregion

        #region UIBase

        /// <summary>
        /// 打开时刷新基础状态并绑定事件。
        /// </summary>
        public override void OnOpen(params object[] parameters)
        {
            base.OnOpen(parameters);
            BindEvents();
            SelectTab(0);
            RefreshAll();
        }

        /// <summary>
        /// 关闭时解绑事件。
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            UnbindEvents();
        }

        /// <summary>
        /// 刷新主界面。
        /// </summary>
        public override void OnRefresh()
        {
            base.OnRefresh();
            RefreshAll();
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultReferences();
            BindNavButtonEvents();
        }

        private void Update()
        {
            HandlePinchZoom();
        }

        #endregion

        #region 顶部状态栏

        /// <summary>
        /// 更新资源显示。
        /// </summary>
        /// <param name="type">资源类型。</param>
        /// <param name="amount">最新数值。</param>
        public void UpdateResource(ResourceType type, int amount)
        {
            int oldValue = 0;
            _resourceValues.TryGetValue(type, out oldValue);
            _resourceValues[type] = amount;

            switch (type)
            {
                case ResourceType.Coin:
                    TweenNumber(goldText, oldValue, amount);
                    break;
                case ResourceType.Energy:
                    TweenNumber(energyText, oldValue, amount);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 更新等级显示。
        /// </summary>
        /// <param name="level">等级。</param>
        /// <param name="exp">当前经验。</param>
        /// <param name="maxExp">经验上限。</param>
        public void UpdateLevel(int level, int exp, int maxExp)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv.{Mathf.Max(1, level)}";
            }

            if (levelProgressImage != null)
            {
                float value = maxExp > 0 ? Mathf.Clamp01((float)exp / maxExp) : 0f;
                levelProgressImage.fillAmount = value;
            }
        }

        /// <summary>
        /// 更新旅客数量显示。
        /// </summary>
        /// <param name="current">当前旅客数量。</param>
        /// <param name="max">最大旅客数量。</param>
        public void UpdateVisitorCount(int current, int max)
        {
            if (visitorCountText != null)
            {
                visitorCountText.text = $"{Mathf.Max(0, current)}/{Mathf.Max(0, max)}";
            }
        }

        #endregion

        #region 底部导航

        /// <summary>
        /// 选择导航标签。
        /// </summary>
        /// <param name="index">标签索引。</param>
        public void SelectTab(int index)
        {
            if (navButtons == null || navButtons.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, navButtons.Length - 1);
            _selectedTab = index;

            for (int i = 0; i < navButtons.Length; i++)
            {
                bool selected = i == index;
                Image targetImage = navButtons[i] != null ? navButtons[i].GetComponent<Image>() : null;
                if (targetImage != null)
                {
                    targetImage.color = selected ? navSelectedColor : navUnselectedColor;
                }

                if (navHighlights != null && i < navHighlights.Length && navHighlights[i] != null)
                {
                    navHighlights[i].enabled = selected;
                }
            }
        }

        private void BindNavButtonEvents()
        {
            if (navButtons == null)
            {
                return;
            }

            for (int i = 0; i < navButtons.Length; i++)
            {
                if (navButtons[i] == null)
                {
                    continue;
                }

                int index = i;
                navButtons[i].onClick.RemoveAllListeners();
                navButtons[i].onClick.AddListener(() => OnClickNav(index));
            }
        }

        private void OnClickNav(int index)
        {
            SelectTab(index);

            switch (index)
            {
                case 0:
                    break;
                case 1:
                    UIManager.Instance.OpenUI(UIType.Build);
                    break;
                case 3:
                    UIManager.Instance.OpenUI(UIType.Tech);
                    break;
                default:
                    UIManager.Instance.ShowToast("功能开发中", 1.5f);
                    break;
            }
        }

        #endregion

        #region 设施节点

        /// <summary>
        /// 创建设施节点。
        /// </summary>
        /// <param name="data">设施数据。</param>
        public void CreateFacilityNode(FacilityData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.id))
            {
                return;
            }

            if (_facilityNodes.ContainsKey(data.id))
            {
                UpdateFacilityNode(data.id, data);
                return;
            }

            RectTransform root = facilityNodeRoot != null ? facilityNodeRoot : stationViewRect;
            if (root == null)
            {
                return;
            }

            GameObject nodeGo = null;
            if (facilityNodePrefab != null)
            {
                nodeGo = Instantiate(facilityNodePrefab, root);
            }
            else
            {
                nodeGo = CreateDefaultFacilityNode(root);
            }

            FacilityNodeView nodeView = nodeGo.GetComponent<FacilityNodeView>();
            if (nodeView == null)
            {
                nodeView = nodeGo.AddComponent<FacilityNodeView>();
            }

            nodeView.Bind(data, OnClickFacilityNode);
            _facilityNodes[data.id] = nodeView;
        }

        /// <summary>
        /// 更新设施节点。
        /// </summary>
        /// <param name="id">设施 ID。</param>
        /// <param name="data">设施数据。</param>
        public void UpdateFacilityNode(string id, FacilityData data)
        {
            if (string.IsNullOrWhiteSpace(id) || data == null)
            {
                return;
            }

            if (!_facilityNodes.TryGetValue(id, out FacilityNodeView node))
            {
                CreateFacilityNode(data);
                return;
            }

            node.Bind(data, OnClickFacilityNode);
        }

        /// <summary>
        /// 移除设施节点。
        /// </summary>
        /// <param name="id">设施 ID。</param>
        public void RemoveFacilityNode(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (_facilityNodes.TryGetValue(id, out FacilityNodeView node))
            {
                _facilityNodes.Remove(id);
                if (node != null)
                {
                    Destroy(node.gameObject);
                }
            }
        }

        private void OnClickFacilityNode(FacilityData data)
        {
            if (data == null)
            {
                return;
            }

            UIManager.Instance.ShowConfirmDialog(
                $"设施：{data.name}",
                $"等级：{data.level}\n状态：{(data.unlocked ? "已解锁" : "未解锁")}",
                null,
                null
            );
        }

        #endregion

        #region 悬浮提示

        /// <summary>
        /// 显示新旅客到达提示。
        /// </summary>
        /// <param name="visitor">旅客数据。</param>
        public void ShowArrivalTip(VisitorData visitor)
        {
            string visitorName = visitor != null && !string.IsNullOrWhiteSpace(visitor.name) ? visitor.name : "新旅客";
            ShowFloatingTip($"🛸 {visitorName} 抵达空间站");
        }

        /// <summary>
        /// 显示收益提示。
        /// </summary>
        /// <param name="amount">收益数量。</param>
        public void ShowIncomeTip(int amount)
        {
            ShowFloatingTip($"+{amount} 金币");
        }

        private void ShowFloatingTip(string message)
        {
            RectTransform root = floatingTipsRoot != null ? floatingTipsRoot : transform as RectTransform;
            if (root == null)
            {
                UIManager.Instance.ShowToast(message, 1.2f);
                return;
            }

            Text tipText;
            if (floatingTipTextPrefab != null)
            {
                tipText = Instantiate(floatingTipTextPrefab, root);
            }
            else
            {
                tipText = CreateDefaultFloatingTip(root);
            }

            tipText.text = message;
            StartCoroutine(PlayFloatingTip(tipText));
        }

        private IEnumerator PlayFloatingTip(Text tipText)
        {
            if (tipText == null)
            {
                yield break;
            }

            RectTransform rt = tipText.rectTransform;
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, tipMoveY);

            CanvasGroup cg = tipText.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = tipText.gameObject.AddComponent<CanvasGroup>();
            }

            float timer = 0f;
            float duration = Mathf.Max(0.3f, tipDuration);
            cg.alpha = 1f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                cg.alpha = 1f - t;
                yield return null;
            }

            Destroy(tipText.gameObject);
        }

        #endregion

        #region 交互功能（拖拽/缩放）

        /// <summary>
        /// 开始拖拽。
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastDragPosition = eventData.position;
        }

        /// <summary>
        /// 拖拽空间站视图。
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (stationViewRect == null)
            {
                return;
            }

            Vector2 delta = eventData.position - _lastDragPosition;
            _lastDragPosition = eventData.position;
            stationViewRect.anchoredPosition += delta;
        }

        /// <summary>
        /// 鼠标滚轮缩放（编辑器/PC）。
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (stationViewRect == null)
            {
                return;
            }

            float currentScale = stationViewRect.localScale.x;
            float nextScale = Mathf.Clamp(currentScale + eventData.scrollDelta.y * 0.05f, minZoom, maxZoom);
            stationViewRect.localScale = Vector3.one * nextScale;
        }

        private void HandlePinchZoom()
        {
            if (stationViewRect == null)
            {
                return;
            }

            if (Input.touchCount != 2)
            {
                return;
            }

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 prev0 = t0.position - t0.deltaPosition;
            Vector2 prev1 = t1.position - t1.deltaPosition;
            float prevDistance = Vector2.Distance(prev0, prev1);
            float currDistance = Vector2.Distance(t0.position, t1.position);
            float delta = currDistance - prevDistance;

            float currentScale = stationViewRect.localScale.x;
            float targetScale = Mathf.Clamp(currentScale + delta * pinchZoomSpeed, minZoom, maxZoom);
            stationViewRect.localScale = Vector3.one * targetScale;
        }

        #endregion

        #region 数据绑定

        private void BindEvents()
        {
            if (_isBoundEvents)
            {
                return;
            }

            _visitorManager = FindObjectOfType<VisitorManager>();
            if (_visitorManager != null)
            {
                _visitorManager.OnVisitorArrived += HandleVisitorArrived;
                _visitorManager.OnVisitorDeparted += HandleVisitorDeparted;
                _visitorManager.OnVisitorServed += HandleVisitorServed;
            }

            // 当前项目 ResourceManager 未提供资源变更事件，这里保留绑定扩展点。
            // 后续若增加 OnResourceChanged 事件，可在此处订阅并调用 UpdateResource。

            _isBoundEvents = true;
        }

        private void UnbindEvents()
        {
            if (!_isBoundEvents)
            {
                return;
            }

            if (_visitorManager != null)
            {
                _visitorManager.OnVisitorArrived -= HandleVisitorArrived;
                _visitorManager.OnVisitorDeparted -= HandleVisitorDeparted;
                _visitorManager.OnVisitorServed -= HandleVisitorServed;
            }

            _isBoundEvents = false;
        }

        private void HandleVisitorArrived(StellarHaven.Visitor.Visitor visitor)
        {
            int current = _visitorManager != null ? _visitorManager.GetActiveVisitorCount() : 0;
            int max = 0;
            if (_visitorManager != null)
            {
                max = 20;
            }
            UpdateVisitorCount(current, max);

            VisitorData data = new VisitorData
            {
                id = visitor != null ? visitor.Id : 0,
                name = visitor != null && visitor.Config != null ? visitor.Config.visitorName : "新旅客",
                type = visitor != null && visitor.Config != null ? visitor.Config.type : VisitorType.Merchant
            };
            ShowArrivalTip(data);
        }

        private void HandleVisitorDeparted(StellarHaven.Visitor.Visitor visitor)
        {
            int current = _visitorManager != null ? _visitorManager.GetActiveVisitorCount() : 0;
            UpdateVisitorCount(current, 20);
        }

        private void HandleVisitorServed(StellarHaven.Visitor.Visitor visitor, float satisfaction)
        {
            int reward = Mathf.RoundToInt(Mathf.Max(0f, satisfaction) * 2f);
            ShowIncomeTip(reward);
        }

        private void RefreshAll()
        {
            if (!_resourceValues.ContainsKey(ResourceType.Coin))
            {
                _resourceValues[ResourceType.Coin] = 0;
            }

            if (!_resourceValues.ContainsKey(ResourceType.Energy))
            {
                _resourceValues[ResourceType.Energy] = 0;
            }

            UpdateResource(ResourceType.Coin, _resourceValues[ResourceType.Coin]);
            UpdateResource(ResourceType.Energy, _resourceValues[ResourceType.Energy]);
            UpdateLevel(1, 0, 100);

            int currentVisitor = _visitorManager != null ? _visitorManager.GetActiveVisitorCount() : 0;
            UpdateVisitorCount(currentVisitor, 20);
        }

        #endregion

        #region Helpers

        private void TweenNumber(Text target, int from, int to)
        {
            if (target == null)
            {
                return;
            }

            if (_numberTweenCoroutines.TryGetValue(target, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            Coroutine routine = StartCoroutine(TweenNumberCoroutine(target, from, to, 0.25f));
            _numberTweenCoroutines[target] = routine;
        }

        private IEnumerator TweenNumberCoroutine(Text target, int from, int to, float duration)
        {
            float timer = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                target.text = value.ToString();
                yield return null;
            }

            target.text = to.ToString();
        }

        private void EnsureDefaultReferences()
        {
            if (facilityNodeRoot == null)
            {
                facilityNodeRoot = stationViewRect;
            }
        }

        private GameObject CreateDefaultFacilityNode(RectTransform parent)
        {
            GameObject go = new GameObject("FacilityNode", typeof(RectTransform), typeof(Image), typeof(Button), typeof(FacilityNodeView));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 120f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.3f, 0.45f, 0.9f, 0.95f);

            GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(rt, false);
            RectTransform labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            Text label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            return go;
        }

        private Text CreateDefaultFloatingTip(RectTransform parent)
        {
            GameObject go = new GameObject("FloatingTip", typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(500f, 70f);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.95f, 0.5f, 1f);

            return text;
        }

        #endregion

        #region 内部节点组件

        /// <summary>
        /// 设施节点视图组件。
        /// </summary>
        private class FacilityNodeView : MonoBehaviour
        {
            private FacilityData _data;
            private Action<FacilityData> _onClick;
            private RectTransform _rt;
            private Text _label;
            private Button _button;

            public void Bind(FacilityData data, Action<FacilityData> onClick)
            {
                _data = data;
                _onClick = onClick;

                if (_rt == null)
                {
                    _rt = transform as RectTransform;
                }

                if (_button == null)
                {
                    _button = GetComponent<Button>();
                    if (_button != null)
                    {
                        _button.onClick.RemoveAllListeners();
                        _button.onClick.AddListener(HandleClick);
                    }
                }

                if (_label == null)
                {
                    _label = GetComponentInChildren<Text>();
                }

                if (_rt != null)
                {
                    _rt.anchoredPosition = data.position;
                }

                if (_label != null)
                {
                    _label.text = $"{data.name}\nLv.{data.level}";
                }

                gameObject.name = $"Facility_{data.id}";
                gameObject.SetActive(true);
            }

            private void HandleClick()
            {
                _onClick?.Invoke(_data);
            }
        }

        #endregion
    }

    /// <summary>
    /// 资源类型（项目内轻量定义）。
    /// </summary>
    /// <summary>
    /// 设施数据（主界面展示用）。
    /// </summary>
    [Serializable]
    public class FacilityData
    {
        public string id;
        public string name;
        public int level;
        public bool unlocked = true;
        public Vector2 position;
    }

    /// <summary>
    /// 旅客数据（主界面提示用）。
    /// </summary>
    [Serializable]
    public class VisitorData
    {
        public int id;
        public string name;
        public VisitorType type;
    }
}
