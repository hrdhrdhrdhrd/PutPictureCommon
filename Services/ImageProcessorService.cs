using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

namespace PutPicture.Services
{
    /// <summary>
    /// 图像处理服务
    /// </summary>
    public class ImageProcessorService : IImageProcessor
    {
        private readonly ITransparencyMaker _transparencyMaker;
        private readonly ILogger _logger;
        
        public ImageProcessorService(ITransparencyMaker transparencyMaker, ILogger logger)
        {
            _transparencyMaker = transparencyMaker ?? throw new ArgumentNullException(nameof(transparencyMaker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 处理单张图片
        /// </summary>
        public Bitmap ProcessImage(Bitmap source, ProcessingOptions options)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (options == null) throw new ArgumentNullException(nameof(options));
            
            var transparencyOptions = new TransparencyOptions
            {
                TargetColor = options.BackgroundColor,
                Tolerance = options.Tolerance,
                UseGradient = options.EnableGradientAlpha,
                AdaptiveTolerance = options.EnableAdaptiveTolerance
            };
            
            var result = _transparencyMaker.MakeColorTransparent(source, transparencyOptions);
            
            // 应用后处理效果
            if (options.EnableMorphology && options.DespeckleSize > 0)
            {
                var morphed = ApplyMorphology(result, options.DespeckleSize);
                result.Dispose();
                result = morphed;
            }
            
            if (options.EnableEdgeSmoothing)
            {
                var smoothed = ApplyEdgeSmoothing(result);
                result.Dispose();
                result = smoothed;
            }
            
            return result;
        }
        
        /// <summary>
        /// 批量处理图片
        /// </summary>
        public ProcessingResult ProcessBatch(BatchProcessingRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            
            var result = new ProcessingResult
            {
                StartTime = DateTime.Now,
                Success = true
            };
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Info("========== 批量图片处理开始 ==========");
                _logger.Info($"源目录: {request.SourceDirectory}");
                _logger.Info($"目标目录: {request.TargetDirectory}");
                
                // 确保目标目录存在
                if (!Directory.Exists(request.TargetDirectory))
                {
                    Directory.CreateDirectory(request.TargetDirectory);
                    _logger.Info($"创建目标目录: {request.TargetDirectory}");
                }
                
                // 扫描文件
                var files = ScanFiles(request);
                result.Statistics.TotalFiles = files.Length;
                
                if (files.Length == 0)
                {
                    _logger.Warning("没有找到需要处理的图片文件");
                    return result;
                }
                
                LogProcessingConfiguration(request.Options);
                
                // 设置并行处理
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = request.MaxDegreeOfParallelism == -1 
                        ? Math.Max(1, Environment.ProcessorCount - 1)
                        : request.MaxDegreeOfParallelism
                };
                
                _logger.Info($"并行度设置: {parallelOptions.MaxDegreeOfParallelism} (CPU核心数: {Environment.ProcessorCount})");
                _logger.Info("========== 开始并行处理 ==========");
                
                // 并行处理文件
                Parallel.ForEach(files, parallelOptions, filePath =>
                {
                    ProcessSingleFile(filePath, request, result.Statistics, result.Errors);
                });
                
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                
                // 计算统计信息
                CalculateStatistics(result.Statistics, stopwatch.Elapsed);
                
                LogProcessingResults(result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"批量处理失败: {ex.Message}");
                _logger.Error("批量处理过程中发生错误", ex);
            }
            
            return result;
        }
        
        /// <summary>
        /// 扫描文件
        /// </summary>
        private string[] ScanFiles(BatchProcessingRequest request)
        {
            _logger.Info("正在扫描图片文件...");
            
            var files = request.SupportedExtensions
                .AsParallel()
                .SelectMany(ext => Directory.GetFiles(request.SourceDirectory, ext, SearchOption.TopDirectoryOnly))
                .Where(file => 
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length == 0)
                    {
                        _logger.Warning($"跳过空文件: {file}");
                        return false;
                    }
                    return true;
                })
                .ToArray();
            
