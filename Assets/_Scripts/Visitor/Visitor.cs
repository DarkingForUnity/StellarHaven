using UnityEngine;
using System;
using System.Collections.Generic;

namespace StellarHaven.Visitor
{
    /// <summary>
    /// 旅客类 - 表示一个具体的旅客实例
    /// </summary>
    public class Visitor : MonoBehaviour
    {
        #region 事件
        
        /// <summary>
        /// 服务完成事件
        /// </summary>
        public event Action<Visitor> OnServiceCompleted;
        
        /// <summary>
        /// 旅客离开事件
        /// </summary>
        public event Action<Visitor> OnLeave;
        
        #endregion
        
        #region 属性
        
        public int Id { get; private set; }
        
        public VisitorConfig Config { get; private set; }
        
        public int CurrentBudget { get; private set; }
        
        public float CurrentPatience { get; private set; }
        
        public float Satisfaction { get; private set; } = 50f;
        
        public VisitorState State { get; private set; } = VisitorState.Spawning;
        
        public List<VisitorNeed> ActiveNeeds { get; private set; } = new List<VisitorNeed>();
        
        public float ArrivalTime { get; private set; }
        
        public float WaitTime { get; private set; }
        
        public Transform CurrentTarget { get; private set; }
        
        #endregion
        
        #region Unity 生命周期
        
        private void Update()
        {
            if (State != VisitorState.Waiting && State != VisitorState.BeingServed)
                return;
            
            // 更新耐心值
            WaitTime += Time.deltaTime;
            CurrentPatience -= Time.deltaTime;
            
            // 检查耐心是否耗尽
            if (CurrentPatience <= 0)
            {
                OnPatienceExpired();
            }
            
            // 更新需求
            UpdateNeeds();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化旅客
        /// </summary>
        public void Initialize(VisitorConfig config, int id)
        {
            Config = config;
            Id = id;
            CurrentBudget = config.GetRandomBudget();
            CurrentPatience = config.patience;
            Satisfaction = 50f;
            ArrivalTime = Time.time;
            WaitTime = 0f;
            
            // 生成初始需求
            GenerateInitialNeeds();
            
            State = VisitorState.Waiting;
            
            Debug.Log($"👤 旅客生成：{config.visitorName} (ID:{id})");
        }
        
        /// <summary>
        /// 生成初始需求
        /// </summary>
        private void GenerateInitialNeeds()
        {
            ActiveNeeds.Clear();
            
            // 根据旅客类型生成 1-3 个需求
            int needCount = UnityEngine.Random.Range(1, 4);
            
            for (int i = 0; i < needCount; i++)
            {
                ServiceType serviceType = GetRandomServiceType();
                VisitorNeed need = new VisitorNeed(serviceType, GetRandomQuantity(serviceType));
                ActiveNeeds.Add(need);
            }
        }
        
        /// <summary>
        /// 获取随机服务类型（基于偏好）
        /// </summary>
        private ServiceType GetRandomServiceType()
        {
            if (Config.preferredServices != null && Config.preferredServices.Length > 0)
            {
                // 70% 概率选择偏好服务
                if (UnityEngine.Random.value < 0.7f)
                {
                    return Config.preferredServices[UnityEngine.Random.Range(0, Config.preferredServices.Length)];
                }
            }
            
            // 随机选择任意服务
            return (ServiceType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(ServiceType)).Length);
        }
        
        /// <summary>
        /// 获取随机数量
        /// </summary>
        private int GetRandomQuantity(ServiceType type)
        {
            switch (type)
            {
                case ServiceType.Fuel:
                    return UnityEngine.Random.Range(100, 500);
                case ServiceType.Repair:
                    return UnityEngine.Random.Range(1, 3);
                case ServiceType.Food:
                    return UnityEngine.Random.Range(1, 5);
                default:
                    return 1;
            }
        }
        
        #endregion
        
        #region 需求更新
        
        /// <summary>
        /// 更新所有需求
        /// </summary>
        private void UpdateNeeds()
        {
            for (int i = ActiveNeeds.Count - 1; i >= 0; i--)
            {
                VisitorNeed need = ActiveNeeds[i];
                
                // 需求随时间增加（如果未完成）
                if (!need.IsCompleted)
                {
                    need.Update(Time.deltaTime);
                }
            }
        }
        
