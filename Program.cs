using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;
using PutPicture.Services;

namespace PutPicture
{
    /// <summary>
    /// 主程序入口
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // 设置日志级别和文件
                BatchImageProcessor.SetLogLevel(LogLevel.Info);
                BatchImageProcessor.SetLogFile("processing.log");
                
                // 示例1: 使用旧接口（向后兼容）
                Console.WriteLine("=== 示例1: 使用旧接口 ===");
                BatchImageProcessor.ProcessAllImages(
                    sourceDirectory: "images",
                    targetDirectory: "images_transparent",
                    backgroundColor: Color.White,
                    tolerance: 50,
                    enableEdgeSmoothing: true,
                    enableAdaptiveTolerance: true
                );
                
                // 示例2: 使用新接口
                Console.WriteLine("\n=== 示例2: 使用新接口 ===");
                var request = new BatchProcessingRequest
                {
                    SourceDirectory = "images",
                    TargetDirectory = "images_transparent_new",
                    MaxDegreeOfParallelism = 4,
                    SkipExisting = true,
                    Options = new ProcessingOptions
                    {
                        BackgroundColor = Color.White,
                        Tolerance = 60,
                        EnableEdgeSmoothing = true,
                        EnableAdaptiveTolerance = true,
                        EnableMorphology = true,
                        EnableGradientAlpha = true,
                        DespeckleSize = 3
                    }
                };
                
                var result = BatchImageProcessor.ProcessBatch(request);
                
                // 输出详细结果
                Console.WriteLine($"处理结果: {(result.Success ? "成功" : "失败")}");
                Console.WriteLine($"总文件数: {result.Statistics.TotalFiles}");
                Console.WriteLine($"成功处理: {result.Statistics.ProcessedFiles}");
                Console.WriteLine($"跳过文件: {result.Statistics.SkippedFiles}");
                Console.WriteLine($"失败文件: {result.Statistics.FailedFiles}");
                Console.WriteLine($"总耗时: {result.Duration.TotalSeconds:F2} 秒");
                Console.WriteLine($"平均速度: {result.Statistics.AverageSpeed:F2} 张/秒");
                Console.WriteLine($"压缩率: {result.Statistics.CompressionRatio:F2}%");
                
                // 示例3: 处理单张图片
                Console.WriteLine("\n=== 示例3: 处理单张图片 ===");
                if (System.IO.File.Exists("images/1.png"))
                {
                    using (var source = new Bitmap("images/1.png"))
                    {
                        var options = new ProcessingOptions
                        {
                            BackgroundColor = Color.White,
                            Tolerance = 50,
                            EnableGradientAlpha = true
                        };
                        
                        using (var processed = BatchImageProcessor.ProcessSingleImage(source, options))
                        {
                            processed.Save("single_processed.png", System.Drawing.Imaging.ImageFormat.Png);
                            Console.WriteLine("单张图片处理完成: single_processed.png");
                        }
                    }
                }
                
                // 示例4: 高级用法 - 直接使用服务
                Console.WriteLine("\n=== 示例4: 高级用法 ===");
                var container = BatchImageProcessor.GetServiceContainer();
                var logger = container.GetService<ILogger>();
                var transparencyMaker = container.GetService<ITransparencyMaker>();
                
                logger.Info("这是通过服务容器获取的日志服务");
                
                // 示例5: 帧提取功能
                Console.WriteLine("\n=== 示例5: 帧提取功能 ===");
                await DemonstrateFrameExtraction(container);
                
