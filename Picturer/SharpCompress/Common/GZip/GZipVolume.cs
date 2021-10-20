using System.IO;

namespace SharpCompress.Common.GZip
{
    public class GZipVolume : Volume
    {
#if !WINDOWS_PHONE
        private readonly FileInfo fileInfo;
#endif

        public GZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !WINDOWS_PHONE
        public GZipVolume(FileInfo fileInfo, Options options)
            : base(fileInfo.OpenRead(), options)
        {
            this.fileInfo = fileInfo;
        }
#endif

#if !WINDOWS_PHONE
        /// <summary>
        /// File that backs this volume, if it not stream based
        /// </summary>
        public override FileInfo VolumeFile
        {
            get
            {
                return fileInfo;
            }
        }
#endif

        public override bool IsFirstVolume
        {
            get
            {
                return true;
            }
        }

        public override bool IsMultiVolume
        {
            get
            {
                return true;
            }
        }
    }
}
