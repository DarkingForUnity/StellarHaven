using System;
using System.Collections;
using System.Collections.Generic;
using StellarHaven.Visitor;
using UnityEngine;
using UnityEngine.UI;

namespace StellarHaven.UI
{
    /// <summary>
    /// 旅客接待界面。
    /// </summary>
    public class UIVisitorService : UIBase
    {
        #region VisitorInfoCard

        [Header("VisitorInfoCard")]
        [SerializeField, Tooltip("旅客头像")]
        private Image visitorAvatarImage;

        [SerializeField, Tooltip("旅客姓名文本")]
        private Text visitorNameText;

        [SerializeField, Tooltip("旅客类型文本")]
        private Text visitorTypeText;

        [SerializeField, Tooltip("旅客类型背景")]
        private Image visitorTypeBackground;

        [SerializeField, Tooltip("旅客描述文本")]
        private Text visitorDescriptionText;

        #endregion

        #region NeedList

        [Header("NeedList")]
        [SerializeField, Tooltip("需求列表滚动视图")]
        private ScrollRect needScrollRect;

        [SerializeField, Tooltip("需求列表内容根节点")]
        private RectTransform needContentRoot;

        [SerializeField, Tooltip("需求项预制体")]
        private GameObject needItemPrefab;

        [SerializeField, Tooltip("需求对象池初始数量")]
        private int needPoolInitialSize = 6;

        #endregion

        #region DialogArea

        [Header("DialogArea")]
        [SerializeField, Tooltip("对话文本")]
        private Text dialogText;

        [SerializeField, Tooltip("对话选项按钮列表（3-4个）")]
        private Button[] dialogOptionButtons;

        [SerializeField, Tooltip("对话选项按钮文本列表（与按钮顺序一致）")]
        private Text[] dialogOptionTexts;

        #endregion

        #region RelationshipBar

        [Header("RelationshipBar")]
        [SerializeField, Tooltip("好感度进度条")]
        private Slider relationshipSlider;

        [SerializeField, Tooltip("好感度等级文本")]
        private Text relationshipLevelText;

        [SerializeField, Tooltip("好感度变化动画时长")]
        private float relationshipTweenDuration = 0.35f;

        #endregion

        #region ActionBar

        [Header("ActionBar")]
        [SerializeField, Tooltip("记录按钮")]
        private Button recordButton;

        [SerializeField, Tooltip("赠礼按钮")]
        private Button giftButton;

        [SerializeField, Tooltip("完成服务按钮")]
        private Button completeServiceButton;

        [SerializeField, Tooltip("拒绝按钮")]
        private Button refuseButton;

        [SerializeField, Tooltip("求助按钮")]
        private Button helpButton;

        #endregion

        #region Animation

        [Header("Animation")]
        [SerializeField, Tooltip("界面根节点（用于滑入滑出）")]
        private RectTransform panelRoot;

        [SerializeField, Tooltip("打开动画时长")]
        private float openDuration = 0.22f;

        [SerializeField, Tooltip("关闭动画时长")]
        private float closeDuration = 0.18f;

        [SerializeField, Tooltip("滑动偏移X（从右侧滑入）")]
        private float slideOffsetX = 900f;

        #endregion

        #region Runtime

        private StellarHaven.Visitor.Visitor _currentVisitor;
        private VisitorData _currentVisitorData;

        private readonly Dictionary<string, NeedItemView> _activeNeedItems = new Dictionary<string, NeedItemView>();
        private readonly Stack<NeedItemView> _needItemPool = new Stack<NeedItemView>();
        private readonly List<NeedItemView> _needItemOrder = new List<NeedItemView>();

        private Coroutine _relationshipCoroutine;
        private Coroutine _panelMoveCoroutine;

        private readonly List<string> _dialogLines = new List<string>(24);

        #endregion

        #region Event

        /// <summary>
        /// 请求完成需求事件（needId）。
        /// </summary>
        public event Action<string> OnNeedCompleteRequested;

        /// <summary>
        /// 对话选项点击事件（optionId）。
        /// </summary>
        public event Action<string> OnDialogOptionSelected;

        /// <summary>
        /// 操作按钮事件（actionId）。
        /// </summary>
        public event Action<string> OnActionButtonClicked;

        #endregion

        #region UIBase

        /// <summary>
        /// 打开界面时调用。
        /// </summary>
        public override void OnOpen(params object[] parameters)
        {
            base.OnOpen(parameters);
            BindButtonEvents();
            PlayOpenAnimation();
        }