                Console.WriteLine("所有示例执行完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"程序执行出错: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
        
        /// <summary>
        /// 演示帧提取功能
        /// </summary>
        private static async Task DemonstrateFrameExtraction(Core.ServiceContainer container)
        {
            try
            {
                var frameManager = container.GetService<FrameExtractionManagerService>();
                var logger = container.GetService<ILogger>();
                
                // 查找视频文件
                var videoFiles = Directory.GetFiles("videos", "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => frameManager.IsFileSupported(f))
                    .ToArray();
                
                if (!videoFiles.Any())
                {
                    Console.WriteLine("未找到支持的视频文件，跳过帧提取演示");
                    return;
                }
                
                var videoFile = videoFiles.First();
                Console.WriteLine($"使用视频文件: {Path.GetFileName(videoFile)}");
                
                // 订阅进度事件
                frameManager.ProgressReported += (sender, e) =>
                {
                    Console.WriteLine($"进度: {e.ProgressPercentage}% ({e.CurrentFrameIndex}/{e.TotalFrames}) - {e.CurrentFileName}");
                };
                
                // 5.1 快速提取10帧
                Console.WriteLine("\n--- 快速提取10帧 ---");
                var quickResult = await frameManager.QuickExtractAsync(
                    videoFile, 
                    "frames_quick", 
                    frameCount: 10
                );
                
                if (quickResult.Success)
                {
                    Console.WriteLine($"快速提取完成: {quickResult.Statistics.SuccessfulFrames} 帧, 耗时: {quickResult.Duration.TotalSeconds:F2}秒");
                }
                
                // 5.2 按时间间隔提取
                Console.WriteLine("\n--- 按10秒间隔提取 ---");
                var intervalResult = await frameManager.ExtractByIntervalAsync(
                    videoFile,
                    "frames_interval",
                    TimeSpan.FromSeconds(10)
                );
                
                if (intervalResult.Success)
                {
                    Console.WriteLine($"间隔提取完成: {intervalResult.Statistics.SuccessfulFrames} 帧");
                }
                
                // 5.3 提取关键帧
                Console.WriteLine("\n--- 提取关键帧 ---");
                var keyFramesResult = await frameManager.ExtractKeyFramesAsync(
                    videoFile,
                    "frames_key"
                );
                
                if (keyFramesResult.Success)
                {
                    Console.WriteLine($"关键帧提取完成: {keyFramesResult.Statistics.SuccessfulFrames} 帧");
                }
                
                // 5.4 创建视频预览图
                Console.WriteLine("\n--- 创建视频预览图 ---");
                var previewPath = await frameManager.CreateVideoPreviewAsync(
                    videoFile,
                    "video_preview.jpg",
                    previewFrameCount: 9,
                    thumbnailSize: new Size(200, 150)
                );
                
                Console.WriteLine($"视频预览图已创建: {previewPath}");
                
                // 5.5 批量处理多个文件
                if (videoFiles.Length > 1)
                {
                    Console.WriteLine("\n--- 批量处理多个文件 ---");
                    var batchRequest = new BatchFrameExtractionRequest
                    {
                        SourceFiles = videoFiles.Take(2).ToList(),
                        OutputDirectory = "frames_batch",
                        Interval = TimeSpan.FromSeconds(30),
                        Options = new FrameExtractionOptions
                        {
                            Quality = 85,
                            MaxWidth = 800,
                            MaxHeight = 600
                        }
                    };
                    
                    var batchResult = await frameManager.BatchExtractAsync(batchRequest);
                    
                    if (batchResult.Success)
                    {
                        Console.WriteLine($"批量处理完成: {batchResult.Statistics.SuccessfulFiles}/{batchResult.Statistics.TotalFiles} 文件");
                        Console.WriteLine($"总提取帧数: {batchResult.Statistics.TotalFramesExtracted}");
                    }
                }
                
                // 5.6 处理GIF文件
                var gifFiles = Directory.GetFiles(".", "*.gif", SearchOption.AllDirectories);
                if (gifFiles.Any())
                {
                    Console.WriteLine("\n--- 处理GIF文件 ---");
                    var gifFile = gifFiles.First();
                    
                    var gifResult = await frameManager.QuickExtractAsync(
                        gifFile,
                        "gif_frames",
                        frameCount: 5
                    );
                    
                    if (gifResult.Success)
                    {
                        Console.WriteLine($"GIF帧提取完成: {gifResult.Statistics.SuccessfulFrames} 帧");
                    }
                }
                
                // 5.7 性能估算
                Console.WriteLine("\n--- 性能估算 ---");
                var estimatedTime = await frameManager.EstimateExtractionTimeAsync(videoFile, 20);
                Console.WriteLine($"提取20帧预计耗时: {estimatedTime.TotalSeconds:F1} 秒");
                
                Console.WriteLine("\n帧提取演示完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"帧提取演示失败: {ex.Message}");
            }
        }
    }
}