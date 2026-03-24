using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

namespace PutPicture.Services
{
    /// <summary>
    /// 高效视频帧提取服务
    /// </summary>
    public class VideoFrameExtractorService : IVideoFrameExtractor
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _engineSemaphore;
        private readonly object _progressLock = new object();
        
        /// <summary>
        /// 进度报告事件
        /// </summary>
        public event EventHandler<FrameExtractionProgressEventArgs> ProgressReported;
        
        public VideoFrameExtractorService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 根据CPU核心数设置并发限制
            int maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
            _engineSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency * 2);
        }
        
        /// <summary>
        /// 检查是否支持该文件格式
        /// </summary>
        public bool IsSupported(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var supportedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
            
            return supportedExtensions.Contains(extension);
        }
        
        /// <summary>
        /// 获取媒体信息
        /// </summary>
        public async Task<MediaInfo> GetMediaInfoAsync(string sourcePath)
        {
            if (!IsSupported(sourcePath))
                throw new NotSupportedException($"不支持的文件格式: {sourcePath}");
            
            return await Task.Run(() =>
            {
                try
                {
                    using var engine = new Engine();
                    var inputFile = new MediaFile { Filename = sourcePath };
                    engine.GetMetadata(inputFile);
                    
                    var fileInfo = new FileInfo(sourcePath);
                    
                    return new MediaInfo
                    {
                        FilePath = sourcePath,
                        Type = MediaType.Video,
                        Duration = inputFile.Metadata.Duration,
                        Width = inputFile.Metadata.VideoData?.FrameSize.Width ?? 0,
                        Height = inputFile.Metadata.VideoData?.FrameSize.Height ?? 0,
                        FrameRate = inputFile.Metadata.VideoData?.Fps ?? 0,
                        TotalFrames = CalculateTotalFrames(inputFile.Metadata.Duration, inputFile.Metadata.VideoData?.Fps ?? 0),
                        FileSize = fileInfo.Length,
                        Codec = inputFile.Metadata.VideoData?.Format,
                        Bitrate = inputFile.Metadata.VideoData?.BitRateKbs ?? 0
                    };
                }
                catch (Exception ex)
                {
                    _logger.Error($"获取媒体信息失败: {sourcePath}", ex);
                    throw;
                }
            });
        }
        
        /// <summary>
        /// 提取单帧
        /// </summary>
        public async Task<Bitmap> ExtractFrameAsync(string sourcePath, TimeSpan timestamp)
        {
            if (!IsSupported(sourcePath))
                throw new NotSupportedException($"不支持的文件格式: {sourcePath}");
            
            await _engineSemaphore.WaitAsync();
            
            try
            {
                return await Task.Run(() =>
                {
                    using var engine = new Engine();
                    var inputFile = new MediaFile { Filename = sourcePath };
                    
                    // 创建临时文件
                    var tempPath = Path.GetTempFileName() + ".jpg";
                    var outputFile = new MediaFile { Filename = tempPath };
                    var options = new ConversionOptions { Seek = timestamp };
                    
                    try
                    {
                        engine.GetThumbnail(inputFile, outputFile, options);
                        
                        // 从临时文件加载位图
                        using var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
                        return new Bitmap(fileStream);
                    }
                    finally
                    {
                        // 清理临时文件
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                    }
                });
            }
            finally
            {
                _engineSemaphore.Release();
            }
        }
        
        /// <summary>
        /// 批量提取帧
        /// </summary>
        public async Task<FrameExtractionResult> ExtractFramesAsync(FrameExtractionRequest request)
        {
            var result = new FrameExtractionResult
            {
                StartTime = DateTime.Now,
                Success = true
            };
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Info($"开始批量提取帧: {request.SourcePath}");
                _logger.Info($"输出目录: {request.OutputDirectory}");
                _logger.Info($"提取帧数: {request.Timestamps.Count}");
                
                // 验证输入
                if (!IsSupported(request.SourcePath))
                {
                    throw new NotSupportedException($"不支持的文件格式: {request.SourcePath}");
                }
                
                // 创建输出目录
                Directory.CreateDirectory(request.OutputDirectory);
                
                // 获取媒体信息
                var mediaInfo = await GetMediaInfoAsync(request.SourcePath);
                _logger.Info($"视频信息: {mediaInfo.Width}x{mediaInfo.Height}, 时长: {mediaInfo.Duration}, 帧率: {mediaInfo.FrameRate:F2}fps");
                
                // 预热引擎
                await PrewarmEngineAsync(request.SourcePath);
                
                // 设置并行选项
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = request.Options.MaxDegreeOfParallelism == -1 
                        ? Math.Max(1, Environment.ProcessorCount - 1)
                        : request.Options.MaxDegreeOfParallelism
                };
                
                _logger.Info($"并行度: {parallelOptions.MaxDegreeOfParallelism}");
                
                // 并行提取帧
                var frameInfos = new ExtractedFrameInfo[request.Timestamps.Count];
                int completedCount = 0;
                
                await Task.Run(() =>
                {
                    Parallel.For(0, request.Timestamps.Count, parallelOptions, i =>
                    {
                        var frameInfo = ExtractSingleFrameSync(request, i, mediaInfo);
                        frameInfos[i] = frameInfo;
                        
                        // 更新进度
                        var completed = Interlocked.Increment(ref completedCount);
                        ReportProgress(completed, request.Timestamps.Count, stopwatch.Elapsed, frameInfo.OutputPath);
                    });
                });
                
                // 收集结果
                result.ExtractedFrames.AddRange(frameInfos.Where(f => f != null));
                
                // 计算统计信息
                CalculateStatistics(result, stopwatch.Elapsed);
                
                _logger.Info($"帧提取完成: 成功 {result.Statistics.SuccessfulFrames}, 失败 {result.Statistics.FailedFrames}, 跳过 {result.Statistics.SkippedFrames}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"批量提取失败: {ex.Message}");
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
        /// 按时间间隔提取帧
        /// </summary>
        public async Task<FrameExtractionResult> ExtractFramesByIntervalAsync(string videoPath, string outputDirectory, TimeSpan interval, FrameExtractionOptions options = null)
        {
            options ??= new FrameExtractionOptions();
            
            // 获取视频信息
            var mediaInfo = await GetMediaInfoAsync(videoPath);
            
            // 生成时间戳列表
            var timestamps = new List<TimeSpan>();
            var currentTime = TimeSpan.Zero;
            
            while (currentTime < mediaInfo.Duration)
            {
                timestamps.Add(currentTime);
                currentTime = currentTime.Add(interval);
            }
            
            _logger.Info($"按间隔 {interval} 提取帧，共 {timestamps.Count} 帧");
            
            var request = new FrameExtractionRequest
            {
                SourcePath = videoPath,
                OutputDirectory = outputDirectory,
                Timestamps = timestamps,
                Options = options
            };
            
            return await ExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 按帧数提取帧
        /// </summary>
        public async Task<FrameExtractionResult> ExtractFramesByCountAsync(string videoPath, string outputDirectory, int frameCount, FrameExtractionOptions options = null)
        {
            options ??= new FrameExtractionOptions();
            
            // 获取视频信息
            var mediaInfo = await GetMediaInfoAsync(videoPath);
            
            // 计算时间间隔
            var interval = TimeSpan.FromMilliseconds(mediaInfo.Duration.TotalMilliseconds / frameCount);
            
            // 生成均匀分布的时间戳
            var timestamps = new List<TimeSpan>();
            for (int i = 0; i < frameCount; i++)
            {
                var timestamp = TimeSpan.FromMilliseconds(i * interval.TotalMilliseconds);
                if (timestamp < mediaInfo.Duration)
                {
                    timestamps.Add(timestamp);
                }
            }
            
            _logger.Info($"提取 {frameCount} 帧，实际生成 {timestamps.Count} 个时间戳");
            
            var request = new FrameExtractionRequest
            {
                SourcePath = videoPath,
                OutputDirectory = outputDirectory,
                Timestamps = timestamps,
                Options = options
            };
            
            return await ExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 预热引擎
        /// </summary>
        private async Task PrewarmEngineAsync(string videoPath)
        {
            _logger.Debug("预热FFmpeg引擎...");
            
            await Task.Run(() =>
            {
                try
                {
                    using var engine = new Engine();
                    var inputFile = new MediaFile { Filename = videoPath };
                    engine.GetMetadata(inputFile);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"引擎预热失败: {ex.Message}");
                }
            });
            
            _logger.Debug("引擎预热完成");
        }
        
        /// <summary>
        /// 同步提取单帧
        /// </summary>
        private ExtractedFrameInfo ExtractSingleFrameSync(FrameExtractionRequest request, int frameIndex, MediaInfo mediaInfo)
        {
            var timestamp = request.Timestamps[frameIndex];
            var frameStopwatch = Stopwatch.StartNew();
            
            var frameInfo = new ExtractedFrameInfo
            {
                FrameIndex = frameIndex,
                Timestamp = timestamp
            };
            
            try
            {
                // 生成输出文件名
                var fileName = string.Format(request.Options.FileNameTemplate, frameIndex + 1);
                var extension = GetFileExtension(request.Options.OutputFormat);
                frameInfo.OutputPath = Path.Combine(request.OutputDirectory, fileName + extension);
                
                // 检查是否跳过已存在的文件
                if (request.Options.SkipExisting && File.Exists(frameInfo.OutputPath))
                {
                    var existingFileInfo = new FileInfo(frameInfo.OutputPath);
                    frameInfo.FileSize = existingFileInfo.Length;
                    frameInfo.Success = true;
                    
                    // 尝试获取图片尺寸
                    try
                    {
                        using var img = Image.FromFile(frameInfo.OutputPath);
                        frameInfo.Width = img.Width;
                        frameInfo.Height = img.Height;
                    }
                    catch
                    {
                        frameInfo.Width = mediaInfo.Width;
                        frameInfo.Height = mediaInfo.Height;
                    }
                    
                    return frameInfo;
                }
                
                // 等待信号量
                _engineSemaphore.Wait();
                
                try
                {
                    using var engine = new Engine();
                    var inputFile = new MediaFile { Filename = request.SourcePath };
                    var outputFile = new MediaFile { Filename = frameInfo.OutputPath };
                    var options = new ConversionOptions { Seek = timestamp };
                    
                    // 设置输出质量
                    if (request.Options.OutputFormat == ImageFormat.Jpeg && request.Options.Quality < 100)
                    {
                        options.VideoQuality = request.Options.Quality;
                    }
                    
                    engine.GetThumbnail(inputFile, outputFile, options);
                    
                    // 后处理：调整尺寸
                    if (request.Options.MaxWidth > 0 || request.Options.MaxHeight > 0)
                    {
                        ResizeImage(frameInfo.OutputPath, request.Options);
                    }
                    
                    // 获取文件信息
                    var fileInfo = new FileInfo(frameInfo.OutputPath);
                    frameInfo.FileSize = fileInfo.Length;
                    
                    // 获取图片尺寸
                    using var img = Image.FromFile(frameInfo.OutputPath);
                    frameInfo.Width = img.Width;
                    frameInfo.Height = img.Height;
                    
                    frameInfo.Success = true;
                }
                finally
                {
                    _engineSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                frameInfo.Success = false;
                frameInfo.ErrorMessage = ex.Message;
                _logger.Warning($"提取帧失败 [{frameIndex}] {timestamp}: {ex.Message}");
                
                // 清理可能创建的损坏文件
                if (File.Exists(frameInfo.OutputPath))
                {
                    try { File.Delete(frameInfo.OutputPath); } catch { }
                }
            }
            finally
            {
                frameStopwatch.Stop();
                frameInfo.ExtractionTime = frameStopwatch.Elapsed;
            }
            
            return frameInfo;
        }
        
        /// <summary>
        /// 调整图片尺寸
        /// </summary>
        private void ResizeImage(string imagePath, FrameExtractionOptions options)
        {
            if (options.MaxWidth <= 0 && options.MaxHeight <= 0)
                return;
            
            try
            {
                using var original = new Bitmap(imagePath);
                var newSize = CalculateNewSize(original.Width, original.Height, options);
                
                if (newSize.Width == original.Width && newSize.Height == original.Height)
                    return;
                
                using var resized = new Bitmap(newSize.Width, newSize.Height);
                using var graphics = Graphics.FromImage(resized);
                
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                
                graphics.DrawImage(original, 0, 0, newSize.Width, newSize.Height);
                
                // 保存调整后的图片
                var codec = GetImageCodec(options.OutputFormat);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)options.Quality);
                
                resized.Save(imagePath, codec, encoderParams);
            }
            catch (Exception ex)
            {
                _logger.Warning($"调整图片尺寸失败: {imagePath}, {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算新尺寸
        /// </summary>
        private Size CalculateNewSize(int originalWidth, int originalHeight, FrameExtractionOptions options)
        {
            if (options.MaxWidth <= 0 && options.MaxHeight <= 0)
                return new Size(originalWidth, originalHeight);
            
            double scaleX = options.MaxWidth > 0 ? (double)options.MaxWidth / originalWidth : double.MaxValue;
            double scaleY = options.MaxHeight > 0 ? (double)options.MaxHeight / originalHeight : double.MaxValue;
            
            double scale = Math.Min(scaleX, scaleY);
            
            if (scale >= 1.0)
                return new Size(originalWidth, originalHeight);
            
            int newWidth = (int)(originalWidth * scale);
            int newHeight = (int)(originalHeight * scale);
            
            return new Size(newWidth, newHeight);
        }
        
        /// <summary>
        /// 报告进度
        /// </summary>
        private void ReportProgress(int completed, int total, TimeSpan elapsed, string currentFile)
        {
            if (ProgressReported == null) return;
            
            lock (_progressLock)
            {
                var progressPercentage = (int)((double)completed / total * 100);
                var averageTime = elapsed.TotalSeconds / completed;
                var remainingTime = TimeSpan.FromSeconds((total - completed) * averageTime);
                
                var args = new FrameExtractionProgressEventArgs
                {
                    ProgressPercentage = progressPercentage,
                    CurrentFrameIndex = completed,
                    TotalFrames = total,
                    CurrentFileName = Path.GetFileName(currentFile),
                    ElapsedTime = elapsed,
                    EstimatedRemainingTime = remainingTime
                };
                
                ProgressReported?.Invoke(this, args);
            }
        }
        
        /// <summary>
        /// 计算统计信息
        /// </summary>
        private void CalculateStatistics(FrameExtractionResult result, TimeSpan duration)
        {
            var stats = result.Statistics;
            
            stats.TotalFrames = result.ExtractedFrames.Count;
            stats.SuccessfulFrames = result.ExtractedFrames.Count(f => f.Success);
            stats.FailedFrames = result.ExtractedFrames.Count(f => !f.Success);
            stats.SkippedFrames = stats.TotalFrames - stats.SuccessfulFrames - stats.FailedFrames;
            stats.TotalOutputSize = result.ExtractedFrames.Where(f => f.Success).Sum(f => f.FileSize);
            
            if (duration.TotalSeconds > 0)
            {
                stats.AverageSpeed = stats.SuccessfulFrames / duration.TotalSeconds;
            }
        }
        
        /// <summary>
        /// 计算总帧数
        /// </summary>
        private int CalculateTotalFrames(TimeSpan duration, double frameRate)
        {
            if (frameRate <= 0) return 0;
            return (int)(duration.TotalSeconds * frameRate);
        }
        
        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        private string GetFileExtension(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Jpeg)) return ".jpg";
            if (format.Equals(ImageFormat.Png)) return ".png";
            if (format.Equals(ImageFormat.Bmp)) return ".bmp";
            if (format.Equals(ImageFormat.Gif)) return ".gif";
            if (format.Equals(ImageFormat.Tiff)) return ".tiff";
            return ".jpg";
        }
        
        /// <summary>
        /// 获取图片编码器
        /// </summary>
        private ImageCodecInfo GetImageCodec(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _engineSemaphore?.Dispose();
        }
    }
}