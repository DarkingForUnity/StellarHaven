using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StellarHaven.UI
{
    /// <summary>
    /// 资源类型。
    /// </summary>
    public enum ResourceType
    {
        Coin = 0,
        Energy = 1,
        Visitor = 2,
        Material = 3,
        Research = 4
    }

    /// <summary>
    /// 可复用资源显示组件。
    /// </summary>
    public class UIResourceDisplay : MonoBehaviour
    {
        [System.Serializable]
        private struct ResourceVisualConfig
        {
            public ResourceType type;
            public Sprite icon;
            public Color color;
            public string format;
        }

        #region Inspector

        [Header("Base")]
        [SerializeField, Tooltip("资源类型")]
        private ResourceType type = ResourceType.Coin;

        [SerializeField, Tooltip("当前数量")]
        private int amount;

        [SerializeField, Tooltip("最大数量（<=0 表示无上限）")]
        private int maxAmount;

        [SerializeField, Range(0, 100), Tooltip("警告阈值百分比")]
        private int warningThreshold = 20;

        [Header("References")]
        [SerializeField, Tooltip("资源图标")]
        private Image icon;

        [SerializeField, Tooltip("资源数值文本")]
        private Text amountText;

        [SerializeField, Tooltip("警告图标")]
        private Image warningIcon;

        [SerializeField, Tooltip("变化提示文本")]
        private Text changeIndicatorText;

        [SerializeField, Tooltip("变化提示节点")]
        private RectTransform changeIndicatorRect;

        [Header("Animation")]
        [SerializeField, Tooltip("数字滚动时长")]
        private float valueAnimationDuration = 0.28f;

        [SerializeField, Tooltip("变化提示显示时长")]
        private float changeIndicatorDuration = 1.5f;

        [SerializeField, Tooltip("变化提示上浮距离")]
        private float changeIndicatorMoveY = 30f;

        [Header("Style")]
        [SerializeField, Tooltip("资源样式配置")]
        private ResourceVisualConfig[] visualConfigs =
        {
            new ResourceVisualConfig{ type = ResourceType.Coin,    icon = null, color = new Color(1f, 0.86f, 0.22f, 1f), format = "N0"},
            new ResourceVisualConfig{ type = ResourceType.Energy,  icon = null, color = new Color(0.40f, 0.96f, 0.52f, 1f), format = "N0"},
            new ResourceVisualConfig{ type = ResourceType.Visitor, icon = null, color = new Color(0.44f, 0.77f, 1f, 1f), format = "N0"},
            new ResourceVisualConfig{ type = ResourceType.Material,icon = null, color = new Color(0.90f, 0.64f, 0.35f, 1f), format = "N0"},
            new ResourceVisualConfig{ type = ResourceType.Research,icon = null, color = new Color(0.75f, 0.56f, 1f, 1f), format = "N0"},
        };

        #endregion

        #region Static Cache

        private static readonly Dictionary<ResourceType, Sprite> IconMap = new Dictionary<ResourceType, Sprite>(8);
        private static readonly Dictionary<ResourceType, Color> ColorMap = new Dictionary<ResourceType, Color>(8);
        private static readonly Dictionary<ResourceType, string> FormatMap = new Dictionary<ResourceType, string>(8);

        #endregion

        #region Runtime

        private Coroutine _valueCoroutine;
        private Coroutine _changeIndicatorCoroutine;
        private CanvasGroup _changeIndicatorCanvasGroup;
        private int _displayedAmount;

        #endregion

        #region Properties

        /// <summary>
        /// 资源类型。
        /// </summary>
        public ResourceType Type
        {
            get => type;
            set
            {
                type = value;
                ApplyVisual();
                RefreshDisplay(false);
            }
        }

        /// <summary>
        /// 当前数量。
        /// </summary>
        public int Amount
        {
            get => amount;
            set => SetAmount(value, true);
        }

        /// <summary>
        /// 警告阈值（百分比）。
        /// </summary>
        public int WarningThreshold
        {
            get => warningThreshold;
            set => SetWarningThreshold(value);
        }

        #endregion

        #region Unity

        private void Awake()
        {
            CacheStaticVisualMap();
            EnsureIndicatorReferences();
            ApplyVisual();
            RefreshDisplay(false);
        }

        private void OnDisable()
        {
            if (_valueCoroutine != null)
            {
                StopCoroutine(_valueCoroutine);
                _valueCoroutine = null;
            }

            if (_changeIndicatorCoroutine != null)
            {
                StopCoroutine(_changeIndicatorCoroutine);
                _changeIndicatorCoroutine = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 设置数量。
        /// </summary>
        /// <param name="newAmount">新数量。</param>
        /// <param name="animate">是否播放数字动画。</param>
        public void SetAmount(int newAmount, bool animate = true)
        {
            int clamped = Mathf.Max(0, newAmount);
            int old = amount;
            amount = clamped;

            if (animate)
            {
                AnimateValue(old, amount, valueAnimationDuration);
            }
            else
            {
                _displayedAmount = amount;
                UpdateAmountText(_displayedAmount);
            }

            UpdateWarningState();
        }

        /// <summary>
        /// 增减数量。
        /// </summary>
        /// <param name="delta">变化值。</param>
        /// <param name="animate">是否播放动画。</param>
        public void AddAmount(int delta, bool animate = true)
        {
            if (delta == 0)
            {
                return;
            }

            SetAmount(amount + delta, animate);
            ShowChangeIndicator(delta);
        }

        /// <summary>
        /// 设置最大数量（用于阈值警告）。
        /// </summary>
        /// <param name="max">最大值，<=0 表示无上限。</param>
        public void SetMaxAmount(int max)
        {
            maxAmount = Mathf.Max(0, max);
            UpdateWarningState();
        }

        /// <summary>
        /// 数字滚动动画。
        /// </summary>
        /// <param name="from">起始值。</param>
        /// <param name="to">目标值。</param>
        /// <param name="duration">时长。</param>
        public void AnimateValue(int from, int to, float duration)
        {
            if (_valueCoroutine != null)
            {
                StopCoroutine(_valueCoroutine);
            }

            _valueCoroutine = StartCoroutine(AnimateValueCoroutine(from, to, duration));
        }

        /// <summary>
        /// 显示变化提示。
        /// </summary>
        /// <param name="delta">变化值。</param>
        public void ShowChangeIndicator(int delta)
        {
            if (changeIndicatorText == null || changeIndicatorRect == null)
            {
                return;
            }

            EnsureIndicatorReferences();

            if (_changeIndicatorCoroutine != null)
            {
                StopCoroutine(_changeIndicatorCoroutine);
            }

            bool increase = delta > 0;
            int absValue = Mathf.Abs(delta);
            changeIndicatorText.text = $"{(increase ? "+" : "-")}{FormatNumber(absValue)}";
            changeIndicatorText.color = increase
                ? new Color(0.36f, 0.95f, 0.46f, 1f)
                : new Color(1f, 0.35f, 0.35f, 1f);
            changeIndicatorRect.gameObject.SetActive(true);

            _changeIndicatorCoroutine = StartCoroutine(ShowChangeIndicatorCoroutine());
        }

        /// <summary>
        /// 设置警告阈值百分比。
        /// </summary>
        /// <param name="percentage">0-100。</param>
        public void SetWarningThreshold(int percentage)
        {
            warningThreshold = Mathf.Clamp(percentage, 0, 100);
            UpdateWarningState();
        }

        /// <summary>
        /// 格式化数字（千分位）。
        /// </summary>
        public static string FormatNumber(int num)
        {
            return num.ToString("N0");
        }

        /// <summary>
        /// 获取资源图标。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        public static Sprite GetResourceIcon(ResourceType resourceType)
        {
            IconMap.TryGetValue(resourceType, out Sprite sprite);
            return sprite;
        }

        /// <summary>
        /// 获取资源颜色。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        public static Color GetResourceColor(ResourceType resourceType)
        {
            if (ColorMap.TryGetValue(resourceType, out Color color))
            {
                return color;
            }

            return Color.white;
        }

        #endregion

        #region Internal

        private void CacheStaticVisualMap()
        {
            if (visualConfigs == null)
            {
                return;
            }

            for (int i = 0; i < visualConfigs.Length; i++)
            {
                ResourceVisualConfig cfg = visualConfigs[i];
                IconMap[cfg.type] = cfg.icon;
                ColorMap[cfg.type] = cfg.color;
                FormatMap[cfg.type] = string.IsNullOrWhiteSpace(cfg.format) ? "N0" : cfg.format;
            }
        }

        private void ApplyVisual()
        {
            if (icon != null)
            {
                icon.sprite = GetResourceIcon(type);
                icon.color = GetResourceColor(type);
            }

            if (amountText != null)
            {
                amountText.color = GetResourceColor(type);
            }
        }

        private void RefreshDisplay(bool animate)
        {
            if (animate)
            {
                AnimateValue(_displayedAmount, amount, valueAnimationDuration);
            }
            else
            {
                _displayedAmount = amount;
                UpdateAmountText(_displayedAmount);
            }

            UpdateWarningState();
        }

        private IEnumerator AnimateValueCoroutine(int from, int to, float duration)
        {
            float timer = 0f;
            float safeDuration = Mathf.Max(0.05f, duration);

            while (timer < safeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / safeDuration);
                _displayedAmount = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                UpdateAmountText(_displayedAmount);
                yield return null;
            }

            _displayedAmount = to;
            UpdateAmountText(_displayedAmount);
            _valueCoroutine = null;
        }

        private void UpdateAmountText(int value)
        {
            if (amountText == null)
            {
                return;
            }

            string format = "N0";
            if (FormatMap.TryGetValue(type, out string customFormat))
            {
                format = customFormat;
            }

            amountText.text = value.ToString(format);
        }

        private void UpdateWarningState()
        {
            if (warningIcon == null)
            {
                return;
            }

            if (maxAmount <= 0)
            {
                warningIcon.gameObject.SetActive(false);
                return;
            }

            float percentage = maxAmount > 0 ? (float)amount / maxAmount * 100f : 0f;
            warningIcon.gameObject.SetActive(percentage <= warningThreshold);
        }

        private void EnsureIndicatorReferences()
        {
            if (changeIndicatorRect == null && changeIndicatorText != null)
            {
                changeIndicatorRect = changeIndicatorText.rectTransform;
            }

            if (changeIndicatorRect != null && _changeIndicatorCanvasGroup == null)
            {
                _changeIndicatorCanvasGroup = changeIndicatorRect.GetComponent<CanvasGroup>();
                if (_changeIndicatorCanvasGroup == null)
                {
                    _changeIndicatorCanvasGroup = changeIndicatorRect.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private IEnumerator ShowChangeIndicatorCoroutine()
        {
            Vector2 start = changeIndicatorRect.anchoredPosition;
            Vector2 end = start + new Vector2(0f, changeIndicatorMoveY);
            float timer = 0f;
            float duration = Mathf.Max(0.2f, changeIndicatorDuration);

            _changeIndicatorCanvasGroup.alpha = 1f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                changeIndicatorRect.anchoredPosition = Vector2.Lerp(start, end, t);
                _changeIndicatorCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            changeIndicatorRect.anchoredPosition = start;
            _changeIndicatorCanvasGroup.alpha = 0f;
            changeIndicatorRect.gameObject.SetActive(false);
            _changeIndicatorCoroutine = null;
        }

        #endregion
    }
}
