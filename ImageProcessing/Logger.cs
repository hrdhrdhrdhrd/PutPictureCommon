using System;
using System.IO;

namespace ImageProcessing
{
    /// <summary>
    /// 日志管理器
    /// </summary>
    public static class Logger
    {
        // 日志级别枚举
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
        
        // 日志配置
        private static LogLevel _currentLogLevel = LogLevel.Info;
        private static string _logFilePath = null;
        private static readonly object _logLock = new object();
        
        /// <summary>
        /// 设置日志级别
        /// </summary>
        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
            Log(LogLevel.Info, $"日志级别已设置为: {level}");
        }
        
        /// <summary>
        /// 设置日志文件路径
        /// </summary>
        public static void SetLogFile(string filePath)
        {
            _logFilePath = filePath;
            Log(LogLevel.Info, $"日志文件路径已设置为: {filePath}");
        }
        
        /// <summary>
        /// 统一日志方法
        /// </summary>
        public static void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _currentLogLevel) return;
            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string levelStr = level.ToString().ToUpper().PadRight(7);
            string logMessage = $"[{timestamp}] [{levelStr}] {message}";
            
            if (ex != null)
            {
                logMessage += $"\n异常: {ex.Message}\n堆栈: {ex.StackTrace}";
            }
            
            // 控制台输出
            Console.WriteLine(logMessage);
            
            // 文件输出
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    lock (_logLock)
                    {
                        File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"写入日志文件失败: {logEx.Message}");
                }
            }
        }
    }
}
