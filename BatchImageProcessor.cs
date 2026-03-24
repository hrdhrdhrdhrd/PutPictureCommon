using System;
using System.Drawing;
using PutPicture.Core;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

/// <summary>
/// 批量图片处理器 - 主入口类（重构版）
/// </summary>
public class BatchImageProcessor
{
    private static ServiceContainer _serviceContainer;
    private static IImageProcessor _imageProcessor;
    private static ILogger _logger;
    
    /// <summary>
    /// 静态构造函数 - 初始化服务容器
    /// </summary>
    static BatchImageProcessor()
    {
        _serviceContainer = ServiceRegistration.ConfigureServices();
        _imageProcessor = _serviceContainer.GetService<IImageProcessor>();
        _logger = _serviceContainer.GetService<ILogger>();
    }
    
    /// <summary>
    /// 批量处理所有图片（兼容旧接口）
    /// </summary>
    public static void ProcessAllImages(string sourceDirectory, 
                                       string targetDirectory,
                                       Color backgroundColor,
                                       int tolerance = 50,
                                       int maxDegreeOfParallelism = -1,
                                       bool enableEdgeSmoothing = true,
                                       bool enableAdaptiveTolerance = true,
                                       bool enableMorphology = true,
                                       bool enableGradientAlpha = true,
                                       int despeckleSize = 3)
    {
        var request = new BatchProcessingRequest
        {
            SourceDirectory = sourceDirectory,
            TargetDirectory = targetDirectory,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            Options = new ProcessingOptions
            {
                BackgroundColor = backgroundColor == default ? Color.White : backgroundColor,
                Tolerance = tolerance,
                EnableEdgeSmoothing = enableEdgeSmoothing,
                EnableAdaptiveTolerance = enableAdaptiveTolerance,
                EnableMorphology = enableMorphology,
                EnableGradientAlpha = enableGradientAlpha,
                DespeckleSize = despeckleSize
            }
        };
        
        var result = _imageProcessor.ProcessBatch(request);
        
        if (!result.Success)
        {
            _logger.Error("批量处理失败");
            foreach (var error in result.Errors)
            {
                _logger.Error(error);
            }
        }
    }
    
    /// <summary>
    /// 批量处理图片（新接口）
    /// </summary>
    public static ProcessingResult ProcessBatch(BatchProcessingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        
        return _imageProcessor.ProcessBatch(request);
    }
    
    /// <summary>
    /// 处理单张图片
    /// </summary>
    public static Bitmap ProcessSingleImage(Bitmap source, ProcessingOptions options)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (options == null) throw new ArgumentNullException(nameof(options));
        
        return _imageProcessor.ProcessImage(source, options);
    }
    
    /// <summary>
    /// 设置日志级别
    /// </summary>
    public static void SetLogLevel(LogLevel level)
    {
        _logger.Level = level;
    }
    
    /// <summary>
    /// 设置日志文件路径
    /// </summary>
    public static void SetLogFile(string filePath)
    {
        if (_logger is Services.LoggerService loggerService)
        {
            loggerService.SetLogFile(filePath);
        }
    }
    
    /// <summary>
    /// 获取服务容器（用于高级用法）
    /// </summary>
    public static ServiceContainer GetServiceContainer()
    {
        return _serviceContainer;
    }
    
    /// <summary>
    /// 重新配置服务容器
    /// </summary>
    public static void ConfigureServices(string logFilePath = null)
    {
        _serviceContainer = ServiceRegistration.ConfigureServices(logFilePath);
        _imageProcessor = _serviceContainer.GetService<IImageProcessor>();
        _logger = _serviceContainer.GetService<ILogger>();
    }
}
