# 自动化脚本编写指南

## 重要说明

由于使用 **IronPython** 执行 Python 脚本，因此**不能使用 Python 标准库**（如 `os`、`sys` 等），而应该使用 **.NET 类库**。

## 基本语法

### 1. 引入 .NET 命名空间

```python
import clr
clr.AddReference('System')
clr.AddReference('System.Windows.Forms')
clr.AddReference('System.Drawing')

from System import String, Array
from System.Diagnostics import Process
from System.IO import File, Directory, Path
from System.Threading import Thread
from System.Windows.Forms import SendKeys, Clipboard
from System.Drawing import Point, Size
```

**注意**：
- `System.dll` 包含了 `System.Diagnostics`, `System.IO`, `System.Threading` 等命名空间
- 不需要单独引用 `System.Diagnostics` 等程序集
- 使用 `print()` 函数进行输出，而不是 `Console.WriteLine`

### 2. 常用操作示例

#### 启动应用程序
```python
Process.Start("notepad.exe")
Process.Start("calc.exe")
Process.Start("chrome.exe", "https://www.baidu.com")
```

#### 文件操作
```python
# 读取文件
content = File.ReadAllText("C:\\test.txt")

# 写入文件
File.WriteAllText("C:\\test.txt", "Hello World")

# 检查文件是否存在
if File.Exists("C:\\test.txt"):
    Console.WriteLine("文件存在")

# 删除文件
File.Delete("C:\\test.txt")

# 创建目录
Directory.CreateDirectory("C:\\MyFolder")
```

#### 键盘输入
```python
# 发送文本
SendKeys.SendWait("Hello World")

# 发送特殊键
SendKeys.SendWait("{ENTER}")  # 回车
SendKeys.SendWait("{TAB}")    # Tab 键
SendKeys.SendWait("^c")       # Ctrl+C (复制)
SendKeys.SendWait("^v")       # Ctrl+V (粘贴)
```

#### 等待
```python
# 等待 1000 毫秒（1 秒）
Thread.Sleep(1000)
```

#### 剪贴板操作
```python
# 设置剪贴板内容
Clipboard.SetText("要复制的文本")

# 获取剪贴板内容
text = Clipboard.GetText()
print("剪贴板内容：" + text)
```

#### 输出信息
```python
print("输出信息")
print("变量值：" + str(variable))
```

### 3. 完整示例

#### 示例 1：打开记事本并输入文本
```python
import clr
clr.AddReference('System')
from System.Diagnostics import Process
from System.Windows.Forms import SendKeys
from System.Threading import Thread

# 打开记事本
Process.Start("notepad.exe")

# 等待记事本启动
Thread.Sleep(1000)

# 输入文本
SendKeys.SendWait("你好，这是自动化脚本！")
```

#### 示例 2：文件备份
```python
import clr
clr.AddReference('System')
from System.IO import File, Directory

sourceFile = "C:\\source.txt"
destFile = "C:\\backup.txt"

# 检查源文件是否存在
if File.Exists(sourceFile):
    # 复制文件
    File.Copy(sourceFile, destFile, True)
    print("备份完成！")
else:
    print("源文件不存在")
```

#### 示例 3：批量操作
```python
import clr
clr.AddReference('System')
from System.Diagnostics import Process
from System.Threading import Thread
from System.Windows.Forms import SendKeys

# 打开多个应用程序
Process.Start("notepad.exe")
Thread.Sleep(500)

Process.Start("calc.exe")
Thread.Sleep(500)

Process.Start("mspaint.exe")
Thread.Sleep(500)

print("已打开 3 个应用程序")
```

## 常见错误

### ❌ 错误写法（使用 Python 标准库）
```python
import os
os.startfile("notepad.exe")  # 错误！IronPython 没有 os 模块

import time
time.sleep(1)  # 错误！IronPython 没有 time 模块
```

### ✅ 正确写法（使用 .NET 类库）
```python
import clr
clr.AddReference('System')
from System.Diagnostics import Process
from System.Threading import Thread

Process.Start("notepad.exe")  # 正确
Thread.Sleep(1000)  # 正确
```

## 可用的 .NET 程序集

| 程序集名称 | 用途 | 命名空间示例 |
|-----------|------|-------------|
| System.dll | 基础类库（包含 Diagnostics, IO, Threading 等） | Process, File, Thread |
| System.Windows.Forms.dll | Windows 窗体和控件 | SendKeys, Clipboard, MessageBox |
| System.Drawing.dll | 图形和图像处理 | Point, Size, Color, Bitmap |
| System.Data.dll | 数据库操作 | DataTable, DataSet, SqlConnection |

**重要提示**：
- `System.dll` 已经包含了 `System.Diagnostics`, `System.IO`, `System.Threading` 等命名空间
- 不需要单独引用 `System.Diagnostics` 等程序集
- 只需引用主要的程序集文件（.dll）

## 调试技巧

### 1. 输出调试信息
```python
print("当前步骤：打开程序")
print("变量值：" + str(myVariable))
```

### 2. 异常处理
```python
try:
    Process.Start("notepad.exe")
except Exception as e:
    print("发生错误：" + str(e))
```

### 3. 检查文件路径
```python
path = "C:\\test.txt"
print("检查路径：" + path)
if File.Exists(path):
    print("文件存在")
else:
    print("文件不存在")
```

## 最佳实践

1. **总是先导入 clr 模块**
   ```python
   import clr
   ```

2. **明确指定要使用的命名空间**
   ```python
   clr.AddReference('System')
   from System.Diagnostics import Process
   ```

3. **使用 Thread.Sleep 而不是 time.sleep**
   ```python
   from System.Threading import Thread
   Thread.Sleep(1000)
   ```

4. **使用 .NET 的文件操作**
   ```python
   from System.IO import File
   File.ReadAllText("path")
   ```

5. **添加适当的错误处理**
   ```python
   try:
       # 你的代码
   except Exception as e:
       Console.WriteLine("错误：" + str(e))
   ```

## 参考资源

- [IronPython 官方文档](https://ironpython.net/)
- [.NET Framework API 参考](https://learn.microsoft.com/zh-cn/dotnet/api/)
- [System.Diagnostics.Process 类](https://learn.microsoft.com/zh-cn/dotnet/api/system.diagnostics.process)
- [System.IO.File 类](https://learn.microsoft.com/zh-cn/dotnet/api/system.io.file)
