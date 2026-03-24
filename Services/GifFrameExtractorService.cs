using System;
using System.Collections.Generic;
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
    /// 高效GIF帧提取服务
    /// </summary>
    public class GifFrameExtractorService : IGifFrameExtractor
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _processingLock;
        private readonly object _progressLock = new object();
        
        /// <summary>
        /// 进度报告事件
        /// </summary>
        public event EventHandler<FrameExtractionProgressEventArgs> ProgressReported;
        
        public GifFrameExtractorService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // GIF处理通常是CPU密集型，限制并发数
            int maxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
            _processingLock = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }
        
        /// <summary>
        /// 检查是否支持该文件格式
        /// </summary>
        public bool IsSupported(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".gif";
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
                    using var gif = Image.FromFile(sourcePath);
                    var fileInfo = new FileInfo(sourcePath);
                    
                    // 获取帧数
                    var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                    int frameCount = gif.GetFrameCount(dimension);
                    
                    // 计算总时长（基于帧延迟）
                    var totalDuration = CalculateGifDuration(gif);
                    
                    return new MediaInfo
                    {
                        FilePath = sourcePath,
                        Type = MediaType.Gif,
                        Duration = totalDuration,
                        Width = gif.Width,
                        Height = gif.Height,
                        FrameRate = frameCount / totalDuration.TotalSeconds,
                        TotalFrames = frameCount,
                        FileSize = fileInfo.Length,
                        Codec = "GIF",
                        Bitrate = 0
                    };
                }
                catch (Exception ex)
                {
                    _logger.Error($"获取GIF信息失败: {sourcePath}", ex);
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
            
            await _processingLock.WaitAsync();
            
            try
            {
                return await Task.Run(() =>
                {
                    using var gif = Image.FromFile(sourcePath);
                    var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                    int frameCount = gif.GetFrameCount(dimension);
                    
                    // 根据时间戳计算帧索引
                    var mediaInfo = GetMediaInfoAsync(sourcePath).Result;
                    int frameIndex = (int)(timestamp.TotalMilliseconds / mediaInfo.Duration.TotalMilliseconds * frameCount);
                    frameIndex = Math.Max(0, Math.Min(frameIndex, frameCount - 1));
                    
                    return ExtractFrameByIndexSync(gif, frameIndex);
                });
            }
            finally
            {
                _processingLock.Release();
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
                _logger.Info($"开始批量提取GIF帧: {request.SourcePath}");
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
                _logger.Info($"GIF信息: {mediaInfo.Width}x{mediaInfo.Height}, 帧数: {mediaInfo.TotalFrames}, 时长: {mediaInfo.Duration}");
                
                // 使用内存优化模式处理大GIF
                if (request.Options.MemoryOptimized || mediaInfo.FileSize > 50 * 1024 * 1024) // 50MB
                {
                    result = await ExtractFramesMemoryOptimized(request, mediaInfo);
                }
                else
                {
                    result = await ExtractFramesStandard(request, mediaInfo);
                }
                
                // 计算统计信息
                CalculateStatistics(result, stopwatch.Elapsed);
                
                _logger.Info($"GIF帧提取完成: 成功 {result.Statistics.SuccessfulFrames}, 失败 {result.Statistics.FailedFrames}, 跳过 {result.Statistics.SkippedFrames}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"批量提取失败: {ex.Message}");
                _logger.Error("批量GIF帧提取失败", ex);
            }
            finally
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
            }
            
            return result;
        }
        
        /// <summary>
        /// 提取所有帧
        /// </summary>
        public async Task<FrameExtractionResult> ExtractAllFramesAsync(string gifPath, string outputDirectory, FrameExtractionOptions options = null)
        {
            options ??= new FrameExtractionOptions();
            
            // 获取GIF信息
            var mediaInfo = await GetMediaInfoAsync(gifPath);
            
            // 生成所有帧的时间戳
            var timestamps = new List<TimeSpan>();
            var frameDuration = mediaInfo.Duration.TotalMilliseconds / mediaInfo.TotalFrames;
            
            for (int i = 0; i < mediaInfo.TotalFrames; i++)
            {
                timestamps.Add(TimeSpan.FromMilliseconds(i * frameDuration));
            }
            
            _logger.Info($"提取GIF所有帧，共 {timestamps.Count} 帧");
            
            var request = new FrameExtractionRequest
            {
                SourcePath = gifPath,
                OutputDirectory = outputDirectory,
                Timestamps = timestamps,
                Options = options
            };
            
            return await ExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 提取指定帧
        /// </summary>
        public async Task<Bitmap> ExtractFrameByIndexAsync(string gifPath, int frameIndex)
        {
            if (!IsSupported(gifPath))
                throw new NotSupportedException($"不支持的文件格式: {gifPath}");
            
            await _processingLock.WaitAsync();
            
            try
            {
                return await Task.Run(() =>
                {
                    using var gif = Image.FromFile(gifPath);
                    return ExtractFrameByIndexSync(gif, frameIndex);
                });
            }
            finally
            {
                _processingLock.Release();
            }
        }
        
        /// <summary>
        /// 标准模式提取帧
        /// </summary>
        private async Task<FrameExtractionResult> ExtractFramesStandard(FrameExtractionRequest request, MediaInfo mediaInfo)
        {
            var result = new FrameExtractionResult { Success = true };
            
            await Task.Run(() =>
            {
                using var gif = Image.FromFile(request.SourcePath);
                var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = request.Options.MaxDegreeOfParallelism == -1 
                        ? Math.Max(1, Environment.ProcessorCount / 2)
                        : request.Options.MaxDegreeOfParallelism
                };
                
                var frameInfos = new ExtractedFrameInfo[request.Timestamps.Count];
                int completedCount = 0;
                
                Parallel.For(0, request.Timestamps.Count, parallelOptions, i =>
                {
                    var frameInfo = ExtractSingleGifFrame(gif, request, i, mediaInfo);
                    frameInfos[i] = frameInfo;
                    
                    // 更新进度
                    var completed = Interlocked.Increment(ref completedCount);
                    ReportProgress(completed, request.Timestamps.Count, DateTime.Now - result.StartTime, frameInfo?.OutputPath);
                });
                
                result.ExtractedFrames.AddRange(frameInfos.Where(f => f != null));
            });
            
            return result;
        }
        
        /// <summary>
        /// 内存优化模式提取帧
        /// </summary>
        private async Task<FrameExtractionResult> ExtractFramesMemoryOptimized(FrameExtractionRequest request, MediaInfo mediaInfo)
        {
            var result = new FrameExtractionResult { Success = true };
            
            _logger.Info("使用内存优化模式处理大GIF文件");
            
            // 分批处理，避免内存溢出
            const int batchSize = 10;
            var batches = request.Timestamps
                .Select((timestamp, index) => new { timestamp, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.ToList())
                .ToList();
            
            int totalCompleted = 0;
            
            foreach (var batch in batches)
            {
                await Task.Run(() =>
                {
                    using var gif = Image.FromFile(request.SourcePath);
                    
                    foreach (var item in batch)
                    {
                        var frameInfo = ExtractSingleGifFrame(gif, request, item.index, mediaInfo);
                        if (frameInfo != null)
                        {
                            result.ExtractedFrames.Add(frameInfo);
                        }
                        
                        totalCompleted++;
                        ReportProgress(totalCompleted, request.Timestamps.Count, DateTime.Now - result.StartTime, frameInfo?.OutputPath);
                    }
                });
                
                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            
            return result;
        }
        
        /// <summary>
        /// 提取单个GIF帧
        /// </summary>
        private ExtractedFrameInfo ExtractSingleGifFrame(Image gif, FrameExtractionRequest request, int timestampIndex, MediaInfo mediaInfo)
        {
            var timestamp = request.Timestamps[timestampIndex];
            var frameStopwatch = Stopwatch.StartNew();
            
            var frameInfo = new ExtractedFrameInfo
            {
                FrameIndex = timestampIndex,
                Timestamp = timestamp
            };
            
            try
            {
                // 生成输出文件名
                var fileName = string.Format(request.Options.FileNameTemplate, timestampIndex + 1);
                var extension = GetFileExtension(request.Options.OutputFormat);
                frameInfo.OutputPath = Path.Combine(request.OutputDirectory, fileName + extension);
                
                // 检查是否跳过已存在的文件
                if (request.Options.SkipExisting && File.Exists(frameInfo.OutputPath))
                {
                    var existingFileInfo = new FileInfo(frameInfo.OutputPath);
                    frameInfo.FileSize = existingFileInfo.Length;
                    frameInfo.Success = true;
                    
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
                
                // 计算帧索引
                var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                int frameCount = gif.GetFrameCount(dimension);
                int frameIndex = (int)(timestamp.TotalMilliseconds / mediaInfo.Duration.TotalMilliseconds * frameCount);
                frameIndex = Math.Max(0, Math.Min(frameIndex, frameCount - 1));
                
                // 提取帧
                using var frameBitmap = ExtractFrameByIndexSync(gif, frameIndex);
                
                // 调整尺寸
                Bitmap finalBitmap = frameBitmap;
                if (request.Options.MaxWidth > 0 || request.Options.MaxHeight > 0)
                {
                    var newSize = CalculateNewSize(frameBitmap.Width, frameBitmap.Height, request.Options);
                    if (newSize.Width != frameBitmap.Width || newSize.Height != frameBitmap.Height)
                    {
                        finalBitmap = ResizeBitmap(frameBitmap, newSize);
                    }
                }
                
                // 保存图片
                SaveBitmap(finalBitmap, frameInfo.OutputPath, request.Options);
                
                // 获取文件信息
                var fileInfo = new FileInfo(frameInfo.OutputPath);
                frameInfo.FileSize = fileInfo.Length;
                frameInfo.Width = finalBitmap.Width;
                frameInfo.Height = finalBitmap.Height;
                frameInfo.Success = true;
                
                // 清理资源
                if (finalBitmap != frameBitmap)
                {
                    finalBitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                frameInfo.Success = false;
                frameInfo.ErrorMessage = ex.Message;
                _logger.Warning($"提取GIF帧失败 [{timestampIndex}] {timestamp}: {ex.Message}");
                
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
        /// 同步提取指定索引的帧
        /// </summary>
        private Bitmap ExtractFrameByIndexSync(Image gif, int frameIndex)
        {
            var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);
            
            if (frameIndex < 0 || frameIndex >= frameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex), $"帧索引超出范围: {frameIndex}, 总帧数: {frameCount}");
            
            // 选择指定帧
            gif.SelectActiveFrame(dimension, frameIndex);
            
            // 创建帧的副本
            return new Bitmap(gif);
        }
        
        /// <summary>
        /// 计算GIF总时长
        /// </summary>
        private TimeSpan CalculateGifDuration(Image gif)
        {
            try
            {
                var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                int frameCount = gif.GetFrameCount(dimension);
                
                // 获取帧延迟属性
                var delays = gif.GetPropertyItem(0x5100)?.Value;
                if (delays == null) return TimeSpan.FromSeconds(frameCount * 0.1); // 默认100ms每帧
                
                int totalDelay = 0;
                for (int i = 0; i < frameCount; i++)
                {
                    int delay = BitConverter.ToInt32(delays, i * 4) * 10; // 转换为毫秒
                    totalDelay += Math.Max(delay, 100); // 最小100ms
                }
                
                return TimeSpan.FromMilliseconds(totalDelay);
            }
            catch
            {
                // 如果无法获取延迟信息，使用默认值
                var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                int frameCount = gif.GetFrameCount(dimension);
                return TimeSpan.FromSeconds(frameCount * 0.1);
            }
        }
        
        /// <summary>
        /// 调整位图尺寸
        /// </summary>
        private Bitmap ResizeBitmap(Bitmap original, Size newSize)
        {
            var resized = new Bitmap(newSize.Width, newSize.Height);
            using var graphics = Graphics.FromImage(resized);
            
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            
            graphics.DrawImage(original, 0, 0, newSize.Width, newSize.Height);
            
            return resized;
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
        /// 保存位图
        /// </summary>
        private void SaveBitmap(Bitmap bitmap, string outputPath, FrameExtractionOptions options)
        {
            var codec = GetImageCodec(options.OutputFormat);
            if (codec != null && options.OutputFormat.Equals(ImageFormat.Jpeg))
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)options.Quality);
                bitmap.Save(outputPath, codec, encoderParams);
            }
            else
            {
                bitmap.Save(outputPath, options.OutputFormat);
            }
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
            _processingLock?.Dispose();
        }
    }
}