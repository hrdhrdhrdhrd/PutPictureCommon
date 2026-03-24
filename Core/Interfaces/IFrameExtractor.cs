using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using PutPicture.Core.Models;

namespace PutPicture.Core.Interfaces
{
    /// <summary>
    /// 帧提取器接口
    /// </summary>
    public interface IFrameExtractor
    {
        /// <summary>
        /// 提取单帧
        /// </summary>
        Task<Bitmap> ExtractFrameAsync(string sourcePath, TimeSpan timestamp);
        
        /// <summary>
        /// 批量提取帧
        /// </summary>
        Task<FrameExtractionResult> ExtractFramesAsync(FrameExtractionRequest request);
        
        /// <summary>
        /// 获取媒体信息
        /// </summary>
        Task<MediaInfo> GetMediaInfoAsync(string sourcePath);
        
        /// <summary>
        /// 检查是否支持该文件格式
        /// </summary>
        bool IsSupported(string filePath);
    }
    
    /// <summary>
    /// 视频帧提取器接口
    /// </summary>
    public interface IVideoFrameExtractor : IFrameExtractor
    {
        /// <summary>
        /// 按时间间隔提取帧
        /// </summary>
        Task<FrameExtractionResult> ExtractFramesByIntervalAsync(string videoPath, string outputDirectory, TimeSpan interval, FrameExtractionOptions options = null);
        
        /// <summary>
        /// 按帧数提取帧
        /// </summary>
        Task<FrameExtractionResult> ExtractFramesByCountAsync(string videoPath, string outputDirectory, int frameCount, FrameExtractionOptions options = null);
    }
    
    /// <summary>
    /// GIF帧提取器接口
    /// </summary>
    public interface IGifFrameExtractor : IFrameExtractor
    {
        /// <summary>
        /// 提取所有帧
        /// </summary>
        Task<FrameExtractionResult> ExtractAllFramesAsync(string gifPath, string outputDirectory, FrameExtractionOptions options = null);
        
        /// <summary>
        /// 提取指定帧
        /// </summary>
        Task<Bitmap> ExtractFrameByIndexAsync(string gifPath, int frameIndex);
    }
}