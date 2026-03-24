# 架构设计指南

## 目录
- [架构概述](#架构概述)
- [设计原则](#设计原则)
- [分层架构](#分层架构)
- [依赖注入](#依赖注入)
- [数据流](#数据流)
- [扩展指南](#扩展指南)

## 架构概述

PutPicture 采用分层架构设计，遵循SOLID原则，通过依赖注入实现松耦合，支持高度的可扩展性和可测试性。

### 🏗️ 整体架构图

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │ BatchImageProcessor │  │    Program.cs   │  │  Console UI  │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                     Service Layer                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │ImageProcessorSvc│  │TransparencyMaker│  │ColorMatcher  │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
│  ┌─────────────────┐                                       │
│  │  LoggerService  │                                       │
│  └─────────────────┘                                       │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                      Core Layer                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Interfaces    │  │     Models      │  │ServiceContainer│ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                     Utils Layer                            │
│  ┌─────────────────┐  ┌─────────────────┐                  │
│  │ColorSpaceConverter│  │  Other Utils   │                  │
│  └─────────────────┘  └─────────────────┘                  │
└─────────────────────────────────────────────────────────────┘
```

## 设计原则

### SOLID 原则

#### 1. 单一职责原则 (SRP)
每个类都有明确的单一职责：
- `LoggerService`: 只负责日志记录
- `ColorMatcherService`: 只负责颜色匹配
- `TransparencyMakerService`: 只负责透明化处理

#### 2. 开闭原则 (OCP)
通过接口抽象，对扩展开放，对修改关闭：
```csharp
// 可以轻松添加新的颜色匹配算法
public class AdvancedColorMatcher : IColorMatcher
{
    // 新的实现
}
```

#### 3. 里氏替换原则 (LSP)
所有接口实现都可以互相替换：
```csharp
IColorMatcher matcher = new ColorMatcherService(); // 或任何其他实现
```

#### 4. 接口隔离原则 (ISP)
接口设计精简，避免强迫实现不需要的方法：
```csharp
public interface ILogger // 只包含日志相关方法
public interface IColorMatcher // 只包含颜色匹配方法
```

#### 5. 依赖倒置原则 (DIP)
高层模块不依赖低层模块，都依赖抽象：
```csharp
public class ImageProcessorService : IImageProcessor
{
    private readonly ITransparencyMaker _transparencyMaker; // 依赖抽象
    private readonly ILogger _logger; // 依赖抽象
}
```

### 其他设计原则

#### DRY (Don't Repeat Yourself)
- 公共功能提取到工具类
- 通过继承和组合避免代码重复

#### KISS (Keep It Simple, Stupid)
- 接口设计简洁明了
- 避免过度设计

#### YAGNI (You Aren't Gonna Need It)
- 只实现当前需要的功能
- 保持架构的灵活性以便未来扩展

## 分层架构

### 1. 表示层 (Presentation Layer)

负责用户交互和程序入口。

```csharp
// 主入口类，提供向后兼容的API
public class BatchImageProcessor
{
    // 静态方法，易于使用
    public static void ProcessAllImages(...)
    public static ProcessingResult ProcessBatch(...)
}
```

**职责**:
- 提供用户友好的API
- 参数验证和转换
- 结果展示

### 2. 服务层 (Service Layer)

包含业务逻辑和核心功能实现。

```csharp
// 图像处理协调器
public class ImageProcessorService : IImageProcessor
{
    // 协调各个服务完成图像处理
}

// 透明化处理核心
public class TransparencyMakerService : ITransparencyMaker
{
    // 实现透明化算法
}
```

**职责**:
- 实现核心业务逻辑
- 协调各个组件
- 处理复杂的业务流程

### 3. 核心层 (Core Layer)

定义接口、模型和基础设施。

```csharp
// 接口定义
public interface IImageProcessor { }
public interface ITransparencyMaker { }

// 数据模型
public class ProcessingOptions { }
public class ProcessingResult { }

// 依赖注入容器
public class ServiceContainer { }
```

**职责**:
- 定义系统契约(接口)
- 提供数据传输对象
- 管理依赖关系

### 4. 工具层 (Utils Layer)

提供通用工具和辅助功能。

```csharp
// 色彩空间转换
public static class ColorSpaceConverter
{
    public static (double H, double S, double V) RgbToHsv(...)
    public static (double L, double A, double B) RgbToLab(...)
}
```

**职责**:
- 提供通用算法
- 数据转换功能
- 独立的工具方法

## 依赖注入

### 服务容器设计

```csharp
public class ServiceContainer
{
    private readonly Dictionary<Type, object> _services;
    private readonly Dictionary<Type, Func<ServiceContainer, object>> _factories;
    
    // 注册单例
    public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance)
    
    // 注册工厂
    public void RegisterFactory<TInterface>(Func<ServiceContainer, TInterface> factory)
    
    // 获取服务
    public T GetService<T>()
}
```

### 服务注册配置

```csharp
public static class ServiceRegistration
{
    public static ServiceContainer ConfigureServices(string logFilePath = null)
    {
        var container = new ServiceContainer();
        
        // 注册服务的依赖关系
        container.RegisterSingleton<ILogger, LoggerService>(new LoggerService(logFilePath));
        container.RegisterFactory<IColorMatcher>(c => new ColorMatcherService());
        container.RegisterFactory<ITransparencyMaker>(c => 
            new TransparencyMakerService(
                c.GetService<IColorMatcher>(), 
                c.GetService<ILogger>()));
        
        return container;
    }
}
```

### 依赖关系图

```
IImageProcessor
    ├── ITransparencyMaker
    │   ├── IColorMatcher
    │   └── ILogger
    └── ILogger
```

## 数据流

### 批量处理数据流

```
BatchProcessingRequest
    ↓
ImageProcessorService.ProcessBatch()
    ↓
扫描文件 → 并行处理 → 统计结果
    ↓           ↓
文件列表    ProcessSingleFile()
               ↓
        TransparencyMakerService.MakeColorTransparent()
               ↓
        ColorMatcherService.IsMatch()
               ↓
        返回处理结果
    ↓
ProcessingResult
```

### 单张图片处理数据流

```
Bitmap + ProcessingOptions
    ↓
ImageProcessorService.ProcessImage()
    ↓
TransparencyMakerService.MakeColorTransparent()
    ↓
┌─────────────────┬─────────────────┐
│  自适应容差计算   │   颜色匹配处理    │
└─────────────────┴─────────────────┘
    ↓
后处理 (形态学、边缘平滑)
    ↓
返回处理后的 Bitmap
```

## 扩展指南

### 添加新的颜色匹配算法

1. **实现接口**:
```csharp
public class AIColorMatcher : IColorMatcher
{
    public bool IsMatch(byte r, byte g, byte b, Color targetColor, int tolerance)
    {
        // 使用AI算法进行颜色匹配
        return false;
    }
    
    public double CalculateMatchScore(byte r, byte g, byte b, Color targetColor, int tolerance)
    {
        // 计算AI匹配分数
        return 0.0;
    }
}
```

2. **注册服务**:
```csharp
container.RegisterFactory<IColorMatcher>(c => new AIColorMatcher());
```

### 添加新的后处理效果

1. **定义接口**:
```csharp
public interface IPostProcessor
{
    Bitmap Process(Bitmap source, PostProcessingOptions options);
}
```

2. **实现服务**:
```csharp
public class BlurPostProcessor : IPostProcessor
{
    public Bitmap Process(Bitmap source, PostProcessingOptions options)
    {
        // 实现模糊效果
        return new Bitmap(source);
    }
}
```

3. **集成到处理流程**:
```csharp
public class ImageProcessorService : IImageProcessor
{
    private readonly IPostProcessor _postProcessor;
    
    public Bitmap ProcessImage(Bitmap source, ProcessingOptions options)
    {
        var result = _transparencyMaker.MakeColorTransparent(source, transparencyOptions);
        
        if (options.EnablePostProcessing)
        {
            var postProcessed = _postProcessor.Process(result, options.PostProcessingOptions);
            result.Dispose();
            result = postProcessed;
        }
        
        return result;
    }
}
```

### 添加新的日志输出方式

1. **扩展日志服务**:
```csharp
public class DatabaseLoggerService : ILogger
{
    public void Log(LogLevel level, string message, Exception exception = null)
    {
        // 写入数据库
    }
}
```

2. **组合多个日志输出**:
```csharp
public class CompositeLogger : ILogger
{
    private readonly ILogger[] _loggers;
    
    public CompositeLogger(params ILogger[] loggers)
    {
        _loggers = loggers;
    }
    
    public void Log(LogLevel level, string message, Exception exception = null)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(level, message, exception);
        }
    }
}
```

### 添加新的图片格式支持

1. **扩展文件扫描**:
```csharp
public class ExtendedImageProcessor : ImageProcessorService
{
    protected override string[] GetSupportedExtensions()
    {
        return base.GetSupportedExtensions()
            .Concat(new[] { "*.webp", "*.avif" })
            .ToArray();
    }
}
```

2. **自定义图片加载**:
```csharp
public interface IImageLoader
{
    Bitmap LoadImage(string filePath);
    bool CanLoad(string extension);
}
```

## 测试策略

### 单元测试

```csharp
[Test]
public void ColorMatcher_ShouldMatchExactColor()
{
    // Arrange
    var matcher = new ColorMatcherService();
    var targetColor = Color.FromArgb(255, 255, 255);
    
    // Act
    var result = matcher.IsMatch(255, 255, 255, targetColor, 0);
    
    // Assert
    Assert.IsTrue(result);
}
```

### 集成测试

```csharp
[Test]
public void ImageProcessor_ShouldProcessImageCorrectly()
{
    // Arrange
    var container = ServiceRegistration.ConfigureServices();
    var processor = container.GetService<IImageProcessor>();
    
    // Act & Assert
    using (var source = new Bitmap(100, 100))
    using (var result = processor.ProcessImage(source, new ProcessingOptions()))
    {
        Assert.IsNotNull(result);
    }
}
```

### 性能测试

```csharp
[Test]
public void BatchProcessor_ShouldMeetPerformanceRequirements()
{
    var stopwatch = Stopwatch.StartNew();
    
    // 执行批量处理
    var result = BatchImageProcessor.ProcessBatch(request);
    
    stopwatch.Stop();
    
    // 验证性能指标
    Assert.Less(stopwatch.ElapsedMilliseconds, 10000); // 10秒内完成
    Assert.Greater(result.Statistics.AverageSpeed, 1.0); // 至少1张/秒
}
```

## 最佳实践

### 1. 资源管理
```csharp
// 使用 using 语句确保资源释放
using (var source = new Bitmap("input.jpg"))
using (var result = processor.ProcessImage(source, options))
{
    result.Save("output.png", ImageFormat.Png);
}
```

### 2. 异常处理
```csharp
public ProcessingResult ProcessBatch(BatchProcessingRequest request)
{
    var result = new ProcessingResult();
    
    try
    {
        // 处理逻辑
    }
    catch (Exception ex)
    {
        result.Success = false;
        result.Errors.Add($"处理失败: {ex.Message}");
        _logger.Error("批量处理失败", ex);
    }
    
    return result;
}
```

### 3. 配置验证
```csharp
public void ProcessAllImages(string sourceDirectory, string targetDirectory, ...)
{
    if (string.IsNullOrEmpty(sourceDirectory))
        throw new ArgumentException("源目录不能为空", nameof(sourceDirectory));
        
    if (!Directory.Exists(sourceDirectory))
        throw new DirectoryNotFoundException($"源目录不存在: {sourceDirectory}");
}
```

### 4. 日志记录
```csharp
public Bitmap ProcessImage(Bitmap source, ProcessingOptions options)
{
    _logger.Debug($"开始处理图片: {source.Width}x{source.Height}");
    
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = DoProcessing(source, options);
        
        stopwatch.Stop();
        _logger.Info($"图片处理完成，耗时: {stopwatch.ElapsedMilliseconds}ms");
        
        return result;
    }
    catch (Exception ex)
    {
        _logger.Error("图片处理失败", ex);
        throw;
    }
}
```

这个架构设计提供了高度的灵活性和可扩展性，同时保持了代码的清晰性和可维护性。