        /// <summary>
        /// 关闭界面时调用。
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            PlayCloseAnimation();
        }

        protected override void Awake()
        {
            base.Awake();

            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (needContentRoot == null && needScrollRect != null)
            {
                needContentRoot = needScrollRect.content;
            }

            WarmupNeedItemPool();
            BindButtonEvents();
        }

        #endregion

        #region 数据设置

        /// <summary>
        /// 设置旅客对象并刷新界面。
        /// </summary>
        /// <param name="visitor">旅客对象。</param>
        public void SetVisitor(StellarHaven.Visitor.Visitor visitor)
        {
            _currentVisitor = visitor;
            if (visitor == null)
            {
                return;
            }

            VisitorData data = new VisitorData
            {
                id = visitor.Id,
                name = visitor.Config != null ? visitor.Config.visitorName : "未知旅客",
                type = visitor.Config != null ? visitor.Config.type : VisitorType.Merchant
            };
            SetVisitorData(data);

            if (visitor.Config != null)
            {
                visitorDescriptionText.text = visitor.Config.GetRandomArrivalDialogue();
            }

            RefreshNeedList(visitor.ActiveNeeds);
        }

        /// <summary>
        /// 设置旅客展示数据并刷新信息卡片。
        /// </summary>
        /// <param name="data">旅客数据。</param>
        public void SetVisitorData(VisitorData data)
        {
            _currentVisitorData = data;
            if (data == null)
            {
                return;
            }

            if (visitorNameText != null)
            {
                visitorNameText.text = string.IsNullOrWhiteSpace(data.name) ? "未知旅客" : data.name;
            }

            if (visitorTypeText != null)
            {
                visitorTypeText.text = GetVisitorTypeLabel(data.type);
            }

            if (visitorTypeBackground != null)
            {
                visitorTypeBackground.color = GetVisitorTypeColor(data.type);
            }
        }

        #endregion

        #region 需求列表

        /// <summary>
        /// 刷新需求列表。
        /// </summary>
        /// <param name="needs">需求集合。</param>
        public void RefreshNeedList(List<VisitorNeed> needs)
        {
            RecycleAllNeedItems();

            if (needs == null || needs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < needs.Count; i++)
            {
                VisitorNeed need = needs[i];
                if (need == null)
                {
                    continue;
                }

                string needId = GetNeedId(need, i);
                NeedItemView view = GetNeedItemFromPool();
                view.Bind(needId, need, HandleNeedCompleteClicked);
                _activeNeedItems[needId] = view;
                _needItemOrder.Add(view);
            }
        }

        /// <summary>
        /// 更新指定需求进度。
        /// </summary>
        /// <param name="needId">需求 ID。</param>
        /// <param name="progress">进度（0-100）。</param>
        public void UpdateNeedProgress(string needId, float progress)
        {
            if (string.IsNullOrWhiteSpace(needId))
            {
                return;
            }

            if (!_activeNeedItems.TryGetValue(needId, out NeedItemView view))
            {
                return;
            }

            view.SetProgress(progress);

            if (progress >= 100f)
            {
                StartCoroutine(PlayNeedCompletedEffect(view));
            }
        }

        private void HandleNeedCompleteClicked(string needId)
        {
            OnNeedCompleteRequested?.Invoke(needId);
        }

        #endregion

        #region 对话系统

        /// <summary>
        /// 显示对话内容与选项。
        /// </summary>
        /// <param name="speaker">说话者。</param>
        /// <param name="content">对话内容。</param>
        /// <param name="options">选项列表。</param>
        public void ShowDialog(string speaker, string content, List<DialogOption> options)
        {
            string line = $"{speaker}: {content}";
            AddDialogLine(line, false);
            ApplyDialogOptions(options);
        }

        /// <summary>
        /// 追加一条对话文本。
        /// </summary>
        /// <param name="text">文本内容。</param>
        /// <param name="isPlayer">是否玩家发言。</param>
        public void AddDialogLine(string text, bool isPlayer)
        {
            string prefix = isPlayer ? "你" : "访客";
            string line = text;
            if (!text.Contains(":"))
            {
                line = $"{prefix}: {text}";
            }

            _dialogLines.Add(line);
            if (_dialogLines.Count > 8)
            {
                _dialogLines.RemoveAt(0);
            }

            if (dialogText != null)
            {
                dialogText.text = string.Join("\n", _dialogLines);
            }
        }

