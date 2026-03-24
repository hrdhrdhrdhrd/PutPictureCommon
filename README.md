# PutPicture - 图片透明化处理工具

## 项目概述

PutPicture 是一个高性能的批量图片透明化处理工具，支持多种图片格式，采用先进的颜色匹配算法和边缘处理技术。

## 📚 文档导航

### 🚀 快速开始
- **[用户使用指南](docs/User-Guide.md)** - 从入门到精通的完整使用教程
- **[API 文档](docs/API-Documentation.md)** - 详细的接口和类库文档

### 🏗️ 开发文档
- **[架构设计指南](docs/Architecture-Guide.md)** - 系统架构和设计原则
- **[算法文档](docs/Algorithm-Documentation.md)** - 核心算法原理和实现
- **[性能优化指南](docs/Performance-Guide.md)** - 性能调优和最佳实践

## 架构设计

### 🏗️ 分层架构

```
├── docs/                    # 开发文档 100%kiro编写
│   ├── Algorithm-Documentation.md         # 算法文档 
│   ├── API-Documentation.md         # API文档 
│   ├── Architecture-Guide.md         # 架构设计指南 
│   ├── Performance-Guide.md         # 性能优化指南
│   ├── User-Guide.md         # 用户使用指南
├── Core/                    # 核心层 80%kiro编写
│   ├── Interfaces/         # 接口定义
│   ├── Models/            # 数据模型
│   ├── ServiceContainer.cs # 依赖注入容器
│   └── ServiceRegistration.cs # 服务注册配置
├── Services/               # 服务层 90%kiro编写
│   ├── ImageProcessorService.cs    # 图像处理服务
│   ├── TransparencyMakerService.cs # 透明化服务
│   ├── ColorMatcherService.cs      # 颜色匹配服务
│   └── LoggerService.cs            # 日志服务
├── Utils/                  # 工具层 90%kiro编写
│   └── ColorSpaceConverter.cs      # 色彩空间转换
├── BatchImageProcessor.cs  # 主入口类（向后兼容） 90%kiro编写
└── Program.cs             # 示例程序 80%
└── README.md             # 软件自述文档 100%kiro编写
```

### 🔧 核心接口

- **IImageProcessor**: 图像处理器接口
- **ITransparencyMaker**: 透明化处理接口
- **IColorMatcher**: 颜色匹配接口
- **ILogger**: 日志接口

### 📊 数据模型

- **ProcessingOptions**: 处理选项配置
- **BatchProcessingRequest**: 批量处理请求
- **ProcessingResult**: 处理结果和统计信息

## 功能特性

### ✨ 核心功能

- **多格式支持**: JPG, PNG, BMP, GIF, TIFF
- **高性能并行处理**: 充分利用多核CPU
- **智能颜色匹配**: 结合RGB、HSV、Lab色彩空间
- **自适应容差**: 根据图像复杂度自动调整
- **边缘平滑**: 抗锯齿和羽化处理
- **形态学处理**: 去噪和孔洞填充
- **渐变透明**: 边缘区域渐变透明效果

### 📈 性能优化

- **内存优化**: 使用unsafe指针直接操作内存
- **并行处理**: 支持自定义并行度
- **智能跳过**: 可跳过已存在的文件
- **详细统计**: 处理速度、压缩率等统计信息

## 使用方法

### 🚀 快速开始

```csharp
// 基本用法（向后兼容）
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: "input",
    targetDirectory: "output", 
    backgroundColor: Color.White,
    tolerance: 50
);
```

### 🔧 高级用法

```csharp
// 使用新接口
var request = new BatchProcessingRequest
{
    SourceDirectory = "input",
    TargetDirectory = "output",
    Options = new ProcessingOptions
    {
        BackgroundColor = Color.White,
        Tolerance = 60,
        EnableEdgeSmoothing = true,
        EnableAdaptiveTolerance = true,
        EnableGradientAlpha = true
    }
};

var result = BatchImageProcessor.ProcessBatch(request);
```

### 🎯 单张图片处理

```csharp
using (var source = new Bitmap("input.jpg"))
{
    var options = new ProcessingOptions
    {
        BackgroundColor = Color.White,
        Tolerance = 50
    };
    
    using (var processed = BatchImageProcessor.ProcessSingleImage(source, options))
    {
        processed.Save("output.png", ImageFormat.Png);
    }
}
```

### 🔍 服务容器用法

```csharp
// 获取服务容器
var container = BatchImageProcessor.GetServiceContainer();

// 直接使用服务
var logger = container.GetService<ILogger>();
var transparencyMaker = container.GetService<ITransparencyMaker>();

logger.Info("自定义日志消息");
```

## 配置选项

### ProcessingOptions 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| BackgroundColor | Color | White | 要透明化的背景色 |
| Tolerance | int | 50 | 颜色容差 (0-255) |
| EnableEdgeSmoothing | bool | true | 启用边缘平滑 |
| EnableAdaptiveTolerance | bool | true | 启用自适应容差 |
| EnableMorphology | bool | true | 启用形态学处理 |
| EnableGradientAlpha | bool | true | 启用渐变透明 |
| DespeckleSize | int | 3 | 去噪核大小 |

### BatchProcessingRequest 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| SourceDirectory | string | - | 源图片目录 |
| TargetDirectory | string | - | 输出目录 |
| MaxDegreeOfParallelism | int | -1 | 最大并行度 (-1为自动) |
| SkipExisting | bool | true | 跳过已存在文件 |
| SupportedExtensions | string[] | jpg,png,bmp,gif,tiff | 支持的文件格式 |

## 日志配置

```csharp
// 设置日志级别
BatchImageProcessor.SetLogLevel(LogLevel.Debug);

// 设置日志文件
BatchImageProcessor.SetLogFile("processing.log");
```

## 性能建议

1. **并行度设置**: 通常设置为 CPU核心数-1 获得最佳性能
2. **内存使用**: 大图片处理时注意内存使用情况
3. **磁盘I/O**: 使用SSD可显著提升处理速度
4. **容差调整**: 根据图片特点调整容差值

## 扩展开发

### 添加新的颜色匹配算法

```csharp
public class CustomColorMatcher : IColorMatcher
{
    public bool IsMatch(byte r, byte g, byte b, Color targetColor, int tolerance)
    {
        // 实现自定义匹配逻辑
        return false;
    }
    
    public double CalculateMatchScore(byte r, byte g, byte b, Color targetColor, int tolerance)
    {
        // 实现自定义评分逻辑
        return 0.0;
    }
}

// 注册自定义服务
var container = new ServiceContainer();
container.RegisterSingleton<IColorMatcher, CustomColorMatcher>(new CustomColorMatcher());
```

### 添加新的后处理效果

```csharp
public class CustomImageProcessor : IImageProcessor
{
    // 实现自定义处理逻辑
}
```

## 环境要求

- .NET Framework 4.7.2 或更高版本
- 支持的图片格式：JPG, PNG, BMP, GIF, TIFF
- 需要启用"允许不安全代码"选项

## 版本历史

- **v2.0**: 重构架构，引入依赖注入和分层设计
- **v1.0**: 初始版本，基本透明化功能

## 许可证

MIT License

## 贡献指南

欢迎提交 Issue 和 Pull Request！

---

**注意**: 本工具使用 unsafe 代码以获得最佳性能，需要在项目设置中启用 "允许不安全代码"。

Made with Kiro for Unity Developers
