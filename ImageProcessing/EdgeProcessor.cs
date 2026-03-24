using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace ImageProcessing
{
    /// <summary>
    /// 边缘处理工具
    /// </summary>
    public static class EdgeProcessor
    {
        /// <summary>
        /// 边缘平滑处理
        /// </summary>
        public static unsafe Bitmap ApplyEdgeSmoothing(Bitmap bitmap)
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
                
                // 并行处理每一行（跳过边界）
                Parallel.For(1, height - 1, y =>
                {
                    byte* srcRow = srcPtr + y * stride;
                    byte* dstRow = dstPtr + y * stride;
                    
                    for (int x = 1; x < width - 1; x++)
                    {
                        int pixelIndex = x * 4;
                        byte srcAlpha = srcRow[pixelIndex + 3];
                        
                        if (srcAlpha > 0 && srcAlpha < 255)
                        {
                            ApplyAntiAliasing(srcPtr, dstPtr, x, y, width, height, stride);
                        }
                        else if (srcAlpha == 0)
                        {
                            bool hasOpaqueNeighbor = HasOpaqueNeighbor(srcPtr, x, y, width, height, stride);
                            
                            if (hasOpaqueNeighbor)
                            {
                                ApplyEdgeFeathering(srcPtr, dstPtr, x, y, width, height, stride);
                            }
                            else
                            {
                                SetTransparent(dstRow, pixelIndex);
                            }
                        }
                        else
                        {
                            CopyPixel(srcRow, dstRow, pixelIndex);
                        }
                    }
                });
                
                CopyBorderPixels(srcPtr, dstPtr, width, height, stride);
            }
            finally
            {
                bitmap.UnlockBits(srcData);
                result.UnlockBits(dstData);
            }
            
            return result;
        }
        
        /// <summary>
        /// 抗锯齿处理
        /// </summary>
        private static unsafe void ApplyAntiAliasing(byte* srcPtr, byte* dstPtr, int x, int y, int width, int height, int stride)
        {
            byte* dstRow = dstPtr + y * stride;
            int pixelIndex = x * 4;
            
            int totalR = 0, totalG = 0, totalB = 0, totalA = 0;
            int count = 0;
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        byte* neighborRow = srcPtr + ny * stride;
                        int neighborIndex = nx * 4;
                        
                        totalB += neighborRow[neighborIndex];
                        totalG += neighborRow[neighborIndex + 1];
                        totalR += neighborRow[neighborIndex + 2];
                        totalA += neighborRow[neighborIndex + 3];
                        count++;
                    }
                }
            }
            
            if (count > 0)
            {
                dstRow[pixelIndex] = (byte)(totalB / count);
                dstRow[pixelIndex + 1] = (byte)(totalG / count);
                dstRow[pixelIndex + 2] = (byte)(totalR / count);
                dstRow[pixelIndex + 3] = (byte)(totalA / count);
            }
        }
        
        /// <summary>
        /// 边缘羽化处理
        /// </summary>
        private static unsafe void ApplyEdgeFeathering(byte* srcPtr, byte* dstPtr, int x, int y, int width, int height, int stride)
        {
            byte* dstRow = dstPtr + y * stride;
            int pixelIndex = x * 4;
            
            int nearestR = 0, nearestG = 0, nearestB = 0;
            int minDistance = int.MaxValue;
            bool found = false;
            
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        byte* neighborRow = srcPtr + ny * stride;
                        int neighborIndex = nx * 4;
                        byte alpha = neighborRow[neighborIndex + 3];
                        
                        if (alpha > 128)
                        {
                            int distance = Math.Abs(dx) + Math.Abs(dy);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestB = neighborRow[neighborIndex];
                                nearestG = neighborRow[neighborIndex + 1];
                                nearestR = neighborRow[neighborIndex + 2];
                                found = true;
                            }
                        }
                    }
                }
            }
            
            if (found)
            {
                byte featherAlpha = (byte)Math.Max(0, 64 - minDistance * 20);
                dstRow[pixelIndex] = (byte)nearestB;
                dstRow[pixelIndex + 1] = (byte)nearestG;
                dstRow[pixelIndex + 2] = (byte)nearestR;
                dstRow[pixelIndex + 3] = featherAlpha;
            }
            else
            {
                SetTransparent(dstRow, pixelIndex);
            }
        }
        
        /// <summary>
        /// 检查是否有不透明邻居
        /// </summary>
        private static unsafe bool HasOpaqueNeighbor(byte* srcPtr, int x, int y, int width, int height, int stride)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        byte* neighborRow = srcPtr + ny * stride;
                        byte neighborAlpha = neighborRow[nx * 4 + 3];
                        if (neighborAlpha > 128)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        
        /// <summary>
        /// 复制边界像素
        /// </summary>
        private static unsafe void CopyBorderPixels(byte* srcPtr, byte* dstPtr, int width, int height, int stride)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = x * 4;
                CopyPixel(srcPtr, dstPtr, pixelIndex);
                
                int lastRowOffset = (height - 1) * stride;
                CopyPixel(srcPtr + lastRowOffset, dstPtr + lastRowOffset, pixelIndex);
            }
            
            for (int y = 1; y < height - 1; y++)
            {
                int rowOffset = y * stride;
                CopyPixel(srcPtr + rowOffset, dstPtr + rowOffset, 0);
                
                int lastColOffset = (width - 1) * 4;
                CopyPixel(srcPtr + rowOffset, dstPtr + rowOffset, lastColOffset);
            }
        }
        
        private static unsafe void CopyPixel(byte* src, byte* dst, int index)
        {
            dst[index] = src[index];
            dst[index + 1] = src[index + 1];
            dst[index + 2] = src[index + 2];
            dst[index + 3] = src[index + 3];
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
