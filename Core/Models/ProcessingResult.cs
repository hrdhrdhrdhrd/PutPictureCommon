using System;
using System.Collections.Generic;

namespace PutPicture.Core.Models
{
    /// <summary>
    /// 处理结果
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 统计信息
        /// </summary>
        public ProcessingStatistics Statistics { get; set; } = new ProcessingStatistics();
        
        /// <summary>
        /// 错误信息
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
        
        /// <summary>
        /// 处理开始时间
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// 处理结束时间
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// 总耗时
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
    
    /// <summary>
    /// 处理统计信息
    /// </summary>
    public class ProcessingStatistics
    {
        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }
        
        /// <summary>
        /// 成功处理的文件数
        /// </summary>
        public int ProcessedFiles { get; set; }
        
        /// <summary>
        /// 跳过的文件数
        /// </summary>
        public int SkippedFiles { get; set; }
        
        /// <summary>
        /// 失败的文件数
        /// </summary>
        public int FailedFiles { get; set; }
        
        /// <summary>
        /// 总输入字节数
        /// </summary>
        public long TotalInputBytes { get; set; }
        
        /// <summary>
        /// 总输出字节数
        /// </summary>
        public long TotalOutputBytes { get; set; }
        
        /// <summary>
        /// 处理时间记录
        /// </summary>
        public List<FileProcessingTime> ProcessingTimes { get; set; } = new List<FileProcessingTime>();
        
        /// <summary>
        /// 平均处理速度（文件/秒）
        /// </summary>
        public double AverageSpeed { get; set; }
        
        /// <summary>
        /// 压缩率
        /// </summary>
        public double CompressionRatio => TotalInputBytes > 0 
            ? (1.0 - (double)TotalOutputBytes / TotalInputBytes) * 100 
            : 0;
    }
    
    /// <summary>
    /// 文件处理时间记录
    /// </summary>
    public class FileProcessingTime
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 处理时间（秒）
        /// </summary>
        public double ProcessingTimeSeconds { get; set; }
        
        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSizeBytes { get; set; }
    }
}