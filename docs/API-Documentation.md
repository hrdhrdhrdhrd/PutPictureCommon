# API 文档

## 目录
- [核心接口](#核心接口)
- [数据模型](#数据模型)
- [服务类](#服务类)
- [工具类](#工具类)
- [使用示例](#使用示例)

## 核心接口

### ILogger 接口

日志记录接口，提供统一的日志管理功能。

```csharp
public interface ILogger
{
    LogLevel Level { get; set; }
    void Log(LogLevel level, string message, Exception exception = null);
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception exception = null);
}
```

#### 方法说明

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `Log` | level, message, exception | void | 记录指定级别的日志 |
| `Debug` | message | void | 记录调试信息 |
| `Info` | message | void | 记录一般信息 |
| `Warning` | message | void | 记录警告信息 |
| `Error` | message, exception | void | 记录错误信息 |

#### 使用示例

```csharp
var logger = container.GetService<ILogger>();
logger.Info("处理开始");
logger.Error("处理失败", exception);
```

### IImageProcessor 接口

图像处理器接口，提供单张和批量图像处理功能。

```csharp
public interface IImageProcessor
{
    Bitmap ProcessImage(Bitmap source, ProcessingOptions options);
    ProcessingResult ProcessBatch(BatchProcessingRequest request);
}
```

#### 方法说明

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `ProcessImage` | source, options | Bitmap | 处理单张图片 |
| `ProcessBatch` | request | ProcessingResult | 批量处理图片 |

#### 使用示例

```csharp
var processor = container.GetService<IImageProcessor>();

// 单张处理
using var result = processor.ProcessImage(sourceBitmap, options);

// 批量处理
var batchResult = processor.ProcessBatch(request);
```

### ITransparencyMaker 接口

透明化处理接口，提供颜色透明化功能。

```csharp
public interface ITransparencyMaker
{
    Bitmap MakeColorTransparent(Bitmap source, TransparencyOptions options);
    int CalculateAdaptiveTolerance(Bitmap source, Color targetColor, int baseTolerance);
}
```

#### 方法说明

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `MakeColorTransparent` | source, options | Bitmap | 使指定颜色透明 |
| `CalculateAdaptiveTolerance` | source, targetColor, baseTolerance | int | 计算自适应容差 |

### IColorMatcher 接口

颜色匹配接口，提供颜色匹配算法。

```csharp
public interface IColorMatcher
{
    bool IsMatch(byte r, byte g, byte b, Color targetColor, int tolerance);
    double CalculateMatchScore(byte r, byte g, byte b, Color targetColor, int tolerance);
}
```

#### 方法说明

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `IsMatch` | r, g, b, targetColor, tolerance | bool | 判断颜色是否匹配 |
| `CalculateMatchScore` | r, g, b, targetColor, tolerance | double | 计算匹配分数(0-1) |

## 数据模型

### ProcessingOptions 类

图像处理选项配置类。

```csharp
public class ProcessingOptions
{
    public Color BackgroundColor { get; set; } = Color.White;
    public int Tolerance { get; set; } = 50;
    public bool EnableEdgeSmoothing { get; set; } = true;
    public bool EnableAdaptiveTolerance { get; set; } = true;
    public bool EnableMorphology { get; set; } = true;
    public bool EnableGradientAlpha { get; set; } = true;
    public int DespeckleSize { get; set; } = 3;
}
```

#### 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `BackgroundColor` | Color | White | 要透明化的背景色 |
| `Tolerance` | int | 50 | 颜色容差 (0-255) |
| `EnableEdgeSmoothing` | bool | true | 启用边缘平滑处理 |
| `EnableAdaptiveTolerance` | bool | true | 启用自适应容差计算 |
| `EnableMorphology` | bool | true | 启用形态学处理 |
| `EnableGradientAlpha` | bool | true | 启用渐变透明效果 |
| `DespeckleSize` | int | 3 | 去噪处理的核大小 |

### BatchProcessingRequest 类

批量处理请求配置类。

```csharp
public class BatchProcessingRequest
{
    public string SourceDirectory { get; set; }
    public string TargetDirectory { get; set; }
    public ProcessingOptions Options { get; set; }
    public int MaxDegreeOfParallelism { get; set; } = -1;
    public string[] SupportedExtensions { get; set; }
    public bool SkipExisting { get; set; } = true;
}
```

#### 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `SourceDirectory` | string | - | 源图片目录路径 |
| `TargetDirectory` | string | - | 输出目录路径 |
| `Options` | ProcessingOptions | - | 处理选项配置 |
| `MaxDegreeOfParallelism` | int | -1 | 最大并行度(-1为自动) |
| `SupportedExtensions` | string[] | jpg,png,bmp,gif,tiff | 支持的文件扩展名 |
| `SkipExisting` | bool | true | 是否跳过已存在的文件 |

### ProcessingResult 类

处理结果类，包含详细的统计信息。

```csharp
public class ProcessingResult
{
    public bool Success { get; set; }
    public ProcessingStatistics Statistics { get; set; }
    public List<string> Errors { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}
```

#### 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `Success` | bool | 处理是否成功 |
| `Statistics` | ProcessingStatistics | 详细统计信息 |
| `Errors` | List<string> | 错误信息列表 |
| `StartTime` | DateTime | 处理开始时间 |
| `EndTime` | DateTime | 处理结束时间 |
| `Duration` | TimeSpan | 总耗时 |

### ProcessingStatistics 类

处理统计信息类。

```csharp
public class ProcessingStatistics
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int FailedFiles { get; set; }
    public long TotalInputBytes { get; set; }
    public long TotalOutputBytes { get; set; }
    public List<FileProcessingTime> ProcessingTimes { get; set; }
    public double AverageSpeed { get; set; }
    public double CompressionRatio { get; }
}
```

#### 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `TotalFiles` | int | 总文件数 |
| `ProcessedFiles` | int | 成功处理的文件数 |
| `SkippedFiles` | int | 跳过的文件数 |
| `FailedFiles` | int | 失败的文件数 |
| `TotalInputBytes` | long | 总输入字节数 |
| `TotalOutputBytes` | long | 总输出字节数 |
| `ProcessingTimes` | List<FileProcessingTime> | 每个文件的处理时间 |
| `AverageSpeed` | double | 平均处理速度(文件/秒) |
| `CompressionRatio` | double | 压缩率(%) |

## 服务类

### LoggerService 类

日志服务实现类。

```csharp
public class LoggerService : ILogger
{
    public LoggerService(string logFilePath = null)
    public void SetLogFile(string filePath)
}
```

#### 构造函数

| 参数 | 类型 | 说明 |
|------|------|------|
| `logFilePath` | string | 可选的日志文件路径 |

#### 方法

| 方法 | 参数 | 说明 |
|------|------|------|
| `SetLogFile` | filePath | 设置日志文件路径 |

### ImageProcessorService 类

图像处理服务实现类。

```csharp
public class ImageProcessorService : IImageProcessor
{
    public ImageProcessorService(ITransparencyMaker transparencyMaker, ILogger logger)
}
```

#### 依赖注入

| 参数 | 类型 | 说明 |
|------|------|------|
| `transparencyMaker` | ITransparencyMaker | 透明化处理服务 |
| `logger` | ILogger | 日志服务 |

### TransparencyMakerService 类

透明化处理服务实现类。

```csharp
public class TransparencyMakerService : ITransparencyMaker
{
    public TransparencyMakerService(IColorMatcher colorMatcher, ILogger logger)
}
```

#### 依赖注入

| 参数 | 类型 | 说明 |
|------|------|------|
| `colorMatcher` | IColorMatcher | 颜色匹配服务 |
| `logger` | ILogger | 日志服务 |

### ColorMatcherService 类

颜色匹配服务实现类。

```csharp
public class ColorMatcherService : IColorMatcher
```

#### 算法特性

- **多色彩空间**: 结合RGB、HSV、Lab色彩空间进行匹配
- **智能权重**: 根据颜色特性调整匹配权重
- **高性能**: 优化的计算算法

## 工具类

### ColorSpaceConverter 类

色彩空间转换工具类。

```csharp
public static class ColorSpaceConverter
{
    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
}
```

#### 方法说明

| 方法 | 参数 | 返回值 | 说明 |
|------|------|--------|------|
| `RgbToHsv` | r, g, b | (H, S, V) | RGB转HSV色彩空间 |
| `RgbToLab` | r, g, b | (L, A, B) | RGB转Lab色彩空间 |

### ServiceContainer 类

简单的依赖注入容器。

```csharp
public class ServiceContainer
{
    public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance)
    public void RegisterFactory<TInterface>(Func<ServiceContainer, TInterface> factory)
    public T GetService<T>()
    public bool IsRegistered<T>()
}
```

#### 方法说明

| 方法 | 参数 | 说明 |
|------|------|------|
| `RegisterSingleton` | instance | 注册单例服务 |
| `RegisterFactory` | factory | 注册服务工厂 |
| `GetService` | - | 获取服务实例 |
| `IsRegistered` | - | 检查服务是否已注册 |

## 使用示例

### 基本用法

```csharp
// 设置日志
BatchImageProcessor.SetLogLevel(LogLevel.Info);
BatchImageProcessor.SetLogFile("processing.log");

// 批量处理
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: "input",
    targetDirectory: "output",
    backgroundColor: Color.White,
    tolerance: 50
);
```

### 高级用法

```csharp
// 创建处理请求
var request = new BatchProcessingRequest
{
    SourceDirectory = "input",
    TargetDirectory = "output",
    MaxDegreeOfParallelism = 4,
    Options = new ProcessingOptions
    {
        BackgroundColor = Color.FromArgb(255, 255, 255),
        Tolerance = 60,
        EnableEdgeSmoothing = true,
        EnableAdaptiveTolerance = true,
        EnableGradientAlpha = true,
        DespeckleSize = 3
    }
};

// 执行处理
var result = BatchImageProcessor.ProcessBatch(request);

// 检查结果
if (result.Success)
{
    Console.WriteLine($"处理完成: {result.Statistics.ProcessedFiles} 个文件");
    Console.WriteLine($"平均速度: {result.Statistics.AverageSpeed:F2} 文件/秒");
    Console.WriteLine($"压缩率: {result.Statistics.CompressionRatio:F2}%");
}
else
{
    Console.WriteLine("处理失败:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  {error}");
    }
}
```

### 单张图片处理

```csharp
using (var source = new Bitmap("input.jpg"))
{
    var options = new ProcessingOptions
    {
        BackgroundColor = Color.White,
        Tolerance = 50,
        EnableGradientAlpha = true
    };
    
    using (var processed = BatchImageProcessor.ProcessSingleImage(source, options))
    {
        processed.Save("output.png", ImageFormat.Png);
    }
}
```

### 自定义服务

```csharp
// 获取服务容器
var container = BatchImageProcessor.GetServiceContainer();

// 使用服务
var logger = container.GetService<ILogger>();
var transparencyMaker = container.GetService<ITransparencyMaker>();

// 自定义处理
var transparencyOptions = new TransparencyOptions
{
    TargetColor = Color.Blue,
    Tolerance = 30,
    UseGradient = true
};

using (var result = transparencyMaker.MakeColorTransparent(sourceBitmap, transparencyOptions))
{
    result.Save("custom_output.png", ImageFormat.Png);
}
```

## 性能优化建议

### 并行处理

```csharp
// 设置合适的并行度
var request = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = Environment.ProcessorCount - 1, // 保留一个核心给系统
    // ... 其他配置
};
```

### 内存管理

```csharp
// 及时释放资源
using (var source = new Bitmap("input.jpg"))
using (var processed = processor.ProcessImage(source, options))
{
    processed.Save("output.png", ImageFormat.Png);
    // 自动释放内存
}
```

### 批量处理优化

```csharp
var request = new BatchProcessingRequest
{
    SkipExisting = true, // 跳过已存在文件
    SupportedExtensions = new[] { "*.jpg", "*.png" }, // 只处理需要的格式
    // ... 其他配置
};
```