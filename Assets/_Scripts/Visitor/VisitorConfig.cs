using UnityEngine;

namespace StellarHaven.Visitor
{
    /// <summary>
    /// 旅客配置数据（ScriptableObject）
    /// 用于在 Inspector 中配置旅客类型属性
    /// </summary>
    [CreateAssetMenu(fileName = "NewVisitorConfig", menuName = "StellarHaven/Visitor Config", order = 1)]
    public class VisitorConfig : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("旅客类型 ID")]
        public int id;
        
        [Tooltip("旅客类型名称")]
        public string visitorName;
        
        [Tooltip("旅客类型枚举")]
        public VisitorType type;
        
        [Header("属性")]
        [Tooltip("预算范围 (最小值)")]
        public int minBudget;
        
        [Tooltip("预算范围 (最大值)")]
        public int maxBudget;
        
        [Tooltip("耐心值 (秒)")]
        public float patience;
        
        [Tooltip("移动速度")]
        public float moveSpeed = 3f;
        
        [Header("偏好服务")]
        [Tooltip("偏好的服务类型")]
        public ServiceType[] preferredServices;
        
        [Tooltip("偏好服务满意度加成 (%)")]
        [Range(0, 50)]
        public float preferenceBonus = 20f;
        
        [Header("对话")]
        [Tooltip("到达时的对话")]
        [TextArea(2, 5)]
        public string[] arrivalDialogues;
        
        [Tooltip("离开时的对话")]
        [TextArea(2, 5)]
        public string[] departureDialogues;
        
        [Header("外观")]
        [Tooltip("旅客预制体")]
        public GameObject visitorPrefab;
        
        [Tooltip("头像精灵")]
        public Sprite portrait;
        
        /// <summary>
        /// 获取随机预算
        /// </summary>
        public int GetRandomBudget()
        {
            return Random.Range(minBudget, maxBudget + 1);
        }
        
        /// <summary>
        /// 检查服务是否是偏好服务
        /// </summary>
        public bool IsPreferredService(ServiceType service)
        {
            foreach (ServiceType preferred in preferredServices)
            {
                if (preferred == service)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// 获取随机到达对话
        /// </summary>
        public string GetRandomArrivalDialogue()
        {
            if (arrivalDialogues == null || arrivalDialogues.Length == 0)
                return "你好！";
            
            return arrivalDialogues[Random.Range(0, arrivalDialogues.Length)];
        }
        
        /// <summary>
        /// 获取随机离开对话
        /// </summary>
        public string GetRandomDepartureDialogue()
        {
            if (departureDialogues == null || departureDialogues.Length == 0)
                return "再见！";
            
            return departureDialogues[Random.Range(0, departureDialogues.Length)];
        }
    }
    
    /// <summary>
    /// 旅客类型枚举
    /// </summary>
    public enum VisitorType
    {
        Merchant = 0,      // 商旅
        Explorer = 1,      // 探险家
        Immigrant = 2,     // 移民
        Noble = 3,         // 贵族
        Smuggler = 4,      // 走私者
        Scientist = 5      // 科学家
    }
    
    /// <summary>
    /// 服务类型枚举
    /// </summary>
    public enum ServiceType
    {
        Fuel = 0,          // 燃料补给
        Repair = 1,        // 维修服务
        Food = 2,          // 餐饮服务
        Rest = 3,          // 休息住宿
        Shop = 4,          // 购物
        VIP = 5,           // VIP 服务
        Research = 6,      // 科研服务
        Info = 7           // 信息服务
    }
}
