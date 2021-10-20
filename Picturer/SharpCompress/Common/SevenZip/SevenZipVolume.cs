using System.IO;

namespace SharpCompress.Common.SevenZip
{
    public class SevenZipVolume : GenericVolume
    {
        public SevenZipVolume(Stream stream, Options options)
            : base(stream, options)
        {
        }

#if !WINDOWS_PHONE
        public SevenZipVolume(FileInfo fileInfo, Options options)
            : base(fileInfo, options)
        {
        }
#endif
    }
}
