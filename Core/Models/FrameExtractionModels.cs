using System;
using System.Collections.Generic;
using System.Drawing.Imaging;

namespace PutPicture.Core.Models
{
    /// <summary>
    /// 帧提取请求
    /// </summary>
    public class FrameExtractionRequest
    {
        /// <summary>
        /// 源文件路径
        /// </summary>
        public string SourcePath { get; set; }
        
        /// <summary>
        /// 输出目录
        /// </summary>
        public string OutputDirectory { get; set; }
        
        /// <summary>
        /// 要提取的时间点列表
        /// </summary>
        public List<TimeSpan> Timestamps { get; set; } = new List<TimeSpan>();
        
        /// <summary>
        /// 提取选项
        /// </summary>
        public FrameExtractionOptions Options { get; set; } = new FrameExtractionOptions();
    }
    
    /// <summary>
    /// 帧提取选项
    /// </summary>
    public class FrameExtractionOptions
    {
        /// <summary>
        /// 输出图片格式
        /// </summary>
        public ImageFormat OutputFormat { get; set; } = ImageFormat.Jpeg;
        
        /// <summary>
        /// 输出图片质量 (1-100)
        /// </summary>
        public int Quality { get; set; } = 90;
        
        /// <summary>
        /// 最大并行度
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = -1;
        
        /// <summary>
        /// 输出文件名模板
        /// </summary>
        public string FileNameTemplate { get; set; } = "frame_{0:000}";
        
        /// <summary>
        /// 是否跳过已存在的文件
        /// </summary>
        public bool SkipExisting { get; set; } = true;
        
        /// <summary>
        /// 输出图片最大宽度（0表示不限制）
        /// </summary>
        public int MaxWidth { get; set; } = 0;
        
        /// <summary>
        /// 输出图片最大高度（0表示不限制）
        /// </summary>
        public int MaxHeight { get; set; } = 0;
        
        /// <summary>
        /// 是否保持宽高比
        /// </summary>
        public bool MaintainAspectRatio { get; set; } = true;
        
        /// <summary>
        /// 启用进度报告
        /// </summary>
        public bool EnableProgressReporting { get; set; } = true;
        
        /// <summary>
        /// 内存优化模式
        /// </summary>
        public bool MemoryOptimized { get; set; } = false;
    }
    
    /// <summary>
    /// 帧提取结果
    /// </summary>
    public class FrameExtractionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 提取的帧信息列表
        /// </summary>
        public List<ExtractedFrameInfo> ExtractedFrames { get; set; } = new List<ExtractedFrameInfo>();
        
        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
        
        /// <summary>
        /// 统计信息
        /// </summary>
        public FrameExtractionStatistics Statistics { get; set; } = new FrameExtractionStatistics();
        
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// 总耗时
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
    
    /// <summary>
    /// 提取的帧信息
    /// </summary>
    public class ExtractedFrameInfo
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public int FrameIndex { get; set; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public TimeSpan Timestamp { get; set; }
        
        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputPath { get; set; }
        
        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// 图片宽度
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// 图片高度
        /// </summary>
        public int Height { get; set; }
        
        /// <summary>
        /// 提取耗时
        /// </summary>
        public TimeSpan ExtractionTime { get; set; }
        
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// 帧提取统计信息
    /// </summary>
    public class FrameExtractionStatistics
    {
        /// <summary>
        /// 总帧数
        /// </summary>
        public int TotalFrames { get; set; }
        
        /// <summary>
        /// 成功提取的帧数
        /// </summary>
        public int SuccessfulFrames { get; set; }
        
        /// <summary>
        /// 跳过的帧数
        /// </summary>
        public int SkippedFrames { get; set; }
        
        /// <summary>
        /// 失败的帧数
        /// </summary>
        public int FailedFrames { get; set; }
        
        /// <summary>
        /// 总输出文件大小
        /// </summary>
        public long TotalOutputSize { get; set; }
        
        /// <summary>
        /// 平均提取速度（帧/秒）
        /// </summary>
        public double AverageSpeed { get; set; }
        
        /// <summary>
        /// 平均文件大小
        /// </summary>
        public double AverageFileSize => SuccessfulFrames > 0 ? (double)TotalOutputSize / SuccessfulFrames : 0;
    }
    
    /// <summary>
    /// 媒体信息
    /// </summary>
    public class MediaInfo
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 媒体类型
        /// </summary>
        public MediaType Type { get; set; }
        
        /// <summary>
        /// 时长
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// 宽度
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// 高度
        /// </summary>
        public int Height { get; set; }
        
        /// <summary>
        /// 帧率
        /// </summary>
        public double FrameRate { get; set; }
        
        /// <summary>
        /// 总帧数
        /// </summary>
        public int TotalFrames { get; set; }
        
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// 编码格式
        /// </summary>
        public string Codec { get; set; }
        
        /// <summary>
        /// 比特率
        /// </summary>
        public int Bitrate { get; set; }
    }
    
    /// <summary>
    /// 媒体类型
    /// </summary>
    public enum MediaType
    {
        Unknown,
        Video,
        Gif,
        Image
    }
    
    /// <summary>
    /// 进度报告事件参数
    /// </summary>
    public class FrameExtractionProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 当前进度（0-100）
        /// </summary>
        public int ProgressPercentage { get; set; }
        
        /// <summary>
        /// 当前处理的帧索引
        /// </summary>
        public int CurrentFrameIndex { get; set; }
        
        /// <summary>
        /// 总帧数
        /// </summary>
        public int TotalFrames { get; set; }
        
        /// <summary>
        /// 当前处理的文件名
        /// </summary>
        public string CurrentFileName { get; set; }
        
        /// <summary>
        /// 已用时间
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }
        
        /// <summary>
        /// 预估剩余时间
        /// </summary>
        public TimeSpan EstimatedRemainingTime { get; set; }
    }
}