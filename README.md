# XIAOHAI.AI - 小海智能对话助手

<div align="center">

🤖 一款功能强大的本地 AI 智能对话助手

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D7?style=flat-square&logo=windows)](https://docs.microsoft.com/dotnet/desktop/wpf/)
[![Ollama](https://img.shields.io/badge/Ollama-AI-FF6F00?style=flat-square&logo=ollama)](https://ollama.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

</div>

---

## 📖 目录

- [简介](#简介)
- [核心功能](#核心功能)
- [技术栈](#技术栈)
- [快速开始](#快速开始)
- [功能详解](#功能详解)
- [常见问题](#常见问题)
- [项目结构](#项目结构)
- [贡献指南](#贡献指南)
- [许可证](#许可证)

---

## 💡 简介

**小海 AI** 是一款基于 **Ollama 大模型**的本地智能对话助手，完全运行在您的 Windows 电脑上，无需联网即可享受 AI 智能服务。

### ✨ 主要特点

- 🔒 **隐私安全** - 所有数据本地存储，不上传云端
- 🚀 **快速响应** - 本地部署，无需等待网络延迟
- 🧠 **智能对话** - 支持多轮对话、上下文理解
- 📚 **知识库** - 向量数据库支持，语义检索
- 🤖 **自动化** - Python 脚本自动化，智能执行任务
- 🖼️ **视觉识别** - 图片 OCR、智能填表
- 🌐 **联网搜索** - 可选配百度千帆搜索 API

---

## 🎯 核心功能

### 💬 智能对话
- ✅ 多轮对话与上下文理解
- ✅ 打字机效果实时显示
- ✅ 思考过程可视化（支持 DeepSeek 等推理模型）
- ✅ 聊天记录保存与导出（Markdown/TXT/JSON）

### 🧠 知识库管理
- ✅ 支持导入多种文档格式（TXT、DOCX、PDF、Excel）
- ✅ 向量语义检索（RAG 技术）
- ✅ AI 智能压缩知识内容
- ✅ 知识条目锁定保护
- ✅ SQLite 本地存储

### 🤖 桌面自动化
- ✅ 自然语言描述任务
- ✅ AI 智能分析并生成 Python 脚本
- ✅ 技能库管理（执行、编辑、删除、重命名）
- ✅ 安全检查与用户确认机制
- ✅ IronPython 执行引擎

### 🔍 视觉识别
- ✅ 图片 OCR 文字提取
- ✅ 证件识别（身份证、银行卡等）
- ✅ 智能表单填充
- ✅ 截图分析

### 🌐 联网搜索
- ✅ 集成百度千帆搜索 API
- ✅ 实时获取网络信息
- ✅ AI 智能判断是否需要搜索

### 🖥️ 系统集成
- ✅ 悬浮球快捷操作
- ✅ 系统托盘运行
- ✅ 密码保护设置

---

## 🛠️ 技术栈

| 类别 | 技术/库 | 说明 |
|------|---------|------|
| **框架** | .NET 10.0 + WPF | Windows 桌面应用框架 |
| **AI 核心** | Ollama | 本地大模型服务 |
| **向量数据库** | SQLite + Microsoft.Data.Sqlite | 知识存储与检索 |
| **Python 引擎** | IronPython | .NET 环境下的 Python 执行 |
| **文件处理** | NPOI、PiggyPDF | Word/Excel/PDF 解析 |
| **网络** | Selenium、WebView2 | 浏览器自动化 |
| **其他** | Newtonsoft.Json、WpfAnimatedGif | JSON 处理、GIF 显示 |

---

## 🚀 快速开始

### 1️⃣ 安装 Ollama

```bash
# 访问官网下载安装
https://ollama.com
```

### 2️⃣ 拉取模型

```bash
# 对话模型（推荐）
ollama pull glm-4.7-flash

# 视觉模型（可选）
ollama pull llava
```

### 3️⃣ 运行程序

```bash
# 直接运行编译后的程序
XIAOHAI.AI.exe
```

### 4️⃣ 配置使用

1. 首次启动会自动检测 Ollama 连接
2. 点击设置按钮（密码：`admin888`）
3. 确认模型配置正确
4. 开始对话！

---

## 📚 功能详解

### 智能对话

**主界面操作：**
- 在输入框输入问题，按 `Enter` 发送
- `Shift + Enter` 换行输入
- 支持粘贴图片进行视觉识别
- 右侧边栏显示当前模型和状态

**对话技巧：**
```
✅ 清晰描述问题
"如何用 Python 读取 Excel 文件？"

✅ 提供上下文
"我想做数据分析，有 10 万行数据，用什么库好？"

✅ 多轮对话
"什么是机器学习？" → "有哪些应用场景？" → "如何入门？"
```

### 知识库管理

**使用步骤：**
1. 点击侧边栏 `🧠 知识库` 按钮
2. 添加知识：
   - **手动添加**：输入标题和内容
   - **导入文件**：支持 TXT、DOCX、PDF、XLSX
3. 点击 `💾 保存并同步` 生成向量索引
4. 管理操作：压缩、锁定、编辑、删除

**示例场景：**
- 📖 个人学习笔记
- 📄 公司文档资料
- 📊 专业技术手册
- 📝 常见问题解答

### 桌面自动化

**操作流程：**
1. 点击侧边栏 `🤖 自动化` 按钮
2. 输入任务描述（如"帮我打开记事本"）
3. AI 分析并生成 Python 脚本
4. 安全检查（有风险时会提示确认）
5. 执行脚本
6. 自动保存到技能库

**技能管理：**
- **▶ 执行** - 直接运行技能脚本
- **✏ 编辑** - 查看和修改代码
- **📝 重命名** - 修改技能名称
- **🗑 删除** - 移除技能

**Python 脚本示例（IronPython）：**
```python
import clr
clr.AddReference('System')
from System.Diagnostics import Process
from System.Threading import Thread

# 打开记事本
Process.Start("notepad.exe")
Thread.Sleep(1000)
print("记事本已打开")
```

### 视觉识别

**使用步骤：**
1. 点击侧边栏 `🔍 视觉识别` 按钮
2. 选择图片文件
3. 点击 `🔍 识别图片`
4. 查看识别结果
5. （可选）输入 URL 自动填表

**支持场景：**
- 🆔 身份证信息提取
- 💳 银行卡识别
- 📋 表单自动填充
- 📄 文档 OCR

### 联网搜索

**配置方法：**
1. 点击 `⚙ 设置`（密码：admin888）
2. 切换到 `搜索引擎` 标签
3. 填写百度千帆 API 密钥和用户 ID
4. 获取密钥：https://console.bce.baidu.com/

**使用场景：**
- 📰 时事新闻查询
- 🌤️ 天气信息
- 📈 股票价格
- 🔬 最新研究进展

---

## ❓ 常见问题

### Q: 为什么 AI 回答很慢？
**A:** 回答速度取决于硬件配置和模型大小。建议：
- 使用 4bit 量化模型（如 glm-4.7-flash）
- 确保至少 8GB RAM
- 使用 SSD 硬盘

### Q: 如何更换 AI 模型？
**A:** 
1. 点击 `⚙ 设置`（密码：admin888）
2. 在 `本地模型` 标签页点击 `🔄 刷新可用模型`
3. 从下拉列表选择模型
4. 保存设置

### Q: 知识库有什么作用？
**A:** 知识库让 AI 记住您的专业资料、文档、笔记等。回答问题时会自动检索相关知识，提供更准确的答案。

### Q: 如何备份数据？
**A:** 备份以下文件：
- `knowledge_vectors.db` - 知识库数据库
- `settings.json` - 设置文件
- `chat_history_main.json` - 聊天记录

### Q: 自动化功能如何使用？
**A:** 
1. 点击 `🤖 自动化` 按钮
2. 描述任务（如"帮我打开记事本"）
3. AI 会生成并执行 Python 脚本
4. 脚本自动保存到技能库

### Q: 联网搜索失败怎么办？
**A:** 
- 检查是否配置了百度千帆 API 密钥
- 确认 API 额度是否充足
- 检查网络连接
- 或关闭联网搜索功能

---

## 📁 项目结构

```
XIAOHAI.AI/
├── Models/
│   └── Settings.cs              # 配置模型
├── Services/
│   ├── OllamaService.cs         # Ollama API 封装
│   ├── SearchService.cs          # 百度搜索服务
│   ├── ImageService.cs           # 视觉识别服务
│   ├── KnowledgeBaseService.cs  # 知识库服务
│   ├── VectorDatabaseService.cs  # 向量数据库
│   ├── FileExtractor.cs         # 文件解析
│   ├── AutomationService.cs     # 自动化服务 ⭐
│   └── PythonScriptManager.cs   # Python 脚本管理 ⭐
├── MainWindow.xaml.cs           # 主窗口
├── KnowledgeBaseWindow.xaml.cs  # 知识库管理
├── AutomationWindow.xaml.cs     # 自动化窗口 ⭐
├── TutorialWindow.xaml.cs       # 使用教程
├── SettingsDialog.xaml.cs       # 设置对话框
├── OCRWindow.xaml.cs            # OCR 窗口
└── automation_scripts/          # 技能脚本库 ⭐
    └── README.md                # 脚本编写指南
```

---

## 🤝 贡献指南

欢迎贡献代码、报告问题或提出建议！

1. Fork 本项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

---

## 📞 联系方式

如有问题或建议，请通过以下方式联系：

- 📧 Email: [your-email@example.com]
- 💬 Issues: [GitHub Issues](https://github.com/your-repo/issues)

---

## 🙏 致谢

感谢以下开源项目：

- [Ollama](https://ollama.com/) - 本地大模型服务
- [IronPython](https://ironpython.net/) - .NET Python 引擎
- [.NET](https://dotnet.microsoft.com/) - 开发框架

---

<div align="center">

**⭐ 如果这个项目对您有帮助，请给一个 Star！⭐**

Made with ❤️ by XIAOHAI.AI Team

</div>
