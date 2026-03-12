# 软件更新功能使用说明

## 📦 功能概述

小海 AI 助手已集成完整的 Gitee 自动更新功能，包括：

- ✅ **自动检查更新** - 软件启动时自动检测新版本
- ✅ **手动检查更新** - 点击按钮随时检查
- ✅ **版本信息展示** - 显示更新说明和发布日期
- ✅ **一键下载更新** - 内置下载器，无需浏览器
- ✅ **进度实时显示** - 下载进度条实时更新
- ✅ **自动安装** - 下载完成后自动启动安装程序

---

## 🔧 配置说明

### 1. 修改 Gitee 仓库信息

打开 `Models/Settings.cs` 文件，修改以下配置：

```csharp
// Gitee 更新配置
public string GiteeOwner { get; set; } = "your-username";  // 替换为你的 Gitee 用户名
public string GiteeRepo { get; set; } = "XIAOHAI.AI";      // 仓库名称
public string AppVersion { get; set; } = "1.0.0";          // 当前版本号
public bool AutoCheckUpdate { get; set; } = true;          // 是否自动检查更新
```

**配置说明：**

| 配置项 | 说明 | 示例 |
|--------|------|------|
| `GiteeOwner` | Gitee 用户名或组织名 | `xiaohai-ai` |
| `GiteeRepo` | 仓库名称 | `XIAOHAI.AI` |
| `AppVersion` | 当前应用版本号 | `1.0.0` |
| `AutoCheckUpdate` | 是否启用自动检查 | `true` / `false` |

### 2. 在 Gitee 创建 Release

在 Gitee 仓库中创建新的 Release：

1. 访问你的 Gitee 仓库
2. 点击「发行版」→「创建发行版」
3. 填写标签名称（如 `v1.0.1`）
4. 填写发行版说明（更新日志）
5. 上传编译好的 `.exe` 文件
6. 点击「确定发布」

**Release 标签格式：**
- 支持 `v1.0.0` 或 `1.0.0` 格式
- 版本号格式：`主版本号。次版本号。修订号`

---

## 🎯 功能使用

### 自动检查更新

软件每次启动时会自动检查 Gitee 上的最新版本：

1. **启动软件** → 自动检查更新
2. **有新版本** → 自动弹出更新窗口
3. **无新版本** → 静默跳过，不打扰用户
4. **检查失败** → 静默失败，不影响软件使用

### 手动检查更新

点击侧边栏的「🔄 检查更新」按钮：

1. 点击按钮
2. 弹出更新窗口
3. 显示当前版本和最新版本信息
4. 如有新版本，可点击「⬇️ 立即更新」

---

## 🖼️ 更新窗口界面

```
┌─────────────────────────────────────────┐
│  📦 发现新版本                           │
│  发现新版本，是否立即更新？              │
├─────────────────────────────────────────┤
│                                         │
│  当前版本：v1.0.0    最新版本：v1.0.1   │
│  发布日期：2024 年 01 月 15 日                        │
│                                         │
│  📝 更新说明                             │
│  ┌─────────────────────────────────┐   │
│  │ • 修复了已知问题                 │   │
│  │ • 新增了自动化功能              │   │
│  │ • 优化了性能                     │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ⬇️ 下载进度                             │
│  [████████████░░░░] 60%                 │
│  正在下载...                            │
│                                         │
├─────────────────────────────────────────┤
│  [🌐 前往下载页]     [取消] [⬇️ 立即更新] │
└─────────────────────────────────────────┘
```

---

## 📝 更新流程

### 完整更新步骤

```
1. 用户点击「⬇️ 立即更新」
   ↓
2. 显示下载进度条
   ↓
3. 下载安装包到临时目录
   ↓
4. 下载完成提示
   ↓
5. 自动启动安装程序
   ↓
6. 关闭当前应用
   ↓
7. 用户运行新版本安装
```

### 下载进度显示

- **0%** - 准备下载
- **10%-90%** - 实时下载进度
- **100%** - 下载完成
- **完成** - 启动安装程序

---

## 🔐 安全说明

### 下载安全

- ✅ 只从 Gitee 官方下载
- ✅ 验证文件完整性
- ✅ 使用 HTTPS 加密传输
- ✅ 临时目录存储安装包

