using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ImageProcessing
{
    /// <summary>
    /// 透明化处理核心类
    /// </summary>
    public static class TransparencyMaker
    {
        /// <summary>
        /// 增强版抠图方法 - 集成所有高级功能
        /// </summary>
        public static unsafe Bitmap MakeColorTransparentEnhanced(
            Bitmap bitmap, Color color, int tolerance = 50, 
            bool enableEdgeSmoothing = true, 
            bool enableAdaptiveTolerance = true,
            bool enableMorphology = true,
            bool enableGradientAlpha = true,
            int despeckleSize = 3)
        {
            if (enableAdaptiveTolerance)
            {
                tolerance = CalculateAdaptiveTolerance(bitmap, color, tolerance);
            }
            
            var result = enableGradientAlpha 
                ? MakeColorTransparentWithGradient(bitmap, color, tolerance)
                : MakeColorTransparentFast(bitmap, color, tolerance);
            
            if (enableMorphology && despeckleSize > 0)
            {
                var morphed = MorphologyProcessor.ApplyMorphologicalOperations(result, despeckleSize);
                result.Dispose();
                result = morphed;
            }
            
            if (enableEdgeSmoothing)
            {
                var smoothed = EdgeProcessor.ApplyEdgeSmoothing(result);
                result.Dispose();
                result = smoothed;
            }
            
            return result;
        }
        
        /// <summary>
        /// 高性能版本 - 直接操作内存指针
        /// </summary>
        public static unsafe Bitmap MakeColorTransparentFast(Bitmap bitmap, Color color, int tolerance = 50)
        {
            Bitmap result = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            BitmapData resultData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)bitmapData.Scan0;
                byte* dstPtr = (byte*)resultData.Scan0;
                
                int width = bitmap.Width;
                int height = bitmap.Height;
                int srcStride = bitmapData.Stride;
                int dstStride = resultData.Stride;
                
                var targetHsv = ColorSpaceConverter.RgbToHsv(color.R, color.G, color.B);
                var targetLab = ColorSpaceConverter.RgbToLab(color.R, color.G, color.B);
                
                Parallel.For(0, height, y =>
                {
                    byte* srcRow = srcPtr + y * srcStride;
                    byte* dstRow = dstPtr + y * dstStride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        
                        byte b = srcRow[pixelIndex];
                        byte g = srcRow[pixelIndex + 1];
                        byte r = srcRow[pixelIndex + 2];
                        byte a = srcRow[pixelIndex + 3];
                        
                        bool shouldMakeTransparent = ColorMatcher.IsColorMatch(r, g, b, color, targetHsv, targetLab, tolerance);
                        
                        dstRow[pixelIndex] = b;
                        dstRow[pixelIndex + 1] = g;
                        dstRow[pixelIndex + 2] = r;
                        dstRow[pixelIndex + 3] = shouldMakeTransparent ? (byte)0 : a;
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
                result.UnlockBits(resultData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 渐变透明度抠图 - 边缘区域使用渐变透明
        /// </summary>
        public static unsafe Bitmap MakeColorTransparentWithGradient(Bitmap bitmap, Color color, int tolerance)
        {
            Bitmap result = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            BitmapData resultData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)bitmapData.Scan0;
                byte* dstPtr = (byte*)resultData.Scan0;
                
                int width = bitmap.Width;
                int height = bitmap.Height;
                int srcStride = bitmapData.Stride;
                int dstStride = resultData.Stride;
                
                var targetHsv = ColorSpaceConverter.RgbToHsv(color.R, color.G, color.B);
                var targetLab = ColorSpaceConverter.RgbToLab(color.R, color.G, color.B);
                
                Parallel.For(0, height, y =>
                {
                    byte* srcRow = srcPtr + y * srcStride;
                    byte* dstRow = dstPtr + y * dstStride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        
                        byte b = srcRow[pixelIndex];
                        byte g = srcRow[pixelIndex + 1];
                        byte r = srcRow[pixelIndex + 2];
                        byte a = srcRow[pixelIndex + 3];
                        
                        double matchScore = ColorMatcher.CalculateColorMatchScore(r, g, b, color, targetHsv, targetLab, tolerance);
                        
                        byte alpha;
                        if (matchScore <= 0.3)
                        {
                            alpha = 0;
                        }
                        else if (matchScore >= 0.7)
                        {
                            alpha = a;
                        }
                        else
                        {
                            double alphaFactor = (matchScore - 0.3) / 0.4;
                            alpha = (byte)(a * alphaFactor);
                        }
                        
                        dstRow[pixelIndex] = b;
                        dstRow[pixelIndex + 1] = g;
                        dstRow[pixelIndex + 2] = r;
                        dstRow[pixelIndex + 3] = alpha;
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
                result.UnlockBits(resultData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 计算自适应容差
        /// </summary>
        public static unsafe int CalculateAdaptiveTolerance(Bitmap bitmap, Color targetColor, int baseTolerance)
        {
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* ptr = (byte*)bitmapData.Scan0;
                int width = bitmap.Width;
                int height = bitmap.Height;
                int stride = bitmapData.Stride;
                
                var colorVariances = new List<double>();
                
                for (int y = 0; y < height; y += 10)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < width; x += 10)
                    {
                        int pixelIndex = x * 4;
                        byte b = row[pixelIndex];
                        byte g = row[pixelIndex + 1];
                        byte r = row[pixelIndex + 2];
                        
                        double distance = Math.Sqrt(
                            Math.Pow(r - targetColor.R, 2) + 
                            Math.Pow(g - targetColor.G, 2) + 
                            Math.Pow(b - targetColor.B, 2));
                        
                        colorVariances.Add(distance);
                    }
                }
                
                if (colorVariances.Count == 0) return baseTolerance;
                
                double avgDistance = colorVariances.Average();
                double variance = colorVariances.Select(d => Math.Pow(d - avgDistance, 2)).Average();
                double stdDev = Math.Sqrt(variance);
                
                double adaptiveFactor = Math.Max(0.5, Math.Min(2.0, stdDev / 50.0));
                int adaptiveTolerance = (int)(baseTolerance * adaptiveFactor);
                
                Logger.Log(Logger.LogLevel.Debug, $"自适应容差: {baseTolerance} -> {adaptiveTolerance} (方差: {stdDev:F2})");
                return Math.Max(10, Math.Min(200, adaptiveTolerance));
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }
}
