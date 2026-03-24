using ImageMagick;
using System.IO;
using System.Linq;

public class TransparentGifCreator
{
    public static void CreateTransparentGifFromPngs(string[] pngPaths, string outputPath, int delay)
    {
        using var collection = new MagickImageCollection();
        
        foreach (var pngPath in pngPaths)
        {
            var image = new MagickImage(pngPath);
            
            // 关键设置：保持透明度
            image.AnimationDelay = (uint)delay; // 帧延迟
            image.GifDisposeMethod = GifDisposeMethod.Background; // 重要：背景处理方式
            image.BackgroundColor = MagickColors.Transparent; // 设置背景为透明
            
            collection.Add(image);
        }
        
        // 设置GIF为循环播放
        if (collection.Count > 0)
        {
            collection[0].AnimationIterations = 0; // 0表示无限循环
        }
        
        // 优化设置以保持透明度
        var settings = new QuantizeSettings
        {
            Colors = 256,
            DitherMethod = DitherMethod.FloydSteinberg
        };
        
        collection.Quantize(settings);
        collection.Optimize();
        collection.OptimizeTransparency();
        
        // 保存GIF
        collection.Write(outputPath);
    }
}