        /// <summary>
        /// 清空对话区域。
        /// </summary>
        public void ClearDialog()
        {
            _dialogLines.Clear();
            if (dialogText != null)
            {
                dialogText.text = string.Empty;
            }

            ApplyDialogOptions(null);
        }

        private void ApplyDialogOptions(List<DialogOption> options)
        {
            if (dialogOptionButtons == null)
            {
                return;
            }

            for (int i = 0; i < dialogOptionButtons.Length; i++)
            {
                bool active = options != null && i < options.Count;
                Button btn = dialogOptionButtons[i];
                if (btn == null)
                {
                    continue;
                }

                btn.gameObject.SetActive(active);
                btn.onClick.RemoveAllListeners();

                if (!active)
                {
                    continue;
                }

                DialogOption option = options[i];
                if (dialogOptionTexts != null && i < dialogOptionTexts.Length && dialogOptionTexts[i] != null)
                {
                    dialogOptionTexts[i].text = option.text;
                }

                string optionId = option.id;
                btn.onClick.AddListener(() => OnDialogOptionSelected?.Invoke(optionId));
            }
        }

        #endregion

        #region 好感度

        /// <summary>
        /// 更新好感度显示。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="maxValue">最大值。</param>
        public void UpdateRelationship(float value, float maxValue)
        {
            if (relationshipSlider == null)
            {
                return;
            }

            float current = relationshipSlider.value;
            float target = maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;

            if (_relationshipCoroutine != null)
            {
                StopCoroutine(_relationshipCoroutine);
            }

            _relationshipCoroutine = StartCoroutine(TweenRelationship(current, target));

            int level = Mathf.Max(1, Mathf.FloorToInt(target * 5f) + 1);
            if (relationshipLevelText != null)
            {
                relationshipLevelText.text = $"关系 Lv.{level}";
            }

            if (target > current)
            {
                StartCoroutine(PlayRelationshipLevelUpEffect());
            }
        }

        private IEnumerator TweenRelationship(float from, float to)
        {
            float timer = 0f;
            float duration = Mathf.Max(0.1f, relationshipTweenDuration);

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                relationshipSlider.value = Mathf.Lerp(from, to, t);
                yield return null;
            }

