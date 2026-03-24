using PutPicture.Core.Interfaces;
using PutPicture.Services;

namespace PutPicture.Core
{
    /// <summary>
    /// 服务注册配置
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// 配置服务容器
        /// </summary>
        public static ServiceContainer ConfigureServices(string logFilePath = null)
        {
            var container = new ServiceContainer();
            
            // 注册日志服务
            var logger = new LoggerService(logFilePath);
            container.RegisterSingleton<ILogger, LoggerService>(logger);
            
            // 注册颜色匹配服务
            container.RegisterFactory<IColorMatcher>(c => new ColorMatcherService());
            
            // 注册透明化服务
            container.RegisterFactory<ITransparencyMaker>(c => 
                new TransparencyMakerService(
                    c.GetService<IColorMatcher>(), 
                    c.GetService<ILogger>()));
            
            // 注册图像处理服务
            container.RegisterFactory<IImageProcessor>(c => 
                new ImageProcessorService(
                    c.GetService<ITransparencyMaker>(), 
                    c.GetService<ILogger>()));
            
            // 注册帧提取服务
            container.RegisterFactory<IVideoFrameExtractor>(c => 
                new VideoFrameExtractorService(c.GetService<ILogger>()));
            
            container.RegisterFactory<IGifFrameExtractor>(c => 
                new GifFrameExtractorService(c.GetService<ILogger>()));
            
            container.RegisterFactory<OptimizedFrameExtractorService>(c => 
                new OptimizedFrameExtractorService(
                    c.GetService<IVideoFrameExtractor>(),
                    c.GetService<IGifFrameExtractor>(),
                    c.GetService<ILogger>()));
            
            container.RegisterFactory<FrameExtractionManagerService>(c => 
                new FrameExtractionManagerService(
                    c.GetService<OptimizedFrameExtractorService>(),
                    c.GetService<ILogger>()));
            
            return container;
        }
    }
}