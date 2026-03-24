using System.Drawing;

namespace PutPicture.Core.Interfaces
{
    /// <summary>
    /// 颜色匹配接口
    /// </summary>
    public interface IColorMatcher
    {
        /// <summary>
        /// 判断颜色是否匹配
        /// </summary>
        bool IsMatch(byte r, byte g, byte b, Color targetColor, int tolerance);
        
        /// <summary>
        /// 计算颜色匹配分数
        /// </summary>
        double CalculateMatchScore(byte r, byte g, byte b, Color targetColor, int tolerance);
    }
}