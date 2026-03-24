namespace PutPicture.Core.Models
{
    /// <summary>
    /// 批量处理请求
    /// </summary>
    public class BatchProcessingRequest
    {
        /// <summary>
        /// 源目录
        /// </summary>
        public string SourceDirectory { get; set; }
        
        /// <summary>
        /// 目标目录
        /// </summary>
        public string TargetDirectory { get; set; }
        
        /// <summary>
        /// 处理选项
        /// </summary>
        public ProcessingOptions Options { get; set; }
        
        /// <summary>
        /// 最大并行度
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = -1;
        
        /// <summary>
        /// 支持的文件扩展名
        /// </summary>
        public string[] SupportedExtensions { get; set; } = 
        {
            "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff"
        };
        
        /// <summary>
        /// 跳过已存在的文件
        /// </summary>
        public bool SkipExisting { get; set; } = true;
    }
}