using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;

namespace PutPicture.Services
{
    /// <summary>
    /// 优化的帧提取服务 - 统一管理视频和GIF帧提取
    /// </summary>
    public class OptimizedFrameExtractorService : IFrameExtractor
    {
        private readonly IVideoFrameExtractor _videoExtractor;
        private readonly IGifFrameExtractor _gifExtractor;
        private readonly ILogger _logger;
        private readonly FrameCache _frameCache;
        private readonly PerformanceMonitor _performanceMonitor;
        
        /// <summary>
        /// 进度报告事件
        /// </summary>
        public event EventHandler<FrameExtractionProgressEventArgs> ProgressReported;
        
        public OptimizedFrameExtractorService(
            IVideoFrameExtractor videoExtractor,
            IGifFrameExtractor gifExtractor,
            ILogger logger)
        {
            _videoExtractor = videoExtractor ?? throw new ArgumentNullException(nameof(videoExtractor));
            _gifExtractor = gifExtractor ?? throw new ArgumentNullException(nameof(gifExtractor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _frameCache = new FrameCache(logger);
            _performanceMonitor = new PerformanceMonitor(logger);
            
            // 订阅子服务的进度事件
            if (_videoExtractor is VideoFrameExtractorService videoService)
            {
                videoService.ProgressReported += OnProgressReported;
            }
            if (_gifExtractor is GifFrameExtractorService gifService)
            {
                gifService.ProgressReported += OnProgressReported;
            }
        }
        
        /// <summary>
        /// 检查是否支持该文件格式
        /// </summary>
        public bool IsSupported(string filePath)
        {
            return _videoExtractor.IsSupported(filePath) || _gifExtractor.IsSupported(filePath);
        }
        
        /// <summary>
        /// 获取媒体信息
        /// </summary>
        public async Task<MediaInfo> GetMediaInfoAsync(string sourcePath)
        {
            if (_videoExtractor.IsSupported(sourcePath))
            {
                return await _videoExtractor.GetMediaInfoAsync(sourcePath);
            }
            else if (_gifExtractor.IsSupported(sourcePath))
            {
                return await _gifExtractor.GetMediaInfoAsync(sourcePath);
            }
            else
            {
                throw new NotSupportedException($"不支持的文件格式: {sourcePath}");
            }
        }
        
        /// <summary>
        /// 提取单帧（带缓存）
        /// </summary>
        public async Task<Bitmap> ExtractFrameAsync(string sourcePath, TimeSpan timestamp)
        {
            // 检查缓存
            var cacheKey = $"{sourcePath}_{timestamp.TotalMilliseconds}";
            var cachedFrame = _frameCache.GetFrame(cacheKey);
            if (cachedFrame != null)
            {
                _logger.Debug($"从缓存获取帧: {sourcePath} @ {timestamp}");
                return new Bitmap(cachedFrame);
            }
            
            // 提取帧
            Bitmap frame;
            if (_videoExtractor.IsSupported(sourcePath))
            {
                frame = await _videoExtractor.ExtractFrameAsync(sourcePath, timestamp);
            }
            else if (_gifExtractor.IsSupported(sourcePath))
            {
                frame = await _gifExtractor.ExtractFrameAsync(sourcePath, timestamp);
            }
            else
            {
                throw new NotSupportedException($"不支持的文件格式: {sourcePath}");
            }
            
            // 缓存帧
            _frameCache.CacheFrame(cacheKey, frame);
            
            return frame;
        }
        
        /// <summary>
        /// 批量提取帧（优化版）
        /// </summary>
        public async Task<FrameExtractionResult> ExtractFramesAsync(FrameExtractionRequest request)
        {
            _performanceMonitor.StartMonitoring();
            
            try
            {
                // 预处理：优化请求参数
                var optimizedRequest = OptimizeRequest(request);
                
                // 根据文件类型选择提取器
                FrameExtractionResult result;
                if (_videoExtractor.IsSupported(request.SourcePath))
                {
                    result = await ExtractVideoFramesOptimized(optimizedRequest);
                }
                else if (_gifExtractor.IsSupported(request.SourcePath))
                {
                    result = await ExtractGifFramesOptimized(optimizedRequest);
                }
                else
                {
                    throw new NotSupportedException($"不支持的文件格式: {request.SourcePath}");
                }
                
                // 后处理：优化结果
                await PostProcessResult(result, optimizedRequest);
                
                return result;
            }
            finally
            {
                _performanceMonitor.StopMonitoring();
            }
        }
        
        /// <summary>
        /// 智能批量提取 - 自动选择最优策略
        /// </summary>
        public async Task<FrameExtractionResult> SmartExtractFramesAsync(FrameExtractionRequest request)
        {
            _logger.Info("开始智能帧提取分析...");
            
            // 获取媒体信息
            var mediaInfo = await GetMediaInfoAsync(request.SourcePath);
            
            // 分析最优策略
            var strategy = AnalyzeOptimalStrategy(mediaInfo, request);
            _logger.Info($"选择策略: {strategy.Name}, 预计性能提升: {strategy.PerformanceGain:P0}");
            
            // 应用策略
            var optimizedRequest = ApplyStrategy(request, strategy);
            
            // 执行提取
            return await ExtractFramesAsync(optimizedRequest);
        }
        
        /// <summary>
        /// 优化请求参数
        /// </summary>
        private FrameExtractionRequest OptimizeRequest(FrameExtractionRequest request)
        {
            var optimized = new FrameExtractionRequest
            {
                SourcePath = request.SourcePath,
                OutputDirectory = request.OutputDirectory,
                Timestamps = request.Timestamps,
                Options = new FrameExtractionOptions
                {
                    OutputFormat = request.Options.OutputFormat,
                    Quality = request.Options.Quality,
                    MaxDegreeOfParallelism = OptimizeParallelism(request),
                    FileNameTemplate = request.Options.FileNameTemplate,
                    SkipExisting = request.Options.SkipExisting,
                    MaxWidth = request.Options.MaxWidth,
                    MaxHeight = request.Options.MaxHeight,
                    MaintainAspectRatio = request.Options.MaintainAspectRatio,
                    EnableProgressReporting = request.Options.EnableProgressReporting,
                    MemoryOptimized = ShouldUseMemoryOptimization(request)
                }
            };
            
            return optimized;
        }
        
        /// <summary>
        /// 优化并行度
        /// </summary>
        private int OptimizeParallelism(FrameExtractionRequest request)
        {
            if (request.Options.MaxDegreeOfParallelism > 0)
                return request.Options.MaxDegreeOfParallelism;
            
            // 根据文件类型和系统资源动态调整
            var fileInfo = new FileInfo(request.SourcePath);
            var fileSize = fileInfo.Length;
            var availableMemory = GC.GetTotalMemory(false);
            var cpuCores = Environment.ProcessorCount;
            
            if (_gifExtractor.IsSupported(request.SourcePath))
            {
                // GIF文件通常较小，但处理更CPU密集
                return Math.Max(1, cpuCores / 2);
            }
            else
            {
                // 视频文件根据大小调整
                if (fileSize > 500 * 1024 * 1024) // 500MB+
                {
                    return Math.Max(1, cpuCores / 4);
                }
                else if (fileSize > 100 * 1024 * 1024) // 100MB+
                {
                    return Math.Max(1, cpuCores / 2);
                }
                else
                {
                    return Math.Max(1, cpuCores - 1);
                }
            }
        }
        
        /// <summary>
        /// 判断是否应该使用内存优化
        /// </summary>
        private bool ShouldUseMemoryOptimization(FrameExtractionRequest request)
        {
            if (request.Options.MemoryOptimized)
                return true;
            
            var fileInfo = new FileInfo(request.SourcePath);
            var fileSize = fileInfo.Length;
            var frameCount = request.Timestamps.Count;
            
            // 大文件或大量帧时启用内存优化
            return fileSize > 100 * 1024 * 1024 || frameCount > 100;
        }
        
        /// <summary>
        /// 优化的视频帧提取
        /// </summary>
        private async Task<FrameExtractionResult> ExtractVideoFramesOptimized(FrameExtractionRequest request)
        {
            _logger.Info("使用优化的视频帧提取");
            
            // 预排序时间戳以优化磁盘访问
            var sortedTimestamps = request.Timestamps
                .Select((ts, index) => new { Timestamp = ts, OriginalIndex = index })
                .OrderBy(x => x.Timestamp)
                .ToList();
            
            var sortedRequest = new FrameExtractionRequest
            {
                SourcePath = request.SourcePath,
                OutputDirectory = request.OutputDirectory,
                Timestamps = sortedTimestamps.Select(x => x.Timestamp).ToList(),
                Options = request.Options
            };
            
            var result = await _videoExtractor.ExtractFramesAsync(sortedRequest);
            
            // 恢复原始顺序
            var reorderedFrames = new ExtractedFrameInfo[request.Timestamps.Count];
            for (int i = 0; i < sortedTimestamps.Count; i++)
            {
                var originalIndex = sortedTimestamps[i].OriginalIndex;
                if (i < result.ExtractedFrames.Count)
                {
                    reorderedFrames[originalIndex] = result.ExtractedFrames[i];
                }
            }
            
            result.ExtractedFrames = reorderedFrames.Where(f => f != null).ToList();
            
            return result;
        }
        
        /// <summary>
        /// 优化的GIF帧提取
        /// </summary>
        private async Task<FrameExtractionResult> ExtractGifFramesOptimized(FrameExtractionRequest request)
        {
            _logger.Info("使用优化的GIF帧提取");
            
            // GIF帧提取通常按顺序进行更高效
            return await _gifExtractor.ExtractFramesAsync(request);
        }
        
        /// <summary>
        /// 后处理结果
        /// </summary>
        private async Task PostProcessResult(FrameExtractionResult result, FrameExtractionRequest request)
        {
            if (!result.Success) return;
            
            // 并行优化输出文件
            if (request.Options.Quality < 100 || request.Options.MaxWidth > 0 || request.Options.MaxHeight > 0)
            {
                await OptimizeOutputFiles(result.ExtractedFrames.Where(f => f.Success).ToList(), request.Options);
            }
            
            // 生成缩略图索引（可选）
            if (result.ExtractedFrames.Count > 10)
            {
                await GenerateThumbnailIndex(result, request);
            }
        }
        
        /// <summary>
        /// 优化输出文件
        /// </summary>
        private async Task OptimizeOutputFiles(List<ExtractedFrameInfo> frames, FrameExtractionOptions options)
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(frames, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, frame =>
                {
                    try
                    {
                        OptimizeSingleFile(frame.OutputPath, options);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"优化文件失败: {frame.OutputPath}, {ex.Message}");
                    }
                });
            });
        }
        
        /// <summary>
        /// 优化单个文件
        /// </summary>
        private void OptimizeSingleFile(string filePath, FrameExtractionOptions options)
        {
            // 这里可以添加更多优化逻辑，如：
            // - 无损压缩
            // - 格式转换
            // - 元数据清理
            // - 色彩优化等
        }
        
        /// <summary>
        /// 生成缩略图索引
        /// </summary>
        private async Task GenerateThumbnailIndex(FrameExtractionResult result, FrameExtractionRequest request)
        {
            try
            {
                var indexPath = Path.Combine(request.OutputDirectory, "index.html");
                await GenerateHtmlIndex(result.ExtractedFrames, indexPath);
                _logger.Info($"生成缩略图索引: {indexPath}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"生成索引失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 生成HTML索引
        /// </summary>
        private async Task GenerateHtmlIndex(List<ExtractedFrameInfo> frames, string indexPath)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>帧提取结果</title>
    <style>
        .frame {{ display: inline-block; margin: 10px; text-align: center; }}
        .frame img {{ max-width: 200px; max-height: 150px; border: 1px solid #ccc; }}
        .frame-info {{ font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <h1>帧提取结果 ({frames.Count(f => f.Success)} 帧)</h1>
    <div>
        {string.Join("\n", frames.Where(f => f.Success).Select(f => $@"
        <div class='frame'>
            <img src='{Path.GetFileName(f.OutputPath)}' alt='Frame {f.FrameIndex}' />
            <div class='frame-info'>
                帧 {f.FrameIndex}<br/>
                时间: {f.Timestamp}<br/>
                大小: {f.FileSize / 1024:F1} KB
            </div>
        </div>"))}
    </div>
</body>
</html>";
            
            await File.WriteAllTextAsync(indexPath, html);
        }
        
        /// <summary>
        /// 分析最优策略
        /// </summary>
        private ExtractionStrategy AnalyzeOptimalStrategy(MediaInfo mediaInfo, FrameExtractionRequest request)
        {
            var strategies = new List<ExtractionStrategy>
            {
                new ExtractionStrategy
                {
                    Name = "标准并行",
                    PerformanceGain = 0.0,
                    MemoryUsage = 1.0,
                    Parallelism = Environment.ProcessorCount - 1
                },
                new ExtractionStrategy
                {
                    Name = "内存优化",
                    PerformanceGain = -0.2,
                    MemoryUsage = 0.5,
                    Parallelism = Math.Max(1, Environment.ProcessorCount / 2),
                    UseMemoryOptimization = true
                },
                new ExtractionStrategy
                {
                    Name = "高并发",
                    PerformanceGain = 0.3,
                    MemoryUsage = 1.5,
                    Parallelism = Environment.ProcessorCount * 2
                }
            };
            
            // 根据文件特征选择策略
            if (mediaInfo.FileSize > 500 * 1024 * 1024) // 大文件
            {
                return strategies.First(s => s.Name == "内存优化");
            }
            else if (request.Timestamps.Count > 100) // 大量帧
            {
                return strategies.First(s => s.Name == "高并发");
            }
            else
            {
                return strategies.First(s => s.Name == "标准并行");
            }
        }
        
        /// <summary>
        /// 应用策略
        /// </summary>
        private FrameExtractionRequest ApplyStrategy(FrameExtractionRequest request, ExtractionStrategy strategy)
        {
            var optimized = new FrameExtractionRequest
            {
                SourcePath = request.SourcePath,
                OutputDirectory = request.OutputDirectory,
                Timestamps = request.Timestamps,
                Options = new FrameExtractionOptions
                {
                    OutputFormat = request.Options.OutputFormat,
                    Quality = request.Options.Quality,
                    MaxDegreeOfParallelism = strategy.Parallelism,
                    FileNameTemplate = request.Options.FileNameTemplate,
                    SkipExisting = request.Options.SkipExisting,
                    MaxWidth = request.Options.MaxWidth,
                    MaxHeight = request.Options.MaxHeight,
                    MaintainAspectRatio = request.Options.MaintainAspectRatio,
                    EnableProgressReporting = request.Options.EnableProgressReporting,
                    MemoryOptimized = strategy.UseMemoryOptimization
                }
            };
            
            return optimized;
        }
        
        /// <summary>
        /// 进度事件转发
        /// </summary>
        private void OnProgressReported(object sender, FrameExtractionProgressEventArgs e)
        {
            ProgressReported?.Invoke(this, e);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _frameCache?.Dispose();
            _performanceMonitor?.Dispose();
            
            if (_videoExtractor is IDisposable videoDisposable)
                videoDisposable.Dispose();
            if (_gifExtractor is IDisposable gifDisposable)
                gifDisposable.Dispose();
        }
    }
    
    /// <summary>
    /// 提取策略
    /// </summary>
    public class ExtractionStrategy
    {
        public string Name { get; set; }
        public double PerformanceGain { get; set; }
        public double MemoryUsage { get; set; }
        public int Parallelism { get; set; }
        public bool UseMemoryOptimization { get; set; }
    }
    
    /// <summary>
    /// 帧缓存
    /// </summary>
    public class FrameCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly Timer _cleanupTimer;
        private readonly ILogger _logger;
        private readonly long _maxCacheSize;
        private long _currentCacheSize;
        
        public FrameCache(ILogger logger, long maxCacheSizeBytes = 100 * 1024 * 1024) // 100MB
        {
            _logger = logger;
            _maxCacheSize = maxCacheSizeBytes;
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _cleanupTimer = new Timer(CleanupCache, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
        
        public Bitmap GetFrame(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                item.LastAccessed = DateTime.Now;
                return item.Frame;
            }
            return null;
        }
        
        public void CacheFrame(string key, Bitmap frame)
        {
            if (_currentCacheSize > _maxCacheSize)
            {
                CleanupCache(null);
            }
            
            var frameSize = EstimateFrameSize(frame);
            var item = new CacheItem
            {
                Frame = new Bitmap(frame),
                Size = frameSize,
                LastAccessed = DateTime.Now
            };
            
            _cache.TryAdd(key, item);
            Interlocked.Add(ref _currentCacheSize, frameSize);
        }
        
        private void CleanupCache(object state)
        {
            if (_currentCacheSize <= _maxCacheSize * 0.8) return;
            
            var itemsToRemove = _cache.Values
                .OrderBy(item => item.LastAccessed)
                .Take(_cache.Count / 4)
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                var keyToRemove = _cache.FirstOrDefault(kvp => kvp.Value == item).Key;
                if (keyToRemove != null && _cache.TryRemove(keyToRemove, out var removedItem))
                {
                    removedItem.Frame?.Dispose();
                    Interlocked.Add(ref _currentCacheSize, -removedItem.Size);
                }
            }
            
            _logger.Debug($"缓存清理完成，当前大小: {_currentCacheSize / 1024 / 1024}MB");
        }
        
        private long EstimateFrameSize(Bitmap frame)
        {
            return frame.Width * frame.Height * 4; // 假设32位ARGB
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            
            foreach (var item in _cache.Values)
            {
                item.Frame?.Dispose();
            }
            _cache.Clear();
        }
        
        private class CacheItem
        {
            public Bitmap Frame { get; set; }
            public long Size { get; set; }
            public DateTime LastAccessed { get; set; }
        }
    }
    
    /// <summary>
    /// 性能监控器
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly ILogger _logger;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Timer _monitorTimer;
        private bool _isMonitoring;
        
        public PerformanceMonitor(ILogger logger)
        {
            _logger = logger;
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.Warning($"性能计数器初始化失败: {ex.Message}");
            }
        }
        
        public void StartMonitoring()
        {
            if (_isMonitoring) return;
            
            _isMonitoring = true;
            _monitorTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }
        
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        private void MonitorPerformance(object state)
        {
            if (!_isMonitoring) return;
            
            try
            {
                var cpuUsage = _cpuCounter?.NextValue() ?? 0;
                var availableMemory = _memoryCounter?.NextValue() ?? 0;
                
                _logger.Debug($"性能监控 - CPU: {cpuUsage:F1}%, 可用内存: {availableMemory:F0}MB");
                
                // 性能告警
                if (cpuUsage > 90)
                {
                    _logger.Warning($"CPU使用率过高: {cpuUsage:F1}%");
                }
                
                if (availableMemory < 500)
                {
                    _logger.Warning($"可用内存不足: {availableMemory:F0}MB");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"性能监控异常: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            _monitorTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
        }
    }
}