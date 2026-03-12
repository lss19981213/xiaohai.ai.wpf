# 🎉 小海 AI v1.1.0 - 2026-03-12 (Day 2)

## ✨ 新增功能

### 🧩 插件系统
- ✅ 插件核心架构 (IPlugin 接口)
- ✅ 插件加载器 (PluginLoader)
- ✅ 插件管理器 UI
- ✅ 插件启用/禁用开关
- ✅ 示例插件：快速计算器 🔢

### 🎙️ 语音功能
- ✅ 语音合成 (Text-to-Speech)
- ✅ 语音识别 (Speech-to-Text)
- ✅ 支持中文识别
- ✅ 可调节语速和音量

### 🎨 多主题支持
- ✅ 深色主题 (默认)
- ✅ 浅色主题
- ✅ 蓝色主题
- ✅ 紫色主题
- ✅ 主题切换服务

### 📦 技术更新
- ✅ System.Speech 集成
- ✅ 插件热加载
- ✅ 自动检测 plugins 目录

---

## 🚀 插件开发指南

### 创建你的第一个插件

1. **创建类库项目**
```bash
dotnet new classlib -n MyPlugin -f net10.0-windows
```

2. **实现 IPlugin 接口**
```csharp
public class MyPlugin : IPlugin
{
    public string Name => "我的插件";
    public string Description => "插件描述";
    public string Version => "1.0.0";
    public string Author => "你的名字";
    public string Icon => "🔌";
    
    public Task InitializeAsync() => Task.CompletedTask;
    
    public async Task<PluginResponse?> ProcessMessageAsync(PluginContext context)
    {
        // 处理用户消息
        return null;
    }
    
    // 其他方法...
}
```

3. **编译并复制到 plugins 目录**
```bash
copy MyPlugin.dll C:\path\to\XIAOHAI.AI\plugins\
```

4. **重启小海 AI，插件自动加载！**

---

## 📦 安装说明

### 便携版（推荐）
1. 下载 `XIAOHAI.AI-v1.1.0-portable.zip`
2. 解压到任意目录
3. 运行 `XIAOHAI.AI.exe`

### 插件安装
1. 打开应用
2. 点击侧边栏 **🧩 插件管理**
3. 点击 **📂 插件目录**
4. 将插件 DLL 放入目录
5. 点击 **🔄 刷新**

---

## 🔧 技术栈

- **框架**: .NET 10.0 + WPF
- **语音**: System.Speech 8.0
- **插件**: 自定义插件架构
- **发布**: 单文件自包含

---

## 📝 已知问题

- ⚠️ 大文件 (>50MB) 无法上传到 GitHub（使用 Git LFS）
- ⚠️ 语音识别需要 Windows 语音服务
- ⚠️ 插件系统正在测试阶段

---

## 📅 下一步计划

### Day 3 (2026-03-13)
- [ ] 主题切换 UI 集成
- [ ] 语音输入按钮
- [ ] 更多示例插件
- [ ] 性能优化

### 本周目标
- [ ] 多模型市场
- [ ] 对话模板系统
- [ ] 任务调度器
- [ ] 插件市场

---

## 🔗 链接

- **GitHub**: https://github.com/lss19981213/xiaohai.ai.wpf
- **Issues**: https://github.com/lss19981213/xiaohai.ai.wpf/issues
- **路线图**: ROADMAP.md
- **插件开发文档**: 详见 Wiki

---

## 🙏 致谢

感谢以下开源项目：
- System.Speech (Microsoft)
- .NET WPF Community

---

**Made with ❤️ by 小海 AI 团队**

**版本**: v1.1.0  
**发布日期**: 2026-03-12  
**构建**: GitHub Actions  
**大小**: ~85MB (便携版)