            _logger.Info($"找到 {files.Length} 个有效图片文件");
            return files;
        }
        
        /// <summary>
        /// 处理单个文件
        /// </summary>
        private void ProcessSingleFile(string filePath, BatchProcessingRequest request, 
                                     ProcessingStatistics stats, System.Collections.Generic.List<string> errors)
        {
            var fileStopwatch = Stopwatch.StartNew();
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);
            
            try
            {
                _logger.Debug($"[线程 {Thread.CurrentThread.ManagedThreadId}] 开始处理: {fileName} (大小: {fileInfo.Length / 1024.0:F2} KB)");
                
                string outputFileName = Path.GetFileNameWithoutExtension(filePath) + ".png";
                string outputPath = Path.Combine(request.TargetDirectory, outputFileName);
                
                // 检查是否跳过已存在的文件
                if (request.SkipExisting && File.Exists(outputPath))
                {
                    _logger.Info($"跳过已存在的文件: {outputFileName}");
                    Interlocked.Increment(ref stats.SkippedFiles);
                    return;
                }
                
                // 处理图片
                using (var source = new Bitmap(filePath))
                {
                    _logger.Debug($"图片尺寸: {source.Width}x{source.Height}, 格式: {source.PixelFormat}");
                    
                    using (var processed = ProcessImage(source, request.Options))
                    {
                        processed.Save(outputPath, ImageFormat.Png);
                        
                        var outputFileInfo = new FileInfo(outputPath);
                        Interlocked.Add(ref stats.TotalInputBytes, fileInfo.Length);
                        Interlocked.Add(ref stats.TotalOutputBytes, outputFileInfo.Length);
                        
                        fileStopwatch.Stop();
                        
                        lock (stats.ProcessingTimes)
                        {
                            stats.ProcessingTimes.Add(new FileProcessingTime
                            {
                                FileName = fileName,
                                ProcessingTimeSeconds = fileStopwatch.Elapsed.TotalSeconds,
                                FileSizeBytes = fileInfo.Length
                            });
                        }
                        
                        _logger.Info($"✓ 完成: {fileName} (耗时: {fileStopwatch.Elapsed.TotalSeconds:F2}秒, 输出: {outputFileInfo.Length / 1024.0:F2} KB)");
                    }
                }
                
                Interlocked.Increment(ref stats.ProcessedFiles);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref stats.FailedFiles);
                string errorMsg = $"处理失败: {fileName} - {ex.Message}";
                
                lock (errors)
                {
                    errors.Add(errorMsg);
                }
                
                _logger.Error(errorMsg, ex);
            }
        }
        
        /// <summary>
        /// 记录处理配置
        /// </summary>
        private void LogProcessingConfiguration(ProcessingOptions options)
        {
            _logger.Info("========== 处理参数配置 ==========");
            _logger.Info($"  背景色: R={options.BackgroundColor.R}, G={options.BackgroundColor.G}, B={options.BackgroundColor.B}");
            _logger.Info($"  容差: {options.Tolerance}");
            _logger.Info($"  边缘平滑: {options.EnableEdgeSmoothing}");
            _logger.Info($"  自适应容差: {options.EnableAdaptiveTolerance}");
            _logger.Info($"  形态学处理: {options.EnableMorphology}");
            _logger.Info($"  渐变透明: {options.EnableGradientAlpha}");
            _logger.Info($"  去噪尺寸: {options.DespeckleSize}");
        }
        
        /// <summary>
        /// 计算统计信息
        /// </summary>
        private void CalculateStatistics(ProcessingStatistics stats, TimeSpan duration)
        {
            if (stats.ProcessedFiles > 0 && duration.TotalSeconds > 0)
            {
                stats.AverageSpeed = stats.ProcessedFiles / duration.TotalSeconds;
            }
        }
        
        /// <summary>
        /// 记录处理结果
        /// </summary>
        private void LogProcessingResults(ProcessingResult result)
        {
            var stats = result.Statistics;
            
            _logger.Info("========== 处理完成统计 ==========");
            _logger.Info($"总文件数: {stats.TotalFiles}");
            _logger.Info($"成功处理: {stats.ProcessedFiles}");
            _logger.Info($"跳过文件: {stats.SkippedFiles}");
            _logger.Info($"失败文件: {stats.FailedFiles}");
            _logger.Info($"总耗时: {result.Duration.TotalSeconds:F2} 秒");
            
            if (stats.ProcessedFiles > 0)
            {
                _logger.Info($"平均速度: {stats.AverageSpeed:F2} 张/秒");
                _logger.Info($"输入数据量: {stats.TotalInputBytes / 1024.0 / 1024.0:F2} MB");
                _logger.Info($"输出数据量: {stats.TotalOutputBytes / 1024.0 / 1024.0:F2} MB");
                _logger.Info($"压缩率: {stats.CompressionRatio:F2}%");
                
                if (stats.ProcessingTimes.Any())
                {
                    var times = stats.ProcessingTimes.Select(t => t.ProcessingTimeSeconds).ToList();
                    _logger.Info($"平均处理时间: {times.Average():F2} 秒/张");
                    _logger.Info($"最快处理时间: {times.Min():F2} 秒");
                    _logger.Info($"最慢处理时间: {times.Max():F2} 秒");
                    
                    var slowest = stats.ProcessingTimes.OrderByDescending(t => t.ProcessingTimeSeconds).Take(5);
                    _logger.Debug("处理最慢的文件:");
                    foreach (var item in slowest)
                    {
                        _logger.Debug($"  {item.FileName}: {item.ProcessingTimeSeconds:F2}秒");
                    }
                }
            }
            
            if (result.Errors.Any())
            {
                _logger.Warning("========== 错误详情 ==========");
                foreach (var error in result.Errors)
                {
                    _logger.Warning(error);
                }
            }
            
            _logger.Info("========== 批量处理结束 ==========");
        }
        
        // 临时实现 - 后续可以移到专门的后处理服务中
        private Bitmap ApplyMorphology(Bitmap source, int kernelSize)
        {
            // 简化实现，实际应该调用专门的形态学处理服务
            return new Bitmap(source);
        }
        
        private Bitmap ApplyEdgeSmoothing(Bitmap source)
        {
            // 简化实现，实际应该调用专门的边缘处理服务
            return new Bitmap(source);
        }
    }
}