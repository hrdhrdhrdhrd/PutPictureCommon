using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using PutPicture.Core.Interfaces;
using PutPicture.Core.Models;
using PutPicture.Utils;

namespace PutPicture.Services
{
    /// <summary>
    /// 透明化处理服务
    /// </summary>
    public class TransparencyMakerService : ITransparencyMaker
    {
        private readonly IColorMatcher _colorMatcher;
        private readonly ILogger _logger;
        
        public TransparencyMakerService(IColorMatcher colorMatcher, ILogger logger)
        {
            _colorMatcher = colorMatcher ?? throw new ArgumentNullException(nameof(colorMatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// 使颜色透明
        /// </summary>
        public Bitmap MakeColorTransparent(Bitmap source, TransparencyOptions options)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (options == null) throw new ArgumentNullException(nameof(options));
            
            var tolerance = options.Tolerance;
            
            if (options.AdaptiveTolerance)
            {
                tolerance = CalculateAdaptiveTolerance(source, options.TargetColor, tolerance);
            }
            
            return options.UseGradient 
                ? MakeColorTransparentWithGradient(source, options.TargetColor, tolerance)
                : MakeColorTransparentFast(source, options.TargetColor, tolerance);
        }
        
        /// <summary>
        /// 计算自适应容差
        /// </summary>
        public unsafe int CalculateAdaptiveTolerance(Bitmap source, Color targetColor, int baseTolerance)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            
            BitmapData bitmapData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* ptr = (byte*)bitmapData.Scan0;
                int width = source.Width;
                int height = source.Height;
                int stride = bitmapData.Stride;
                
                var colorVariances = new List<double>();
                
                // 采样分析（每隔10个像素采样一次）
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
                
                _logger.Debug($"自适应容差: {baseTolerance} -> {adaptiveTolerance} (方差: {stdDev:F2})");
                return Math.Max(10, Math.Min(200, adaptiveTolerance));
            }
            finally
            {
                source.UnlockBits(bitmapData);
            }
        }
        
        /// <summary>
        /// 高性能透明化处理
        /// </summary>
        private unsafe Bitmap MakeColorTransparentFast(Bitmap source, Color targetColor, int tolerance)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            
            BitmapData srcData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            BitmapData dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                
                int width = source.Width;
                int height = source.Height;
                int stride = srcData.Stride;
                
                Parallel.For(0, height, y =>
                {
                    byte* srcRow = srcPtr + y * stride;
                    byte* dstRow = dstPtr + y * stride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        
                        byte b = srcRow[pixelIndex];
                        byte g = srcRow[pixelIndex + 1];
                        byte r = srcRow[pixelIndex + 2];
                        byte a = srcRow[pixelIndex + 3];
                        
                        bool shouldMakeTransparent = _colorMatcher.IsMatch(r, g, b, targetColor, tolerance);
                        
                        dstRow[pixelIndex] = b;
                        dstRow[pixelIndex + 1] = g;
                        dstRow[pixelIndex + 2] = r;
                        dstRow[pixelIndex + 3] = shouldMakeTransparent ? (byte)0 : a;
                    }
                });
            }
            finally
            {
                source.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 渐变透明化处理
        /// </summary>
        private unsafe Bitmap MakeColorTransparentWithGradient(Bitmap source, Color targetColor, int tolerance)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            
            BitmapData srcData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, 
                PixelFormat.Format32bppArgb);
            
            BitmapData dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, 
                PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                
                int width = source.Width;
                int height = source.Height;
                int stride = srcData.Stride;
                
                Parallel.For(0, height, y =>
                {
                    byte* srcRow = srcPtr + y * stride;
                    byte* dstRow = dstPtr + y * stride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        
                        byte b = srcRow[pixelIndex];
                        byte g = srcRow[pixelIndex + 1];
                        byte r = srcRow[pixelIndex + 2];
                        byte a = srcRow[pixelIndex + 3];
                        
                        double matchScore = _colorMatcher.CalculateMatchScore(r, g, b, targetColor, tolerance);
                        
                        byte alpha;
                        if (matchScore <= 0.3)
                        {
                            alpha = 0; // 高度匹配 - 完全透明
                        }
                        else if (matchScore >= 0.7)
                        {
                            alpha = a; // 低匹配度 - 保持不透明
                        }
                        else
                        {
                            // 中等匹配度 - 渐变透明
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
                source.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }
            
            return result;
        }
    }
}