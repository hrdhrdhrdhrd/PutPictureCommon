using System.Drawing;
using PutPicture.Core.Models;

namespace PutPicture.Core.Interfaces
{
    /// <summary>
    /// 透明化处理接口
    /// </summary>
    public interface ITransparencyMaker
    {
        /// <summary>
        /// 使颜色透明
        /// </summary>
        Bitmap MakeColorTransparent(Bitmap source, TransparencyOptions options);
        
        /// <summary>
        /// 计算自适应容差
        /// </summary>
        int CalculateAdaptiveTolerance(Bitmap source, Color targetColor, int baseTolerance);
    }
}