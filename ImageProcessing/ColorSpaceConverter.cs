using System;

namespace ImageProcessing
{
    /// <summary>
    /// 色彩空间转换工具
    /// </summary>
    public static class ColorSpaceConverter
    {
        /// <summary>
        /// RGB 转 HSV
        /// </summary>
        public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
        {
            double rNorm = r / 255.0;
            double gNorm = g / 255.0;
            double bNorm = b / 255.0;
            
            double max = Math.Max(rNorm, Math.Max(gNorm, bNorm));
            double min = Math.Min(rNorm, Math.Min(gNorm, bNorm));
            double delta = max - min;
            
            double h = 0;
            if (delta != 0)
            {
                if (max == rNorm)
                    h = 60 * (((gNorm - bNorm) / delta) % 6);
                else if (max == gNorm)
                    h = 60 * ((bNorm - rNorm) / delta + 2);
                else
                    h = 60 * ((rNorm - gNorm) / delta + 4);
            }
            
            if (h < 0) h += 360;
            
            double s = max == 0 ? 0 : (delta / max) * 100;
            double v = max * 100;
            
            return (h, s, v);
        }
        
        /// <summary>
        /// RGB 转 Lab（简化版本）
        /// </summary>
        public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
        {
            // 先转换到 XYZ 色彩空间
            double rNorm = r / 255.0;
            double gNorm = g / 255.0;
            double bNorm = b / 255.0;
            
            // Gamma 校正
            rNorm = rNorm > 0.04045 ? Math.Pow((rNorm + 0.055) / 1.055, 2.4) : rNorm / 12.92;
            gNorm = gNorm > 0.04045 ? Math.Pow((gNorm + 0.055) / 1.055, 2.4) : gNorm / 12.92;
            bNorm = bNorm > 0.04045 ? Math.Pow((bNorm + 0.055) / 1.055, 2.4) : bNorm / 12.92;
            
            // 转换到 XYZ（使用 sRGB 矩阵）
            double x = rNorm * 0.4124564 + gNorm * 0.3575761 + bNorm * 0.1804375;
            double y = rNorm * 0.2126729 + gNorm * 0.7151522 + bNorm * 0.0721750;
            double z = rNorm * 0.0193339 + gNorm * 0.1191920 + bNorm * 0.9503041;
            
            // 标准化（D65 白点）
            x /= 0.95047;
            y /= 1.00000;
            z /= 1.08883;
            
            // 转换到 Lab
            x = x > 0.008856 ? Math.Pow(x, 1.0/3.0) : (7.787 * x + 16.0/116.0);
            y = y > 0.008856 ? Math.Pow(y, 1.0/3.0) : (7.787 * y + 16.0/116.0);
            z = z > 0.008856 ? Math.Pow(z, 1.0/3.0) : (7.787 * z + 16.0/116.0);
            
            double L = 116 * y - 16;
            double A = 500 * (x - y);
            double B = 200 * (y - z);
            
            return (L, A, B);
        }
    }
}
