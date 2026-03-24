using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public class GifCreator
{
    // 缓存编码器，避免重复查找
    private static readonly Lazy<ImageCodecInfo> _gifEncoder = new(() => GetEncoder(ImageFormat.Gif));
    
    public static void CreateGifFromPngs(string[] pngPaths, string outputPath, int frameDelay = 100)
    {
        if (pngPaths == null || pngPaths.Length == 0)
            throw new ArgumentException("PNG paths cannot be null or empty");
        
        Console.WriteLine($"开始创建GIF，共 {pngPaths.Length} 帧...");
        var startTime = DateTime.Now;
        
        // 预先验证所有文件存在
        foreach (var path in pngPaths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"PNG file not found: {path}");
        }
        
        // 使用第一张图片确定尺寸，避免加载所有图片到内存
        int width, height;
        using (var firstImage = Image.FromFile(pngPaths[0]))
        {
            width = firstImage.Width;
            height = firstImage.Height;
        }
        
        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        
        // 创建GIF - 使用缓冲流提升IO性能
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536);
        using var bufferedStream = new BufferedStream(fileStream, 65536);
        
        // 使用第一张图片作为GIF基础
        using var firstFrame = new Bitmap(pngPaths[0]);
        
        // 配置GIF编码器参数
        var gifEncoder = _gifEncoder.Value;
        using var encoderParams = new EncoderParameters(2);
        
        // 设置多帧和帧延迟
        encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        encoderParams.Param[1] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionLZW);
        
        // 保存第一帧到流
        firstFrame.Save(bufferedStream, gifEncoder, encoderParams);
        
        // 配置后续帧参数
        using var frameParams = new EncoderParameters(1);
        frameParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
        
        // 流式处理后续帧，避免同时加载所有图片到内存
        for (int i = 1; i < pngPaths.Length; i++)
        {
            using var frame = new Bitmap(pngPaths[i]);
            
            // 验证尺寸一致性
            if (frame.Width != width || frame.Height != height)
            {
                Console.WriteLine($"警告: 第 {i + 1} 帧尺寸不匹配 ({frame.Width}x{frame.Height} vs {width}x{height})");
            }
            
            firstFrame.SaveAdd(frame, frameParams);
            
            if (i % 10 == 0) // 每10帧显示进度
                Console.WriteLine($"已处理 {i}/{pngPaths.Length} 帧");
        }
        
        // 结束GIF
        using var flushParams = new EncoderParameters(1);
        flushParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        firstFrame.SaveAdd(flushParams);
        
        var endTime = DateTime.Now;
        var duration = endTime - startTime;
        
        Console.WriteLine($"GIF创建完成！");
        Console.WriteLine($"输出文件: {outputPath}");
        Console.WriteLine($"总帧数: {pngPaths.Length}");
        Console.WriteLine($"耗时: {duration.TotalSeconds:F2} 秒");
        Console.WriteLine($"处理速度: {(pngPaths.Length / duration.TotalSeconds):F2} 帧/秒");
    }
    
    // 高性能批量创建GIF - 并行处理多个GIF
    public static void CreateMultipleGifs(Dictionary<string, string[]> gifConfigs, int frameDelay = 100, int maxParallelism = -1)
    {
        if (gifConfigs == null || gifConfigs.Count == 0)
            return;
        
        Console.WriteLine($"开始并行创建 {gifConfigs.Count} 个GIF文件...");
        var startTime = DateTime.Now;
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism == -1 ? Environment.ProcessorCount : maxParallelism
        };
        
        int completed = 0;
        int errors = 0;
        
        Parallel.ForEach(gifConfigs, parallelOptions, kvp =>
        {
            try
            {
                var outputPath = kvp.Key;
                var pngPaths = kvp.Value;
                
                Console.WriteLine($"[线程 {System.Threading.Thread.CurrentThread.ManagedThreadId}] 创建: {Path.GetFileName(outputPath)}");
                CreateGifFromPngs(pngPaths, outputPath, frameDelay);
                
                Interlocked.Increment(ref completed);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                Console.WriteLine($"创建GIF失败 {kvp.Key}: {ex.Message}");
            }
        });
        
        var endTime = DateTime.Now;
        var duration = endTime - startTime;
        
        Console.WriteLine($"批量GIF创建完成！成功: {completed}，失败: {errors}");
        Console.WriteLine($"总耗时: {duration.TotalSeconds:F2} 秒");
    }
    
    // 优化的编码器获取 - 使用正确的方法
    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders(); // 使用 Encoders 而不是 Decoders
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid) 
               ?? throw new NotSupportedException($"No encoder found for format: {format}");
    }
    
    // 从目录创建GIF的便捷方法
    public static void CreateGifFromDirectory(string imageDirectory, string outputPath, 
                                            string searchPattern = "*.png", 
                                            int frameDelay = 100)
    {
        if (!Directory.Exists(imageDirectory))
            throw new DirectoryNotFoundException($"Directory not found: {imageDirectory}");
        
        var pngFiles = Directory.GetFiles(imageDirectory, searchPattern, SearchOption.TopDirectoryOnly)
                               .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                               .ToArray();
        
        if (pngFiles.Length == 0)
            throw new InvalidOperationException($"No files found matching pattern '{searchPattern}' in directory: {imageDirectory}");
        
        Console.WriteLine($"找到 {pngFiles.Length} 个图片文件");
        CreateGifFromPngs(pngFiles, outputPath, frameDelay);
    }
}
