using System;
using System.IO;
using PutPicture.Core.Interfaces;

namespace PutPicture.Services
{
    /// <summary>
    /// 日志服务实现
    /// </summary>
    public class LoggerService : ILogger
    {
        private readonly object _lockObject = new object();
        private string _logFilePath;
        
        public LogLevel Level { get; set; } = LogLevel.Info;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public LoggerService(string logFilePath = null)
        {
            _logFilePath = logFilePath;
        }
        
        /// <summary>
        /// 设置日志文件路径
        /// </summary>
        public void SetLogFile(string filePath)
        {
            _logFilePath = filePath;
        }
        
        public void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < Level) return;
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper().PadRight(7);
            var logMessage = $"[{timestamp}] [{levelStr}] {message}";
            
            if (exception != null)
            {
                logMessage += $"\n异常: {exception.Message}\n堆栈: {exception.StackTrace}";
            }
            
            // 控制台输出
            Console.WriteLine(logMessage);
            
            // 文件输出
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"写入日志文件失败: {ex.Message}");
                }
            }
        }
        
        public void Debug(string message) => Log(LogLevel.Debug, message);
        
        public void Info(string message) => Log(LogLevel.Info, message);
        
        public void Warning(string message) => Log(LogLevel.Warning, message);
        
        public void Error(string message, Exception exception = null) => Log(LogLevel.Error, message, exception);
    }
}