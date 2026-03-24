using System.Drawing;

namespace PutPicture.Core.Models
{
    /// <summary>
    /// 图像处理选项
    /// </summary>
    public class ProcessingOptions
    {
        /// <summary>
        /// 背景色
        /// </summary>
        public Color BackgroundColor { get; set; } = Color.White;
        
        /// <summary>
        /// 容差
        /// </summary>
        public int Tolerance { get; set; } = 50;
        
        /// <summary>
        /// 启用边缘平滑
        /// </summary>
        public bool EnableEdgeSmoothing { get; set; } = true;
        
        /// <summary>
        /// 启用自适应容差
        /// </summary>
        public bool EnableAdaptiveTolerance { get; set; } = true;
        
        /// <summary>
        /// 启用形态学处理
        /// </summary>
        public bool EnableMorphology { get; set; } = true;
        
        /// <summary>
        /// 启用渐变透明
        /// </summary>
        public bool EnableGradientAlpha { get; set; } = true;
        
        /// <summary>
        /// 去噪尺寸
        /// </summary>
        public int DespeckleSize { get; set; } = 3;
    }
    
    /// <summary>
    /// 透明化选项
    /// </summary>
    public class TransparencyOptions
    {
        /// <summary>
        /// 目标颜色
        /// </summary>
        public Color TargetColor { get; set; }
        
        /// <summary>
        /// 容差
        /// </summary>
        public int Tolerance { get; set; }
        
        /// <summary>
        /// 使用渐变透明
        /// </summary>
        public bool UseGradient { get; set; }
        
        /// <summary>
        /// 自适应容差
        /// </summary>
        public bool AdaptiveTolerance { get; set; }
    }
}