        /// <summary>
        /// 完成一个需求
        /// </summary>
        public void CompleteNeed(ServiceType serviceType, int quantity)
        {
            foreach (VisitorNeed need in ActiveNeeds)
            {
                if (need.ServiceType == serviceType && !need.IsCompleted)
                {
                    need.Complete(quantity);
                    
                    // 增加满意度
                    float bonus = Config.IsPreferredService(serviceType) ? Config.preferenceBonus : 0f;
                    Satisfaction = Mathf.Min(100f, Satisfaction + 10f + bonus);
                    
                    // 消耗预算
                    int cost = GetServiceCost(serviceType, quantity);
                    CurrentBudget -= cost;
                    
                    Debug.Log($"✅ 需求完成：{serviceType} x{quantity}, 满意度：{Satisfaction:F0}");
                    
                    // 检查是否所有需求都完成
                    if (AreAllNeedsCompleted())
                    {
                        OnAllNeedsCompleted();
                    }
                    
                    return;
                }
            }
        }
        
        /// <summary>
        /// 检查所有需求是否完成
        /// </summary>
        private bool AreAllNeedsCompleted()
        {
            foreach (VisitorNeed need in ActiveNeeds)
            {
                if (!need.IsCompleted)
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// 所有需求完成
        /// </summary>
        private void OnAllNeedsCompleted()
        {
            State = VisitorState.ReadyToLeave;
            Debug.Log($"✨ 旅客 {Config.visitorName} 所有需求完成，准备离开");
            
            OnServiceCompleted?.Invoke(this);
        }
        
        /// <summary>
        /// 获取服务费用
        /// </summary>
        private int GetServiceCost(ServiceType type, int quantity)
        {
            int basePrice = 100;
            
            switch (type)
            {
                case ServiceType.Fuel:
                    basePrice = 10;
                    break;
                case ServiceType.Repair:
                    basePrice = 200;
                    break;
                case ServiceType.Food:
                    basePrice = 50;
                    break;
                case ServiceType.VIP:
                    basePrice = 500;
                    break;
            }
            
            return basePrice * quantity;
        }
        
        #endregion
        
        #region 状态控制
        
        /// <summary>
        /// 耐心值耗尽
        /// </summary>
        private void OnPatienceExpired()
        {
            Satisfaction = 0f;
            State = VisitorState.Leaving;
            
            Debug.LogWarning($"⚠️ 旅客 {Config.visitorName} 耐心耗尽，生气离开！");
            
            Leave();
        }
        
        /// <summary>
        /// 旅客离开
        /// </summary>
        public void Leave()
        {
            State = VisitorState.Leaving;
            
            // 播放离开动画/特效
            // TODO: 实现离开逻辑
            
            Debug.Log($"👋 旅客离开：{Config.visitorName}, 最终满意度：{Satisfaction:F0}");
            
            OnLeave?.Invoke(this);
        }
        
        /// <summary>
        /// 设置状态
        /// </summary>
        public void SetState(VisitorState newState)
        {
            State = newState;
            
            switch (newState)
            {
                case VisitorState.BeingServed:
                    Debug.Log($"🔧 正在服务：{Config.visitorName}");
                    break;
                case VisitorState.Waiting:
                    Debug.Log($"⏳ 等待中：{Config.visitorName}");
                    break;
            }
        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 获取最紧急的需求
        /// </summary>
        public VisitorNeed GetMostUrgentNeed()
        {
            VisitorNeed mostUrgent = null;
            float lowestProgress = 100f;
            
            foreach (VisitorNeed need in ActiveNeeds)
            {
                if (!need.IsCompleted && need.Progress < lowestProgress)
                {
                    lowestProgress = need.Progress;
                    mostUrgent = need;
                }
            }
            
            return mostUrgent;
        }
        
        /// <summary>
        /// 获取剩余耐心百分比
        /// </summary>
        public float GetPatiencePercentage()
        {
            return (CurrentPatience / Config.patience) * 100f;
        }
        
        /// <summary>
        /// 获取预计消费
        /// </summary>
        public int GetEstimatedSpending()
        {
            int total = 0;
            foreach (VisitorNeed need in ActiveNeeds)
            {
                total += GetServiceCost(need.ServiceType, need.RequiredQuantity);
            }
            return total;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 旅客状态枚举
    /// </summary>
    public enum VisitorState
    {
        Spawning = 0,        // 生成中
        Waiting = 1,         // 等待服务
        BeingServed = 2,     // 正在服务
        ReadyToLeave = 3,    // 准备离开
        Leaving = 4,         // 离开中
        Left = 5             // 已离开
    }
}
