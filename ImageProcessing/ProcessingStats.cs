using System.Collections.Concurrent;

namespace ImageProcessing
{
    /// <summary>
    /// 处理统计信息类
    /// </summary>
    public class ProcessingStats
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorFiles { get; set; }
        public long TotalBytesProcessed { get; set; }
        public long TotalBytesOutput { get; set; }
        public ConcurrentBag<string> ErrorMessages { get; } = new ConcurrentBag<string>();
        public ConcurrentBag<(string FileName, double ProcessingTime)> ProcessingTimes { get; } = new ConcurrentBag<(string, double)>();
    }
}
