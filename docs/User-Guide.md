# 用户使用指南

## 目录
- [快速开始](#快速开始)
- [基础用法](#基础用法)
- [高级功能](#高级功能)
- [配置说明](#配置说明)
- [性能优化](#性能优化)
- [常见问题](#常见问题)
- [最佳实践](#最佳实践)

## 快速开始

### 环境要求

- .NET Framework 4.7.2 或更高版本
- Windows 操作系统
- 支持的图片格式：JPG, PNG, BMP, GIF, TIFF

### 基本使用

最简单的使用方式：

```csharp
using System.Drawing;

// 批量处理图片，将白色背景变透明
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: @"C:\Images\Input",      // 输入目录
    targetDirectory: @"C:\Images\Output",     // 输出目录
    backgroundColor: Color.White              // 要透明化的颜色
);
```

### 项目设置

由于使用了 unsafe 代码以获得最佳性能，需要在项目设置中启用：

1. 右键项目 → 属性
2. 生成 → 勾选"允许不安全代码"
3. 或在 .csproj 文件中添加：
```xml
<PropertyGroup>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

## 基础用法

### 1. 简单批量处理

```csharp
// 处理指定目录下的所有图片
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: @"D:\Photos\Original",
    targetDirectory: @"D:\Photos\Transparent",
    backgroundColor: Color.White,
    tolerance: 50                             // 颜色容差
);
```

### 2. 自定义背景色

```csharp
// 处理蓝色背景
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: @"D:\Photos\BlueBackground",
    targetDirectory: @"D:\Photos\Transparent",
    backgroundColor: Color.FromArgb(0, 100, 200), // 自定义RGB颜色
    tolerance: 30
);
```

### 3. 调整处理参数

```csharp
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: @"D:\Photos\Input",
    targetDirectory: @"D:\Photos\Output",
    backgroundColor: Color.White,
    tolerance: 60,                            // 增加容差，匹配更多相似颜色
    maxDegreeOfParallelism: 4,               // 限制并行度
    enableEdgeSmoothing: true,               // 启用边缘平滑
    enableAdaptiveTolerance: true,           // 启用自适应容差
    enableMorphology: true,                  // 启用形态学处理
    enableGradientAlpha: true,               // 启用渐变透明
    despeckleSize: 3                         // 去噪核大小
);
```

## 高级功能

### 1. 使用新的API接口

```csharp
using PutPicture.Core.Models;

// 创建处理请求
var request = new BatchProcessingRequest
{
    SourceDirectory = @"D:\Photos\Input",
    TargetDirectory = @"D:\Photos\Output",
    MaxDegreeOfParallelism = Environment.ProcessorCount - 1,
    SkipExisting = true,                     // 跳过已存在的文件
    SupportedExtensions = new[] { "*.jpg", "*.png" }, // 只处理特定格式
    Options = new ProcessingOptions
    {
        BackgroundColor = Color.White,
        Tolerance = 50,
        EnableEdgeSmoothing = true,
        EnableAdaptiveTolerance = true,
        EnableMorphology = true,
        EnableGradientAlpha = true,
        DespeckleSize = 3
    }
};

// 执行处理并获取详细结果
var result = BatchImageProcessor.ProcessBatch(request);

// 检查处理结果
if (result.Success)
{
    Console.WriteLine($"处理完成！");
    Console.WriteLine($"总文件数: {result.Statistics.TotalFiles}");
    Console.WriteLine($"成功处理: {result.Statistics.ProcessedFiles}");
    Console.WriteLine($"跳过文件: {result.Statistics.SkippedFiles}");
    Console.WriteLine($"失败文件: {result.Statistics.FailedFiles}");
    Console.WriteLine($"总耗时: {result.Duration.TotalSeconds:F2} 秒");
    Console.WriteLine($"平均速度: {result.Statistics.AverageSpeed:F2} 张/秒");
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

### 2. 单张图片处理

```csharp
using System.Drawing.Imaging;

// 处理单张图片
using (var source = new Bitmap(@"D:\Photos\input.jpg"))
{
    var options = new ProcessingOptions
    {
        BackgroundColor = Color.White,
        Tolerance = 50,
        EnableGradientAlpha = true,          // 启用渐变透明效果
        EnableEdgeSmoothing = true           // 启用边缘平滑
    };
    
    using (var processed = BatchImageProcessor.ProcessSingleImage(source, options))
    {
        processed.Save(@"D:\Photos\output.png", ImageFormat.Png);
        Console.WriteLine("单张图片处理完成");
    }
}
```

### 3. 日志配置

```csharp
using PutPicture.Core.Interfaces;

// 设置日志级别
BatchImageProcessor.SetLogLevel(LogLevel.Debug);

// 设置日志文件
BatchImageProcessor.SetLogFile(@"D:\Logs\processing.log");

// 现在所有处理过程都会记录到文件中
BatchImageProcessor.ProcessAllImages(
    sourceDirectory: @"D:\Photos\Input",
    targetDirectory: @"D:\Photos\Output",
    backgroundColor: Color.White
);
```

### 4. 高级服务使用

```csharp
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

// 获取服务容器
var container = BatchImageProcessor.GetServiceContainer();

// 直接使用服务
var logger = container.GetService<ILogger>();
var transparencyMaker = container.GetService<ITransparencyMaker>();

logger.Info("开始自定义处理");

// 自定义透明化处理
using (var source = new Bitmap(@"D:\Photos\input.jpg"))
{
    var transparencyOptions = new TransparencyOptions
    {
        TargetColor = Color.FromArgb(240, 240, 240), // 浅灰色
        Tolerance = 40,
        UseGradient = true,                          // 使用渐变透明
        AdaptiveTolerance = true                     // 使用自适应容差
    };
    
    using (var result = transparencyMaker.MakeColorTransparent(source, transparencyOptions))
    {
        result.Save(@"D:\Photos\custom_output.png", ImageFormat.Png);
        logger.Info("自定义处理完成");
    }
}
```

## 配置说明

### ProcessingOptions 详细配置

```csharp
var options = new ProcessingOptions
{
    // 基础设置
    BackgroundColor = Color.White,           // 要透明化的背景色
    Tolerance = 50,                          // 颜色容差 (0-255)
    
    // 高级功能开关
    EnableEdgeSmoothing = true,              // 边缘平滑处理
    EnableAdaptiveTolerance = true,          // 自适应容差计算
    EnableMorphology = true,                 // 形态学处理（去噪）
    EnableGradientAlpha = true,              // 渐变透明效果
    
    // 参数调整
    DespeckleSize = 3                        // 去噪核大小 (1-7)
};
```

#### 参数说明

| 参数 | 范围 | 推荐值 | 说明 |
|------|------|--------|------|
| `Tolerance` | 0-255 | 30-80 | 容差越大，匹配的颜色范围越广 |
| `DespeckleSize` | 1-7 | 3 | 去噪核大小，值越大去噪效果越强 |

#### 功能开关说明

| 功能 | 效果 | 性能影响 | 推荐场景 |
|------|------|----------|----------|
| `EnableEdgeSmoothing` | 边缘抗锯齿 | 中等 | 需要高质量边缘的场景 |
| `EnableAdaptiveTolerance` | 智能调整容差 | 低 | 图片复杂度差异大的批量处理 |
| `EnableMorphology` | 去除噪点和孔洞 | 高 | 有噪点或小孔洞的图片 |
| `EnableGradientAlpha` | 边缘渐变透明 | 中等 | 需要自然边缘过渡的场景 |

### BatchProcessingRequest 详细配置

```csharp
var request = new BatchProcessingRequest
{
    // 基本路径
    SourceDirectory = @"D:\Input",
    TargetDirectory = @"D:\Output",
    
    // 性能设置
    MaxDegreeOfParallelism = 4,              // 并行度，-1为自动
    
    // 文件处理设置
    SkipExisting = true,                     // 跳过已存在文件
    SupportedExtensions = new[]              // 支持的文件格式
    {
        "*.jpg", "*.jpeg", "*.png", 
        "*.bmp", "*.gif", "*.tiff"
    },
    
    // 处理选项
    Options = new ProcessingOptions { ... }
};
```

## 性能优化

### 1. 并行度设置

```csharp
// 根据CPU核心数设置
int optimalParallelism = Math.Max(1, Environment.ProcessorCount - 1);

var request = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = optimalParallelism,
    // ... 其他设置
};
```

### 2. 内存优化

```csharp
// 及时释放大图片资源
foreach (var imagePath in imagePaths)
{
    using (var source = new Bitmap(imagePath))
    {
        // 检查图片大小，对大图片进行特殊处理
        if (source.Width * source.Height > 4000 * 4000)
        {
            // 大图片使用更保守的设置
            var options = new ProcessingOptions
            {
                EnableMorphology = false,    // 关闭耗内存的功能
                DespeckleSize = 1           // 减小核大小
            };
        }
        
        using (var result = BatchImageProcessor.ProcessSingleImage(source, options))
        {
            result.Save(outputPath, ImageFormat.Png);
        }
    }
    
    // 强制垃圾回收（仅在处理大量大图片时使用）
    if (processedCount % 100 == 0)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
```

### 3. 磁盘I/O优化

```csharp
// 使用SSD存储临时文件
var request = new BatchProcessingRequest
{
    SourceDirectory = @"D:\Input",           // 机械硬盘
    TargetDirectory = @"C:\Temp\Output",     // SSD临时目录
    // ... 其他设置
};

// 处理完成后移动到最终目录
// Directory.Move(@"C:\Temp\Output", @"D:\Final\Output");
```

### 4. 批量处理优化

```csharp
// 按文件大小分组处理
var files = Directory.GetFiles(sourceDir, "*.jpg");
var smallFiles = files.Where(f => new FileInfo(f).Length < 1024 * 1024).ToArray(); // < 1MB
var largeFiles = files.Where(f => new FileInfo(f).Length >= 1024 * 1024).ToArray(); // >= 1MB

// 小文件高并行度处理
var smallFileRequest = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = Environment.ProcessorCount,
    // ... 其他设置
};

// 大文件低并行度处理
var largeFileRequest = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = 2,
    // ... 其他设置
};
```

## 常见问题

### Q1: 处理后的图片边缘有锯齿怎么办？

**A**: 启用边缘平滑功能：

```csharp
var options = new ProcessingOptions
{
    EnableEdgeSmoothing = true,              // 启用边缘平滑
    EnableGradientAlpha = true,              // 启用渐变透明
    Tolerance = 40                           // 适当降低容差
};
```

### Q2: 某些相似颜色没有被透明化？

**A**: 增加容差值或启用自适应容差：

```csharp
var options = new ProcessingOptions
{
    Tolerance = 80,                          // 增加容差
    EnableAdaptiveTolerance = true           // 启用自适应容差
};
```

### Q3: 处理速度太慢？

**A**: 优化性能设置：

```csharp
var options = new ProcessingOptions
{
    EnableMorphology = false,                // 关闭耗时的形态学处理
    EnableEdgeSmoothing = false,             // 关闭边缘平滑
    DespeckleSize = 1                        // 减小去噪核大小
};

var request = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = Environment.ProcessorCount, // 最大并行度
    Options = options
};
```

### Q4: 内存使用过高？

**A**: 控制并行度和启用垃圾回收：

```csharp
var request = new BatchProcessingRequest
{
    MaxDegreeOfParallelism = 2,              // 降低并行度
    // ... 其他设置
};

// 定期强制垃圾回收
GC.Collect();
GC.WaitForPendingFinalizers();
```

### Q5: 某些图片处理失败？

**A**: 检查错误信息并启用详细日志：

```csharp
BatchImageProcessor.SetLogLevel(LogLevel.Debug);
BatchImageProcessor.SetLogFile("debug.log");

var result = BatchImageProcessor.ProcessBatch(request);

if (!result.Success)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"错误: {error}");
    }
}
```

### Q6: 如何处理特殊颜色（如渐变背景）？

**A**: 使用渐变透明和自适应容差：

```csharp
var options = new ProcessingOptions
{
    BackgroundColor = Color.White,           // 主要背景色
    Tolerance = 60,                          // 较高容差
    EnableGradientAlpha = true,              // 渐变透明
    EnableAdaptiveTolerance = true,          // 自适应容差
    EnableMorphology = true                  // 形态学处理
};
```

## 最佳实践

### 1. 文件组织

```
项目目录/
├── Input/                    # 原始图片
│   ├── photos/
│   └── graphics/
├── Output/                   # 处理结果
│   ├── photos_transparent/
│   └── graphics_transparent/
├── Logs/                     # 日志文件
└── Config/                   # 配置文件
```

### 2. 批量处理流程

```csharp
public void ProcessImageBatch(string inputDir, string outputDir)
{
    // 1. 设置日志
    BatchImageProcessor.SetLogLevel(LogLevel.Info);
    BatchImageProcessor.SetLogFile($"Logs/batch_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    
    // 2. 验证目录
    if (!Directory.Exists(inputDir))
        throw new DirectoryNotFoundException($"输入目录不存在: {inputDir}");
    
    // 3. 创建输出目录
    Directory.CreateDirectory(outputDir);
    
    // 4. 配置处理选项
    var request = new BatchProcessingRequest
    {
        SourceDirectory = inputDir,
        TargetDirectory = outputDir,
        MaxDegreeOfParallelism = Environment.ProcessorCount - 1,
        SkipExisting = true,
        Options = new ProcessingOptions
        {
            BackgroundColor = Color.White,
            Tolerance = 50,
            EnableEdgeSmoothing = true,
            EnableAdaptiveTolerance = true
        }
    };
    
    // 5. 执行处理
    var result = BatchImageProcessor.ProcessBatch(request);
    
    // 6. 报告结果
    Console.WriteLine($"处理完成: {result.Statistics.ProcessedFiles}/{result.Statistics.TotalFiles}");
    
    if (result.Statistics.FailedFiles > 0)
    {
        Console.WriteLine($"失败文件: {result.Statistics.FailedFiles}");
        // 可以选择重新处理失败的文件
    }
}
```

### 3. 错误处理

```csharp
public void SafeProcessImages(string inputDir, string outputDir)
{
    try
    {
        var result = BatchImageProcessor.ProcessBatch(new BatchProcessingRequest
        {
            SourceDirectory = inputDir,
            TargetDirectory = outputDir,
            Options = new ProcessingOptions
            {
                BackgroundColor = Color.White,
                Tolerance = 50
            }
        });
        
        // 检查部分失败的情况
        if (result.Statistics.FailedFiles > 0)
        {
            Console.WriteLine($"警告: {result.Statistics.FailedFiles} 个文件处理失败");
            
            // 记录失败文件信息
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  {error}");
            }
        }
    }
    catch (DirectoryNotFoundException ex)
    {
        Console.WriteLine($"目录不存在: {ex.Message}");
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.WriteLine($"权限不足: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"处理失败: {ex.Message}");
        
        // 记录详细错误信息到日志
        BatchImageProcessor.SetLogLevel(LogLevel.Error);
        var logger = BatchImageProcessor.GetServiceContainer().GetService<ILogger>();
        logger.Error("批量处理异常", ex);
    }
}
```

### 4. 配置管理

```csharp
// 创建配置类
public class ProcessingConfig
{
    public Color BackgroundColor { get; set; } = Color.White;
    public int Tolerance { get; set; } = 50;
    public bool EnableEdgeSmoothing { get; set; } = true;
    public bool EnableAdaptiveTolerance { get; set; } = true;
    public int MaxParallelism { get; set; } = -1;
    
    public ProcessingOptions ToProcessingOptions()
    {
        return new ProcessingOptions
        {
            BackgroundColor = BackgroundColor,
            Tolerance = Tolerance,
            EnableEdgeSmoothing = EnableEdgeSmoothing,
            EnableAdaptiveTolerance = EnableAdaptiveTolerance
        };
    }
}

// 使用配置
var config = LoadConfigFromFile("config.json"); // 从文件加载配置
var options = config.ToProcessingOptions();
```

通过遵循这些最佳实践，你可以更高效、更可靠地使用 PutPicture 进行图片透明化处理。