using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace ImageProcessing
{
    /// <summary>
    /// 形态学处理工具
    /// </summary>
    public static class MorphologyProcessor
    {
        /// <summary>
        /// 形态学操作 - 去除噪点和孤立像素
        /// </summary>
        public static Bitmap ApplyMorphologicalOperations(Bitmap bitmap, int kernelSize)
        {
            var eroded = ApplyErosion(bitmap, kernelSize);
            var opened = ApplyDilation(eroded, kernelSize);
            eroded.Dispose();
            
            var dilated = ApplyDilation(opened, kernelSize);
            var closed = ApplyErosion(dilated, kernelSize);
            dilated.Dispose();
            opened.Dispose();
            
            return closed;
        }
        
        /// <summary>
        /// 腐蚀操作
        /// </summary>
        public static unsafe Bitmap ApplyErosion(Bitmap bitmap, int kernelSize)
        {
            Bitmap result = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            
            BitmapData srcData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            
            BitmapData dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                
                int width = bitmap.Width;
                int height = bitmap.Height;
                int stride = srcData.Stride;
                int radius = kernelSize / 2;
                
                Parallel.For(0, height, y =>
                {
                    byte* dstRow = dstPtr + y * stride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        byte* srcPixel = srcPtr + y * stride + pixelIndex;
                        
                        if (srcPixel[3] == 0)
                        {
                            SetTransparent(dstRow, pixelIndex);
                            continue;
                        }
                        
                        bool hasTransparentNeighbor = HasTransparentNeighbor(srcPtr, x, y, width, height, stride, radius);
                        
                        if (hasTransparentNeighbor)
                        {
                            SetTransparent(dstRow, pixelIndex);
                        }
                        else
                        {
                            CopyPixel(srcPixel, dstRow, pixelIndex);
                        }
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 膨胀操作
        /// </summary>
        public static unsafe Bitmap ApplyDilation(Bitmap bitmap, int kernelSize)
        {
            Bitmap result = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            
            BitmapData srcData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            
            BitmapData dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
            try
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                
                int width = bitmap.Width;
                int height = bitmap.Height;
                int stride = srcData.Stride;
                int radius = kernelSize / 2;
                
                Parallel.For(0, height, y =>
                {
                    byte* dstRow = dstPtr + y * stride;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex = x * 4;
                        byte* srcPixel = srcPtr + y * stride + pixelIndex;
                        
                        if (srcPixel[3] > 128)
                        {
                            CopyPixel(srcPixel, dstRow, pixelIndex);
                            continue;
                        }
                        
                        var (maxAlpha, avgColor) = GetOpaqueNeighborInfo(srcPtr, x, y, width, height, stride, radius);
                        
                        if (avgColor.count > 0)
                        {
                            dstRow[pixelIndex] = (byte)(avgColor.sumB / avgColor.count);
                            dstRow[pixelIndex + 1] = (byte)(avgColor.sumG / avgColor.count);
                            dstRow[pixelIndex + 2] = (byte)(avgColor.sumR / avgColor.count);
                            dstRow[pixelIndex + 3] = (byte)Math.Min(255, maxAlpha);
                        }
                        else
                        {
                            SetTransparent(dstRow, pixelIndex);
                        }
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }
            
            return result;
        }
        
        private static unsafe bool HasTransparentNeighbor(byte* srcPtr, int x, int y, int width, int height, int stride, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        byte* neighbor = srcPtr + ny * stride + nx * 4;
                        if (neighbor[3] < 128)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        
        private static unsafe (int maxAlpha, (int sumR, int sumG, int sumB, int count)) GetOpaqueNeighborInfo(
            byte* srcPtr, int x, int y, int width, int height, int stride, int radius)
        {
            int maxAlpha = 0;
            int sumR = 0, sumG = 0, sumB = 0, count = 0;
            
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        byte* neighbor = srcPtr + ny * stride + nx * 4;
                        if (neighbor[3] > maxAlpha)
                        {
                            maxAlpha = neighbor[3];
                        }
                        if (neighbor[3] > 128)
                        {
                            sumR += neighbor[2];
                            sumG += neighbor[1];
                            sumB += neighbor[0];
                            count++;
                        }
                    }
                }
            }
            
            return (maxAlpha, (sumR, sumG, sumB, count));
        }
        
        private static unsafe void CopyPixel(byte* src, byte* dst, int index)
        {
            dst[index] = src[0];
            dst[index + 1] = src[1];
            dst[index + 2] = src[2];
            dst[index + 3] = src[3];
        }
        
        private static unsafe void SetTransparent(byte* dst, int index)
        {
            dst[index] = 0;
            dst[index + 1] = 0;
            dst[index + 2] = 0;
            dst[index + 3] = 0;
        }
    }
}
