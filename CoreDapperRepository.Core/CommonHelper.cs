using CoreDapperRepository.Core.Infrastructure;

namespace CoreDapperRepository.Core
{
    public class CommonHelper
    {
        /// <summary>
        /// Gets or sets the default file provider
        /// </summary>
        public static IAppFileProvider DefaultFileProvider { get; set; }
    }
}