            relationshipSlider.value = to;
        }

        #endregion

        #region 操作按钮

        /// <summary>
        /// 绑定底部操作按钮事件。
        /// </summary>
        public void BindButtonEvents()
        {
            BindActionButton(recordButton, "record");
            BindActionButton(giftButton, "gift");
            BindActionButton(completeServiceButton, "complete");
            BindActionButton(refuseButton, "refuse");
            BindActionButton(helpButton, "help");
        }

        /// <summary>
        /// 设置操作按钮可用状态。
        /// </summary>
        /// <param name="record">记录按钮。</param>
        /// <param name="gift">赠礼按钮。</param>
        /// <param name="complete">完成服务按钮。</param>
        /// <param name="refuse">拒绝按钮。</param>
        /// <param name="help">求助按钮。</param>
        public void SetActionButtonsInteractable(bool record, bool gift, bool complete, bool refuse, bool help)
        {
            SetButtonState(recordButton, record);
            SetButtonState(giftButton, gift);
            SetButtonState(completeServiceButton, complete);
            SetButtonState(refuseButton, refuse);
            SetButtonState(helpButton, help);
        }

        private void BindActionButton(Button button, string actionId)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnActionButtonClicked?.Invoke(actionId));
        }

        private void SetButtonState(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
        }

        #endregion

        #region 动画

        private void PlayOpenAnimation()
        {
            if (panelRoot == null)
            {
                return;
            }

            if (_panelMoveCoroutine != null)
            {
                StopCoroutine(_panelMoveCoroutine);
            }

            Vector2 end = Vector2.zero;
            Vector2 start = new Vector2(slideOffsetX, end.y);
            _panelMoveCoroutine = StartCoroutine(SlidePanel(panelRoot, start, end, openDuration));
        }

        private void PlayCloseAnimation()
        {
            if (panelRoot == null)
            {
                return;
            }

            if (_panelMoveCoroutine != null)
            {
                StopCoroutine(_panelMoveCoroutine);
            }

            Vector2 start = panelRoot.anchoredPosition;
            Vector2 end = new Vector2(slideOffsetX, start.y);
            _panelMoveCoroutine = StartCoroutine(SlidePanel(panelRoot, start, end, closeDuration));
        }

        private IEnumerator SlidePanel(RectTransform target, Vector2 from, Vector2 to, float duration)
        {
            target.anchoredPosition = from;
            float timer = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                target.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }

            target.anchoredPosition = to;
        }

        private IEnumerator PlayNeedCompletedEffect(NeedItemView view)
        {
            if (view == null)
            {
                yield break;
            }

            RectTransform rt = view.transform as RectTransform;
            if (rt == null)
            {
                yield break;
            }

            Vector3 baseScale = Vector3.one;
            float timer = 0f;
            const float duration = 0.18f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                rt.localScale = baseScale * scale;
                yield return null;
            }

            rt.localScale = baseScale;
        }

        private IEnumerator PlayRelationshipLevelUpEffect()
        {
            if (relationshipLevelText == null)
            {
                yield break;
            }

            Transform target = relationshipLevelText.transform;
            Vector3 origin = target.localScale;
            float timer = 0f;
            const float duration = 0.22f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                target.localScale = origin * scale;
                yield return null;
            }

            target.localScale = origin;
        }

        #endregion

        #region NeedPool

        private void WarmupNeedItemPool()
        {
            for (int i = 0; i < Mathf.Max(0, needPoolInitialSize); i++)
            {
                NeedItemView view = CreateNeedItem();
                view.gameObject.SetActive(false);
                _needItemPool.Push(view);
            }
        }

        private NeedItemView GetNeedItemFromPool()
        {
            NeedItemView view = _needItemPool.Count > 0 ? _needItemPool.Pop() : CreateNeedItem();
            view.gameObject.SetActive(true);
            view.transform.SetParent(needContentRoot, false);
            return view;
        }

        private void RecycleNeedItem(NeedItemView view)
        {
            if (view == null)
            {
                return;
            }

            view.ResetView();
            view.gameObject.SetActive(false);
            view.transform.SetParent(transform, false);
            _needItemPool.Push(view);
        }

        private void RecycleAllNeedItems()
        {
            for (int i = 0; i < _needItemOrder.Count; i++)
            {
                RecycleNeedItem(_needItemOrder[i]);
            }

            _needItemOrder.Clear();
            _activeNeedItems.Clear();
        }

        private NeedItemView CreateNeedItem()
        {
            GameObject go;
            if (needItemPrefab != null)
            {
                go = Instantiate(needItemPrefab);
            }
            else
            {
                go = BuildDefaultNeedItem();
            }

            NeedItemView view = go.GetComponent<NeedItemView>();
            if (view == null)
            {
                view = go.AddComponent<NeedItemView>();
            }

            return view;
        }

        private GameObject BuildDefaultNeedItem()
        {
            GameObject root = new GameObject("NeedItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(NeedItemView));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 110f);
            root.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.92f);

            GameObject nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGO.transform.SetParent(root.transform, false);
            RectTransform nameRt = nameGO.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(20f, 20f);
            nameRt.sizeDelta = new Vector2(280f, 36f);
            Text nameText = nameGO.GetComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 24;
            nameText.color = Color.white;

            GameObject progressRoot = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            progressRoot.transform.SetParent(root.transform, false);
            RectTransform prt = progressRoot.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.offsetMin = new Vector2(20f, -28f);
            prt.offsetMax = new Vector2(-170f, -6f);
            progressRoot.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);

            GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(progressRoot.transform, false);
            RectTransform frt = fillGO.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            fillGO.GetComponent<Image>().color = new Color(0.29f, 0.81f, 0.45f, 1f);

            GameObject progressTextGO = new GameObject("ProgressText", typeof(RectTransform), typeof(Text));
            progressTextGO.transform.SetParent(root.transform, false);
            RectTransform ptRt = progressTextGO.GetComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(1f, 0.5f);
            ptRt.anchorMax = new Vector2(1f, 0.5f);
            ptRt.pivot = new Vector2(1f, 0.5f);
            ptRt.anchoredPosition = new Vector2(-180f, -18f);
            ptRt.sizeDelta = new Vector2(150f, 30f);
            Text ptText = progressTextGO.GetComponent<Text>();
            ptText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            ptText.fontSize = 20;
            ptText.alignment = TextAnchor.MiddleRight;
            ptText.color = new Color(0.92f, 0.92f, 0.92f, 1f);

            GameObject btnGO = new GameObject("CompleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(root.transform, false);
            RectTransform bRt = btnGO.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-20f, 0f);
            bRt.sizeDelta = new Vector2(130f, 52f);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.95f, 1f);

            GameObject btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTextGO.transform.SetParent(btnGO.transform, false);
            RectTransform btRt = btnTextGO.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            Text btText = btnTextGO.GetComponent<Text>();
            btText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btText.fontSize = 22;
            btText.alignment = TextAnchor.MiddleCenter;
            btText.color = Color.white;
            btText.text = "完成";

            return root;
        }

        private static string GetNeedId(VisitorNeed need, int index)
        {
            return $"{need.ServiceType}_{index}";
        }

        #endregion

        #region Helper

        private static string GetVisitorTypeLabel(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Merchant: return "商旅";
                case VisitorType.Explorer: return "探险家";
                case VisitorType.Immigrant: return "移民";
                case VisitorType.Noble: return "贵族";
                case VisitorType.Smuggler: return "走私者";
                case VisitorType.Scientist: return "科学家";
                default: return type.ToString();
            }
        }

        private static Color GetVisitorTypeColor(VisitorType type)
        {
            switch (type)
            {
                case VisitorType.Merchant: return new Color(0.29f, 0.73f, 1f, 1f);
                case VisitorType.Explorer: return new Color(0.45f, 0.92f, 0.54f, 1f);
                case VisitorType.Immigrant: return new Color(1f, 0.76f, 0.35f, 1f);
                case VisitorType.Noble: return new Color(0.92f, 0.45f, 1f, 1f);
                case VisitorType.Smuggler: return new Color(1f, 0.42f, 0.42f, 1f);
                case VisitorType.Scientist: return new Color(0.45f, 0.9f, 0.95f, 1f);
                default: return Color.white;
            }
        }

        #endregion

        #region NeedItemView

        /// <summary>
        /// 需求项视图。
        /// </summary>
        private sealed class NeedItemView : MonoBehaviour
        {
            private string _needId;
            private VisitorNeed _needData;
            private Action<string> _onCompleteClick;

            private Text _nameText;
            private Text _progressText;
            private Image _progressFill;
            private Button _completeButton;

            public void Bind(string needId, VisitorNeed need, Action<string> onCompleteClick)
            {
                CacheReferences();

                _needId = needId;
                _needData = need;
                _onCompleteClick = onCompleteClick;

                if (_nameText != null)
                {
                    _nameText.text = need != null ? need.ServiceType.ToString() : "需求";
                }

                SetProgress(need != null ? need.GetProgressPercentage() : 0f);

                if (_completeButton != null)
                {
                    _completeButton.onClick.RemoveAllListeners();
                    _completeButton.onClick.AddListener(() => _onCompleteClick?.Invoke(_needId));
                    _completeButton.interactable = need != null && !need.IsCompleted;
                }
            }

            public void SetProgress(float progress)
            {
                float value = Mathf.Clamp(progress, 0f, 100f);

                if (_progressFill != null)
                {
                    RectTransform rt = _progressFill.rectTransform;
                    rt.anchorMax = new Vector2(value / 100f, 1f);
                }

                if (_progressText != null)
                {
                    _progressText.text = $"{value:0}%";
                }

                if (_completeButton != null)
                {
                    _completeButton.interactable = value < 100f;
                }
            }

            public void ResetView()
            {
                if (_completeButton != null)
                {
                    _completeButton.onClick.RemoveAllListeners();
                }

                _needId = null;
                _needData = null;
                _onCompleteClick = null;
            }

            private void CacheReferences()
            {
                if (_nameText == null)
                {
                    Transform t = transform.Find("Name");
                    if (t != null) _nameText = t.GetComponent<Text>();
                }

                if (_progressText == null)
                {
                    Transform t = transform.Find("ProgressText");
                    if (t != null) _progressText = t.GetComponent<Text>();
                }

                if (_progressFill == null)
                {
                    Transform t = transform.Find("Progress/Fill");
                    if (t != null) _progressFill = t.GetComponent<Image>();
                }

                if (_completeButton == null)
                {
                    Transform t = transform.Find("CompleteButton");
                    if (t != null) _completeButton = t.GetComponent<Button>();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 对话选项数据。
    /// </summary>
    [Serializable]
    public class DialogOption
    {
        public string id;
        public string text;
    }
}
