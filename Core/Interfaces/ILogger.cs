using System;

namespace PutPicture.Core.Interfaces
{
    /// <summary>
    /// 日志接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        LogLevel Level { get; set; }
        
        /// <summary>
        /// 记录日志
        /// </summary>
        void Log(LogLevel level, string message, Exception exception = null);
        
        /// <summary>
        /// 记录调试信息
        /// </summary>
        void Debug(string message);
        
        /// <summary>
        /// 记录信息
        /// </summary>
        void Info(string message);
        
        /// <summary>
        /// 记录警告
        /// </summary>
        void Warning(string message);
        
        /// <summary>
        /// 记录错误
        /// </summary>
        void Error(string message, Exception exception = null);
    }
    
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}