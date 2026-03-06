using UnityEngine;

namespace StellarHaven.Visitor
{
    /// <summary>
    /// 旅客需求数据（运行时）。
    /// 轻量级纯 C# 类，适合频繁创建与销毁。
    /// </summary>
    [System.Serializable]
    public class VisitorNeed
    {
        private const float MaxUrgency = 100f;
        private const float DefaultUrgencyGrowthRate = 6f;

        /// <summary>
        /// 服务类型。
        /// </summary>
        public ServiceType ServiceType { get; private set; }

        /// <summary>
        /// 需求总量。
        /// </summary>
        public int RequiredQuantity { get; private set; }

        /// <summary>
        /// 已完成数量。
        /// </summary>
        public int CompletedQuantity { get; private set; }

        /// <summary>
        /// 完成度（0-100）。
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// 紧急度（0-100），会随时间增长。
        /// </summary>
        public float Urgency { get; private set; }

        /// <summary>
        /// 是否完成。
        /// </summary>
        public bool IsCompleted => CompletedQuantity >= RequiredQuantity;

        /// <summary>
        /// 紧急度增长速率（每秒）。
        /// </summary>
        public float UrgencyGrowthRate { get; private set; }

        /// <summary>
        /// 创建旅客需求。
        /// </summary>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="requiredQuantity">需求总量。</param>
        public VisitorNeed(ServiceType serviceType, int requiredQuantity)
            : this(serviceType, requiredQuantity, 0f, DefaultUrgencyGrowthRate)
        {
        }

        /// <summary>
        /// 创建旅客需求（可指定初始紧急度和增长速率）。
        /// </summary>
        /// <param name="serviceType">服务类型。</param>
        /// <param name="requiredQuantity">需求总量。</param>
        /// <param name="initialUrgency">初始紧急度（0-100）。</param>
        /// <param name="urgencyGrowthRate">紧急度增长速率（每秒）。</param>
        public VisitorNeed(ServiceType serviceType, int requiredQuantity, float initialUrgency, float urgencyGrowthRate)
        {
            ServiceType = serviceType;
            RequiredQuantity = Mathf.Max(1, requiredQuantity);
            CompletedQuantity = 0;
            Progress = 0f;
            Urgency = Mathf.Clamp(initialUrgency, 0f, MaxUrgency);
            UrgencyGrowthRate = Mathf.Max(0f, urgencyGrowthRate);
        }

        /// <summary>
        /// 更新需求状态（每帧调用）。
        /// 未完成需求会随时间增加紧急度。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间。</param>
        public void Update(float deltaTime)
        {
            if (IsCompleted)
            {
                return;
            }

            Urgency = Mathf.Min(MaxUrgency, Urgency + deltaTime * UrgencyGrowthRate);
        }

        /// <summary>
        /// 完成指定数量的需求。
        /// </summary>
        /// <param name="amount">完成数量。</param>
        public void Complete(int amount)
        {
            if (amount <= 0 || IsCompleted)
            {
                return;
            }

            CompletedQuantity = Mathf.Min(RequiredQuantity, CompletedQuantity + amount);
            Progress = Mathf.Clamp01((float)CompletedQuantity / RequiredQuantity) * 100f;
        }

        /// <summary>
        /// 获取完成百分比（0-100）。
        /// </summary>
        public float GetProgressPercentage()
        {
            return Progress;
        }

        /// <summary>
        /// 获取剩余数量。
        /// </summary>
        public int GetRemainingQuantity()
        {
            return Mathf.Max(0, RequiredQuantity - CompletedQuantity);
        }

        /// <summary>
        /// 获取紧急度倍率（1.0-3.0）。
        /// 可用于影响耐心下降速度。
        /// </summary>
        public float GetUrgencyMultiplier()
        {
            return 1f + (Urgency / MaxUrgency) * 2f;
        }

        /// <summary>
        /// 返回用于 UI 的显示文本。
        /// </summary>
        public string GetDisplayText()
        {
            return $"{ServiceType} {CompletedQuantity}/{RequiredQuantity} ({Progress:0.#}%)";
        }

        /// <summary>
        /// 输出需求调试信息。
        /// </summary>
        public override string ToString()
        {
            return $"Need[{ServiceType}] {CompletedQuantity}/{RequiredQuantity}, " +
                   $"Progress={Progress:0.#}%, Urgency={Urgency:0.#}, " +
                   $"Multiplier={GetUrgencyMultiplier():0.##}";
        }
    }
}