### 安装提示

- ⚠️ 安装程序会关闭当前应用
- ⚠️ 请保存好当前工作
- ⚠️ 建议以管理员身份运行安装程序

---

## 🛠️ 技术细节

### UpdateService 服务

```csharp
// 创建更新服务
var updateService = new UpdateService(
    owner: "your-username",
    repo: "XIAOHAI.AI",
    currentVersion: "1.0.0"
);

// 检查更新
var result = await updateService.CheckForUpdatesAsync();

if (result.HasUpdate)
{
    // 有新版本
    Console.WriteLine($"发现新版本：{result.LatestVersion}");
    Console.WriteLine($"更新说明：{result.ReleaseNotes}");
    Console.WriteLine($"下载地址：{result.DownloadUrl}");
}
```

### 更新检查结果

```csharp
public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }           // 是否有新版本
    public string CurrentVersion { get; set; }    // 当前版本
    public string LatestVersion { get; set; }     // 最新版本
    public string ReleaseNotes { get; set; }      // 更新说明
    public string? DownloadUrl { get; set; }      // 下载地址
    public string? ReleaseDate { get; set; }      // 发布日期
    public string? Error { get; set; }            // 错误信息
}
```

---

## ❓ 常见问题

### Q: 自动检查更新失败怎么办？
**A:** 
- 检查网络连接
- 确认 Gitee 仓库信息正确
- 检查 Gitee API 是否可访问
- 手动点击「检查更新」按钮

### Q: 下载速度慢怎么办？
**A:** 
- Gitee 下载速度通常较快
- 如确实慢，可点击「前往下载页」手动下载
- 检查网络带宽

### Q: 更新失败会损坏软件吗？
**A:** 
- 不会，安装包下载到临时目录
- 原版本不受影响
- 可重新下载或手动更新

### Q: 如何关闭自动检查更新？
**A:** 
- 修改 `Settings.cs` 中的 `AutoCheckUpdate = false`
- 或在设置界面添加开关（需自行实现）

---

## 📋 文件清单

更新功能涉及的文件：

```
XIAOHAI.AI/
├── Models/
│   └── Settings.cs                    # 配置 Gitee 仓库信息
├── Services/
│   └── UpdateService.cs               # 更新服务核心逻辑
├── UpdateWindow.xaml                  # 更新窗口界面
├── UpdateWindow.xaml.cs               # 更新窗口逻辑
└── MainWindow.xaml.cs                 # 集成自动检查和手动检查
```

---

## 🚀 发布新版本流程

### 1. 更新版本号

修改 `Models/Settings.cs`：
```csharp
public string AppVersion { get; set; } = "1.0.1";  // 更新版本号
```

### 2. 编译新版本

```bash
dotnet build -c Release
```

### 3. 在 Gitee 创建 Release

1. 访问 Gitee 仓库
2. 创建新的发行版
3. 标签：`v1.0.1`
4. 上传：`XIAOHAI.AI.exe`
5. 填写更新说明

### 4. 通知用户更新

用户启动软件时会自动检测到新版本并提示更新！

---

## 💡 最佳实践

### 版本号规范

推荐使用 [语义化版本](https://semver.org/lang/zh-CN/)：

- **主版本号**：不兼容的 API 修改
- **次版本号**：向下兼容的功能性新增
- **修订号**：向下兼容的问题修正

示例：`1.0.0` → `1.0.1` → `1.1.0` → `2.0.0`

### 更新说明编写

好的更新说明应该：

- ✅ 简洁明了
- ✅ 列出主要变更
- ✅ 说明修复的问题
- ✅ 提及新功能
- ✅ 如有破坏性变更，明确标注

示例：
```markdown
## v1.0.1

### 新增
- 桌面自动化功能
- 技能库管理

### 修复
- 修复了 OCR 识别失败的问题
- 修复了知识库同步卡顿

### 优化
- 优化了启动速度
- 减少了内存占用
```

---

## 📞 技术支持

如有问题，请：

1. 检查 Gitee 仓库配置
2. 查看网络连通性
3. 检查 Gitee API 状态
4. 查看应用日志

---

**🎉 更新功能已完成！祝使用愉快！**
