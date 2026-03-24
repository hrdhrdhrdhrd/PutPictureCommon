using System;
using System.Drawing;

namespace ImageProcessing
{
    /// <summary>
    /// 颜色匹配算法
    /// </summary>
    public static class ColorMatcher
    {
        /// <summary>
        /// 多重颜色匹配算法 - 结合 RGB、HSV 和 Lab 色彩空间
        /// </summary>
        public static bool IsColorMatch(byte r, byte g, byte b, Color targetColor, 
                                       (double H, double S, double V) targetHsv,
                                       (double L, double A, double B) targetLab,
                                       int tolerance)
        {
            // 1. RGB 欧几里得距离匹配（快速预筛选）
            double rgbDistance = Math.Sqrt(
                Math.Pow(r - targetColor.R, 2) + 
                Math.Pow(g - targetColor.G, 2) + 
                Math.Pow(b - targetColor.B, 2));
            
            // 如果 RGB 距离太大，直接排除
            if (rgbDistance > tolerance * 2.5) return false;
            
            // 2. HSV 色彩空间匹配（对色调敏感）
            var pixelHsv = ColorSpaceConverter.RgbToHsv(r, g, b);
            
            // 色调差异（考虑环形特性）
            double hueDiff = Math.Min(
                Math.Abs(pixelHsv.H - targetHsv.H),
                360 - Math.Abs(pixelHsv.H - targetHsv.H));
            
            // 饱和度和明度差异
            double satDiff = Math.Abs(pixelHsv.S - targetHsv.S);
            double valDiff = Math.Abs(pixelHsv.V - targetHsv.V);
            
            // HSV 综合评分
            double hsvScore = (hueDiff / 180.0) * 0.4 + (satDiff / 100.0) * 0.3 + (valDiff / 100.0) * 0.3;
            
            // 3. Lab 色彩空间匹配（感知均匀）
            var pixelLab = ColorSpaceConverter.RgbToLab(r, g, b);
            double labDistance = Math.Sqrt(
                Math.Pow(pixelLab.L - targetLab.L, 2) + 
                Math.Pow(pixelLab.A - targetLab.A, 2) + 
                Math.Pow(pixelLab.B - targetLab.B, 2));
            
            // 4. 综合判断
            double toleranceNormalized = tolerance / 255.0;
            
            // 对于低饱和度颜色（灰色、白色等），主要看亮度
            if (targetHsv.S < 0.1)
            {
                return valDiff < tolerance * 0.8;
            }
            
            // 对于高饱和度颜色，综合考虑所有因素
            return (rgbDistance < tolerance * 1.5) && 
                   (hsvScore < toleranceNormalized * 0.6) && 
                   (labDistance < tolerance * 1.2);
        }
        
        /// <summary>
        /// 计算颜色匹配分数（0-1，0表示完全匹配）
        /// </summary>
        public static double CalculateColorMatchScore(byte r, byte g, byte b, Color targetColor,
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
            
            // 加权平均
            return (rgbScore * 0.4 + hsvScore * 0.3 + labScore * 0.3);
        }
    }
}
