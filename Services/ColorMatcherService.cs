using System;
using System.Drawing;
using PutPicture.Core.Interfaces;
using PutPicture.Utils;

namespace PutPicture.Services
{
    /// <summary>
    /// 颜色匹配服务
    /// </summary>
    public class ColorMatcherService : IColorMatcher
    {
        /// <summary>
        /// 判断颜色是否匹配
        /// </summary>
        public bool IsMatch(byte r, byte g, byte b, Color targetColor, int tolerance)
        {
            var targetHsv = ColorSpaceConverter.RgbToHsv(targetColor.R, targetColor.G, targetColor.B);
            var targetLab = ColorSpaceConverter.RgbToLab(targetColor.R, targetColor.G, targetColor.B);
            
            return IsColorMatch(r, g, b, targetColor, targetHsv, targetLab, tolerance);
        }
        
        /// <summary>
        /// 计算颜色匹配分数
        /// </summary>
        public double CalculateMatchScore(byte r, byte g, byte b, Color targetColor, int tolerance)
        {
            var targetHsv = ColorSpaceConverter.RgbToHsv(targetColor.R, targetColor.G, targetColor.B);
            var targetLab = ColorSpaceConverter.RgbToLab(targetColor.R, targetColor.G, targetColor.B);
            
            return CalculateColorMatchScore(r, g, b, targetColor, targetHsv, targetLab, tolerance);
        }
        
        /// <summary>
        /// 多重颜色匹配算法
        /// </summary>
        private bool IsColorMatch(byte r, byte g, byte b, Color targetColor, 
                                 (double H, double S, double V) targetHsv,
                                 (double L, double A, double B) targetLab,
                                 int tolerance)
        {
            // RGB 欧几里得距离匹配
            double rgbDistance = Math.Sqrt(
                Math.Pow(r - targetColor.R, 2) + 
                Math.Pow(g - targetColor.G, 2) + 
                Math.Pow(b - targetColor.B, 2));
            
            if (rgbDistance > tolerance * 2.5) return false;
            
            // HSV 色彩空间匹配
            var pixelHsv = ColorSpaceConverter.RgbToHsv(r, g, b);
            
            double hueDiff = Math.Min(
                Math.Abs(pixelHsv.H - targetHsv.H),
                360 - Math.Abs(pixelHsv.H - targetHsv.H));
            
            double satDiff = Math.Abs(pixelHsv.S - targetHsv.S);
            double valDiff = Math.Abs(pixelHsv.V - targetHsv.V);
            
            double hsvScore = (hueDiff / 180.0) * 0.4 + (satDiff / 100.0) * 0.3 + (valDiff / 100.0) * 0.3;
            
            // Lab 色彩空间匹配
            var pixelLab = ColorSpaceConverter.RgbToLab(r, g, b);
            double labDistance = Math.Sqrt(
                Math.Pow(pixelLab.L - targetLab.L, 2) + 
                Math.Pow(pixelLab.A - targetLab.A, 2) + 
                Math.Pow(pixelLab.B - targetLab.B, 2));
            
            double toleranceNormalized = tolerance / 255.0;
            
            // 对于低饱和度颜色，主要看亮度
            if (targetHsv.S < 0.1)
            {
                return valDiff < tolerance * 0.8;
            }
            
            return (rgbDistance < tolerance * 1.5) && 
                   (hsvScore < toleranceNormalized * 0.6) && 
                   (labDistance < tolerance * 1.2);
        }
        
        /// <summary>
        /// 计算颜色匹配分数
        /// </summary>
        private double CalculateColorMatchScore(byte r, byte g, byte b, Color targetColor,
                                               (double H, double S, double V) targetHsv,
                                               (double L, double A, double B) targetLab,
                                               int tolerance)
        {
            // RGB 距离
            double rgbDistance = Math.Sqrt(
                Math.Pow(r - targetColor.R, 2) + 
                Math.Pow(g - targetColor.G, 2) + 
                Math.Pow(b - targetColor.B, 2));
            
            // HSV 匹配
            var pixelHsv = ColorSpaceConverter.RgbToHsv(r, g, b);
            double hueDiff = Math.Min(
                Math.Abs(pixelHsv.H - targetHsv.H),
                360 - Math.Abs(pixelHsv.H - targetHsv.H));
            double satDiff = Math.Abs(pixelHsv.S - targetHsv.S);
            double valDiff = Math.Abs(pixelHsv.V - targetHsv.V);
            
            // Lab 距离
            var pixelLab = ColorSpaceConverter.RgbToLab(r, g, b);
            double labDistance = Math.Sqrt(
                Math.Pow(pixelLab.L - targetLab.L, 2) + 
                Math.Pow(pixelLab.A - targetLab.A, 2) + 
                Math.Pow(pixelLab.B - targetLab.B, 2));
            
            // 综合评分（归一化到 0-1）
            double rgbScore = Math.Min(1.0, rgbDistance / (tolerance * 2.5));
            double hsvScore = (hueDiff / 180.0) * 0.4 + (satDiff / 100.0) * 0.3 + (valDiff / 100.0) * 0.3;
            double labScore = Math.Min(1.0, labDistance / (tolerance * 1.5));
            
            return (rgbScore * 0.4 + hsvScore * 0.3 + labScore * 0.3);
        }
    }
}