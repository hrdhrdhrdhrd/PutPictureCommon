using System.Drawing;
using PutPicture.Core.Models;

namespace PutPicture.Core.Interfaces
{
    /// <summary>
    /// 图像处理器接口
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// 处理单张图片
        /// </summary>
        Bitmap ProcessImage(Bitmap source, ProcessingOptions options);
        
        /// <summary>
        /// 批量处理图片
        /// </summary>
        ProcessingResult ProcessBatch(BatchProcessingRequest request);
    }
}