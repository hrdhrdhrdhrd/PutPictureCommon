using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;

public class VideoSnapshot
{
    // 并发控制信号量
    private static readonly SemaphoreSlim _throttler = new SemaphoreSlim(
        Environment.ProcessorCount, 
        Environment.ProcessorCount * 2
    );

    public static async Task ExtractMultipleSnapshots(string videoFilePath, string outputDirectory, List<TimeSpan> shotTimes)
    {
        // 检查视频文件是否存在
        if (!File.Exists(videoFilePath))
        {
            Console.WriteLine("视频文件不存在！");
            return;
        }

        // 确保输出目录存在
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        Console.WriteLine("开始预初始化 Engine...");
        
        // 预热：先获取视频元数据并完成FFmpeg初始化
        await Task.Run(() =>
        {
            using var engine = new Engine();
            var inputFile = new MediaFile { Filename = videoFilePath };
            engine.GetMetadata(inputFile);
        });
        
        Console.WriteLine($"Engine 预初始化完成，开始截图。并发限制：{Environment.ProcessorCount} 个任务");
        Console.WriteLine($"总共需要截图 {shotTimes.Count} 张");

        var tasks = new List<Task>();
        int index = 1;
        int completedCount = 0;
        object lockObj = new object();

        foreach (TimeSpan shotTime in shotTimes)
        {
            // 等待信号量，控制并发数
            await _throttler.WaitAsync();
            
            string outputImagePath = Path.Combine(outputDirectory, $"snapshot_{index:000}.jpg");
            
            var task = ExtractSingleSnapshot(videoFilePath, outputImagePath, shotTime, index)
                .ContinueWith(t =>
                {
                    // 无论成功与否，都要释放信号量
                    _throttler.Release();
                    
                    lock (lockObj)
                    {
                        completedCount++;
                        if (completedCount % 10 == 0 || completedCount == shotTimes.Count)
                        {
                            Console.WriteLine($"进度：{completedCount}/{shotTimes.Count} ({((double)completedCount / shotTimes.Count * 100):F1}%)");
                        }
                    }
                });
            
            tasks.Add(task);
            index++;
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);
        Console.WriteLine("所有截图任务完成。");
    }

    private static async Task ExtractSingleSnapshot(string videoFilePath, string outputImagePath, TimeSpan shotTime, int index)
    {
        await Task.Run(() =>
        {
            var inputFile = new MediaFile { Filename = videoFilePath };
            var outputFile = new MediaFile { Filename = outputImagePath };
            var conversionOptions = new ConversionOptions { Seek = shotTime };

            try
            {
                using (var engine = new Engine())
                {
                    engine.GetThumbnail(inputFile, outputFile, conversionOptions);
                    Console.WriteLine($"成功截取 #{index:000}: {Path.GetFileName(outputImagePath)} (时间: {shotTime})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"在时间点 {shotTime} 截图失败：{ex.Message}");
                
                // 如果输出文件创建失败但文件存在，尝试删除损坏的文件
                if (File.Exists(outputImagePath))
                {
                    try
                    {
                        File.Delete(outputImagePath);
                    }
                    catch
                    {
                        // 忽略删除异常
                    }
                }
            }
        });
    }

    public static async Task ExtractSnapshotsAtIntervals(string videoFilePath, string outputDirectory, TimeSpan interval)
    {
        if (!File.Exists(videoFilePath))
        {
            Console.WriteLine("视频文件不存在！");
            return;
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var inputFile = new MediaFile { Filename = videoFilePath };
        
        // 获取视频总时长
        using (var engine = new Engine())
        {
            engine.GetMetadata(inputFile);
            TimeSpan videoDuration = inputFile.Metadata.Duration;

            Console.WriteLine($"视频总时长：{videoDuration}");

            List<TimeSpan> shotTimes = new List<TimeSpan>();
            TimeSpan currentTime = TimeSpan.Zero;

            // 生成从0开始，按固定间隔的时间点列表
            while (currentTime < videoDuration)
            {
                shotTimes.Add(currentTime);
                currentTime = currentTime.Add(interval);
            }

            Console.WriteLine($"将生成 {shotTimes.Count} 张截图，间隔：{interval}");

            // 使用并发方法进行截图
            await ExtractMultipleSnapshots(videoFilePath, outputDirectory, shotTimes);
        }
    }

    // 新增方法：在特定时间点截图（单张）
    public static async Task ExtractSingleSnapshotAsync(string videoFilePath, string outputImagePath, TimeSpan shotTime)
    {
        if (!File.Exists(videoFilePath))
        {
            Console.WriteLine("视频文件不存在！");
            return;
        }

        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputImagePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 等待信号量
        await _throttler.WaitAsync();
        
        try
        {
            // 预初始化
            await Task.Run(() => 
            {
                using var engine = new Engine();
                var inputFile = new MediaFile { Filename = videoFilePath };
                engine.GetMetadata(inputFile);
            });

            await ExtractSingleSnapshot(videoFilePath, outputImagePath, shotTime, 1);
        }
        finally
        {
            _throttler.Release();
        }
    }

    // 新增方法：批量截图带自定义并发限制
    public static async Task ExtractMultipleSnapshotsWithCustomConcurrency(
        string videoFilePath, 
        string outputDirectory, 
        List<TimeSpan> shotTimes, 
        int maxConcurrency)
    {
        // 创建自定义信号量
        using var customThrottler = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        if (!File.Exists(videoFilePath))
        {
            Console.WriteLine("视频文件不存在！");
            return;
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        Console.WriteLine("开始预初始化 Engine...");
        
        // 预热
        await Task.Run(() =>
        {
            using var engine = new Engine();
            var inputFile = new MediaFile { Filename = videoFilePath };
            engine.GetMetadata(inputFile);
        });
        
        Console.WriteLine($"Engine 预初始化完成，开始截图。自定义并发限制：{maxConcurrency} 个任务");
        Console.WriteLine($"总共需要截图 {shotTimes.Count} 张");

        var tasks = new List<Task>();
        int index = 1;
        int completedCount = 0;
        object lockObj = new object();

        foreach (TimeSpan shotTime in shotTimes)
        {
            // 等待自定义信号量
            await customThrottler.WaitAsync();
            
            string outputImagePath = Path.Combine(outputDirectory, $"snapshot_{index:000}.jpg");
            
            var task = Task.Run(() =>
            {
                var inputFile = new MediaFile { Filename = videoFilePath };
                var outputFile = new MediaFile { Filename = outputImagePath };
                var conversionOptions = new ConversionOptions { Seek = shotTime };

                try
                {
                    using (var engine = new Engine())
                    {
                        engine.GetThumbnail(inputFile, outputFile, conversionOptions);
                        Console.WriteLine($"成功截取 #{index:000}: {Path.GetFileName(outputImagePath)} (时间: {shotTime})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"在时间点 {shotTime} 截图失败：{ex.Message}");
                    
                    if (File.Exists(outputImagePath))
                    {
                        try { File.Delete(outputImagePath); } catch { }
                    }
                }
                finally
                {
                    // 释放自定义信号量
                    customThrottler.Release();
                    
                    lock (lockObj)
                    {
                        completedCount++;
                        if (completedCount % 10 == 0 || completedCount == shotTimes.Count)
                        {
                            Console.WriteLine($"进度：{completedCount}/{shotTimes.Count} ({((double)completedCount / shotTimes.Count * 100):F1}%)");
                        }
                    }
                }
            });
            
            tasks.Add(task);
            index++;
        }

        await Task.WhenAll(tasks);
        Console.WriteLine("所有截图任务完成。");
    }
}
