using System;
#if !WINDOWS_PHONE
using System.IO;
#endif

namespace SharpCompress.Common
{
    public interface IVolume : IDisposable
    {
#if !WINDOWS_PHONE
        /// <summary>
        /// File that backs this volume, if it not stream based
        /// </summary>
        FileInfo VolumeFile
        {
            get;
        }
#endif
    }
}
