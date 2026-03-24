using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

namespace PutPicture.Services
{
    /// <summary>
    /// 帧提取管理服务 - 提供统一的高级API
    /// </summary>
    public class FrameExtractionManagerService
    {
        private readonly OptimizedFrameExtractorService _frameExtractor;
        private readonly ILogger _logger;
        
        /// <summary>
        /// 进度报告事件
        /// </summary>
        public event EventHandler<FrameExtractionProgressEventArgs> ProgressReported;
        
        public FrameExtractionManagerService(OptimizedFrameExtractorService frameExtractor, ILogger logger)
        {
            _frameExtractor = frameExtractor ?? throw new ArgumentNullException(nameof(frameExtractor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _frameExtractor.ProgressReported += OnProgressReported;
        }
        
        /// <summary>
        /// 快速提取帧 - 自动优化参数
        /// </summary>
        public async Task<FrameExtractionResult> QuickExtractAsync(string sourcePath, string outputDirectory, int frameCount = 10)
        {
            _logger.Info($"快速提取 {frameCount} 帧: {sourcePath}");
            
            var mediaInfo = await _frameExtractor.GetMediaInfoAsync(sourcePath);
            
            // 生成均匀分布的时间戳
            var timestamps = GenerateEvenTimestamps(mediaInfo.Duration, frameCount);
            
            var request = new FrameExtractionRequest
            {
                SourcePath = sourcePath,
                OutputDirectory = outputDirectory,
                Timestamps = timestamps,
                Options = new FrameExtractionOptions
                {
                    FileNameTemplate = "quick_frame_{0:000}",
                    Quality = 85,
                    MaxDegreeOfParallelism = -1, // 自动优化
                    SkipExisting = true
                }
            };
            
            return await _frameExtractor.SmartExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 按时间间隔提取帧
        /// </summary>
        public async Task<FrameExtractionResult> ExtractByIntervalAsync(
            string sourcePath, 
            string outputDirectory, 
            TimeSpan interval,
            FrameExtractionOptions options = null)
        {
            options ??= new FrameExtractionOptions();
            
            _logger.Info($"按间隔 {interval} 提取帧: {sourcePath}");
            
            var mediaInfo = await _frameExtractor.GetMediaInfoAsync(sourcePath);
            
            // 生成时间戳列表
            var timestamps = new List<TimeSpan>();
            var currentTime = TimeSpan.Zero;
            
            while (currentTime < mediaInfo.Duration)
            {
                timestamps.Add(currentTime);
                currentTime = currentTime.Add(interval);
            }
            
            var request = new FrameExtractionRequest
            {
                SourcePath = sourcePath,
                OutputDirectory = outputDirectory,
                Timestamps = timestamps,
                Options = options
            };
            
            return await _frameExtractor.SmartExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 按关键帧提取
        /// </summary>
        public async Task<FrameExtractionResult> ExtractKeyFramesAsync(
            string sourcePath, 
            string outputDirectory,
            FrameExtractionOptions options = null)
        {
            options ??= new FrameExtractionOptions();
            
            _logger.Info($"提取关键帧: {sourcePath}");
            
            var mediaInfo = await _frameExtractor.GetMediaInfoAsync(sourcePath);
            
            // 生成关键时间点（开始、1/4、1/2、3/4、结束）
            var keyTimestamps = new List<TimeSpan>
            {
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(mediaInfo.Duration.TotalMilliseconds * 0.25),
                TimeSpan.FromMilliseconds(mediaInfo.Duration.TotalMilliseconds * 0.5),
                TimeSpan.FromMilliseconds(mediaInfo.Duration.TotalMilliseconds * 0.75),
                TimeSpan.FromMilliseconds(mediaInfo.Duration.TotalMilliseconds * 0.95) // 避免最后一帧可能的问题
            };
            
            var request = new FrameExtractionRequest
            {
                SourcePath = sourcePath,
                OutputDirectory = outputDirectory,
                Timestamps = keyTimestamps,
                Options = options
            };
            
            request.Options.FileNameTemplate = "key_frame_{0:000}";
            
            return await _frameExtractor.SmartExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 批量文件帧提取
        /// </summary>
        public async Task<BatchFrameExtractionResult> BatchExtractAsync(BatchFrameExtractionRequest batchRequest)
        {
            var result = new BatchFrameExtractionResult
            {
                StartTime = DateTime.Now,
                Success = true
            };
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Info($"开始批量帧提取: {batchRequest.SourceFiles.Count} 个文件");
                
                var tasks = batchRequest.SourceFiles.Select(async sourceFile =>
                {
                    try
                    {
                        var outputDir = Path.Combine(batchRequest.OutputDirectory, Path.GetFileNameWithoutExtension(sourceFile));
                        Directory.CreateDirectory(outputDir);
                        
                        var extractionResult = await ExtractByIntervalAsync(sourceFile, outputDir, batchRequest.Interval, batchRequest.Options);
                        
                        return new FileExtractionResult
                        {
                            SourceFile = sourceFile,
                            Success = extractionResult.Success,
                            Result = extractionResult,
                            ErrorMessage = extractionResult.Success ? null : string.Join("; ", extractionResult.Errors)
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"文件处理失败: {sourceFile}", ex);
                        return new FileExtractionResult
                        {
                            SourceFile = sourceFile,
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                });
                
                var fileResults = await Task.WhenAll(tasks);
                result.FileResults.AddRange(fileResults);
                
                // 计算统计信息
                result.Statistics.TotalFiles = fileResults.Length;
                result.Statistics.SuccessfulFiles = fileResults.Count(r => r.Success);
                result.Statistics.FailedFiles = fileResults.Count(r => !r.Success);
                result.Statistics.TotalFramesExtracted = fileResults
                    .Where(r => r.Success && r.Result != null)
                    .Sum(r => r.Result.Statistics.SuccessfulFrames);
                
                _logger.Info($"批量帧提取完成: 成功 {result.Statistics.SuccessfulFiles}/{result.Statistics.TotalFiles} 个文件, 总帧数 {result.Statistics.TotalFramesExtracted}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"批量处理失败: {ex.Message}");
                _logger.Error("批量帧提取失败", ex);
            }
            finally
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
            }
            
            return result;
        }
        
        /// <summary>
        /// 创建视频预览图
        /// </summary>
        public async Task<string> CreateVideoPreviewAsync(
            string videoPath, 
            string outputPath, 
            int previewFrameCount = 9,
            Size? thumbnailSize = null)
        {
            _logger.Info($"创建视频预览图: {videoPath}");
            
            var mediaInfo = await _frameExtractor.GetMediaInfoAsync(videoPath);
            var timestamps = GenerateEvenTimestamps(mediaInfo.Duration, previewFrameCount);
            
            // 提取预览帧
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            try
            {
                var request = new FrameExtractionRequest
                {
                    SourcePath = videoPath,
                    OutputDirectory = tempDir,
                    Timestamps = timestamps,
                    Options = new FrameExtractionOptions
                    {
                        FileNameTemplate = "preview_{0:000}",
                        Quality = 80,
                        MaxWidth = thumbnailSize?.Width ?? 200,
                        MaxHeight = thumbnailSize?.Height ?? 150,
                        MaintainAspectRatio = true
                    }
                };
                
                var result = await _frameExtractor.ExtractFramesAsync(request);
                
                if (!result.Success || result.Statistics.SuccessfulFrames == 0)
                {
                    throw new InvalidOperationException("无法提取预览帧");
                }
                
                // 合成预览图
                var previewImage = CreatePreviewGrid(result.ExtractedFrames.Where(f => f.Success).ToList(), 3, 3);
                previewImage.Save(outputPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                previewImage.Dispose();
                
                _logger.Info($"视频预览图已创建: {outputPath}");
                return outputPath;
            }
            finally
            {
                // 清理临时文件
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"清理临时目录失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 获取支持的文件格式
        /// </summary>
        public string[] GetSupportedFormats()
        {
            return new[]
            {
                ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", // 视频格式
                ".gif" // GIF格式
            };
        }
        
        /// <summary>
        /// 验证文件是否支持
        /// </summary>
        public bool IsFileSupported(string filePath)
        {
            return _frameExtractor.IsSupported(filePath);
        }
        
        /// <summary>
        /// 估算提取时间
        /// </summary>
        public async Task<TimeSpan> EstimateExtractionTimeAsync(string sourcePath, int frameCount)
        {
            try
            {
                var mediaInfo = await _frameExtractor.GetMediaInfoAsync(sourcePath);
                
                // 基于文件大小和帧数的简单估算
                var fileSize = mediaInfo.FileSize;
                var estimatedSecondsPerFrame = fileSize switch
                {
                    < 10 * 1024 * 1024 => 0.1, // < 10MB
                    < 100 * 1024 * 1024 => 0.2, // < 100MB
                    < 500 * 1024 * 1024 => 0.5, // < 500MB
                    _ => 1.0 // >= 500MB
                };
                
                var totalSeconds = frameCount * estimatedSecondsPerFrame;
                return TimeSpan.FromSeconds(totalSeconds);
            }
            catch
            {
                // 如果无法获取信息，返回默认估算
                return TimeSpan.FromSeconds(frameCount * 0.5);
            }
        }
        
        /// <summary>
        /// 生成均匀分布的时间戳
        /// </summary>
        private List<TimeSpan> GenerateEvenTimestamps(TimeSpan duration, int count)
        {
            var timestamps = new List<TimeSpan>();
            
            if (count <= 1)
            {
                timestamps.Add(TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.5));
                return timestamps;
            }
            
            for (int i = 0; i < count; i++)
            {
                var ratio = (double)i / (count - 1);
                var timestamp = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * ratio * 0.95); // 避免最后一帧
                timestamps.Add(timestamp);
            }
            
            return timestamps;
        }
        
        /// <summary>
        /// 创建预览网格图
        /// </summary>
        private Bitmap CreatePreviewGrid(List<ExtractedFrameInfo> frames, int cols, int rows)
        {
            if (!frames.Any()) throw new ArgumentException("没有可用的帧");
            
            // 加载第一帧以获取尺寸
            using var firstFrame = new Bitmap(frames[0].OutputPath);
            var frameWidth = firstFrame.Width;
            var frameHeight = firstFrame.Height;
            
            var gridWidth = frameWidth * cols;
            var gridHeight = frameHeight * rows;
            
            var grid = new Bitmap(gridWidth, gridHeight);
            using var graphics = Graphics.FromImage(grid);
            
            graphics.Clear(Color.Black);
            
            for (int i = 0; i < Math.Min(frames.Count, cols * rows); i++)
            {
                var row = i / cols;
                var col = i % cols;
                
                var x = col * frameWidth;
                var y = row * frameHeight;
                
                try
                {
                    using var frame = new Bitmap(frames[i].OutputPath);
                    graphics.DrawImage(frame, x, y, frameWidth, frameHeight);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"绘制预览帧失败: {frames[i].OutputPath}, {ex.Message}");
                }
            }
            
            return grid;
        }
        
        /// <summary>
        /// 进度事件转发
        /// </summary>
        private void OnProgressReported(object sender, FrameExtractionProgressEventArgs e)
        {
            ProgressReported?.Invoke(this, e);
        }
    }
    
    /// <summary>
    /// 批量帧提取请求
    /// </summary>
    public class BatchFrameExtractionRequest
    {
        /// <summary>
        /// 源文件列表
        /// </summary>
        public List<string> SourceFiles { get; set; } = new List<string>();
        
        /// <summary>
        /// 输出目录
        /// </summary>
        public string OutputDirectory { get; set; }
        
        /// <summary>
        /// 时间间隔
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);
        
        /// <summary>
        /// 提取选项
        /// </summary>
        public FrameExtractionOptions Options { get; set; } = new FrameExtractionOptions();
    }
    
    /// <summary>
    /// 批量帧提取结果
    /// </summary>
    public class BatchFrameExtractionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 文件处理结果列表
        /// </summary>
        public List<FileExtractionResult> FileResults { get; set; } = new List<FileExtractionResult>();
        
        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
        
        /// <summary>
        /// 统计信息
        /// </summary>
        public BatchExtractionStatistics Statistics { get; set; } = new BatchExtractionStatistics();
        
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// 总耗时
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
    
    /// <summary>
    /// 文件提取结果
    /// </summary>
    public class FileExtractionResult
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string SourceFile { get; set; }
        
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 提取结果
        /// </summary>
        public FrameExtractionResult Result { get; set; }
        
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// 批量提取统计信息
    /// </summary>
    public class BatchExtractionStatistics
    {
        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }
        
        /// <summary>
        /// 成功处理的文件数
        /// </summary>
        public int SuccessfulFiles { get; set; }
        
        /// <summary>
        /// 失败的文件数
        /// </summary>
        public int FailedFiles { get; set; }
        
        /// <summary>
        /// 总提取帧数
        /// </summary>
        public int TotalFramesExtracted { get; set; }
    }
}