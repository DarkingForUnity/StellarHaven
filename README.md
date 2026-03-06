# 🚀 星际港湾 (StellarHaven)

> 太空主题休闲经营模拟游戏 | Unity 2022 LTS + AI 辅助开发

---

## 📖 游戏简介

在浩瀚宇宙中，经营你的星际加油站，成为流浪者们心中最温暖的港湾。

**核心玩法：**
- 🛸 接待来自各星球的旅客
- ⛽ 提供燃料、维修、餐饮服务
- 🏗️ 扩建空间站设施
- 🔬 研发新技术
- 🗺️ 探索未知星域

---

## 🛠️ 技术栈

| 项目 | 版本/工具 |
|------|-----------|
| 游戏引擎 | Unity 2022.3.15f1 LTS |
| 开发语言 | C# |
| 版本控制 | Git + GitHub |
| AI 辅助 | ChatGPT Codex |
| 目标平台 | iOS / Android / Steam |

---

## 📁 项目结构

```
StellarHaven/
├── Assets/
│   ├── _Scripts/          # 脚本代码
│   │   ├── Core/         # 核心系统
│   │   ├── Visitor/      # 旅客系统
│   │   ├── Facility/     # 设施系统
│   │   ├── UI/           # UI 系统
│   │   └── Utils/        # 工具类
│   ├── _Prefabs/         # 预制体
│   ├── _Scenes/          # 场景
│   ├── _Art/             # 美术资源
│   ├── _Audio/           # 音频资源
│   ├── _Config/          # 配置文件
│   └── _Resources/       # 动态加载资源
├── README.md
└── .gitignore
```

---

## 🚀 快速开始

### 环境要求

- Unity Hub 最新版
- Unity 2022.3.15f1 LTS
- Git

### 安装步骤

1. **克隆仓库**
```bash
git clone git@github.com:DarkingForUnity/StellarHaven.git
cd StellarHaven
```

2. **用 Unity Hub 打开项目**
   - 点击 "Add" 按钮
   - 选择项目文件夹
   - 选择 Unity 2022.3.15f1

3. **打开场景**
   - 打开 `Assets/_Scenes/Bootstrap.unity`

4. **运行游戏**
   - 点击 Play 按钮

---

## 📋 开发进度

### 阶段一：准备期 (第 1-2 周) 🟢 进行中

- [x] Git 环境配置
- [x] GitHub 仓库创建
- [x] Unity 项目初始化
- [x] GameManager 基础框架
- [ ] ResourceManager
- [ ] 第一次原型测试

### 阶段二：核心期 (第 3-8 周) ⚪ 待开始

- [ ] 旅客系统
- [ ] 设施系统
- [ ] 服务系统
- [ ] UI 框架
- [ ] 新手引导

### 阶段三：内容期 (第 9-14 周) ⚪ 待开始

- [ ] 6 种旅客类型
- [ ] 15+ 设施
- [ ] 科技树系统
- [ ] NPC 剧情

### 阶段四：打磨期 (第 15-18 周) ⚪ 待开始

- [ ] 性能优化
- [ ] Bug 修复
- [ ] 上架材料

### 阶段五：发布期 (第 19-20 周) ⚪ 待开始

- [ ] AppStore 上架
- [ ] 安卓渠道上架
- [ ] Steam 上架

---

## 🤖 AI 辅助开发

本项目采用 **双 AI 协作模式**：

- **当前 AI 助手**: 项目规划、技术决策、文档编写
- **ChatGPT Codex**: 代码生成、代码修改、Debug

详见：[双 AI 协作指南](../休闲游戏探索/双 AI 协作指南.md)

---

## 📝 开发规范

### Git 提交规范

```
feat: 新功能
fix: 修复 Bug
docs: 文档更新
style: 代码格式
refactor: 重构
test: 测试
chore: 构建/工具
```

### 命名规范

- 类名：PascalCase (如 `GameManager`)
- 方法：PascalCase (如 `LoadScene`)
- 变量：camelCase (如 `currentScene`)
- 常量：UPPER_CASE (如 `MAX_VISITORS`)
- 私有字段：_camelCase (如 `_instance`)

---

## 📄 许可证

MIT License

---

## 👨‍💻 开发者

**DarkingForUnity**

---

*最后更新：2026 年 3 月 6 日*
