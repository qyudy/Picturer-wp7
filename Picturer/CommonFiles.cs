using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
#if WINDOWS_PHONE
using ImageTools;
using System.Windows.Media;
#elif !SILVERLIGHT
using System.Drawing;
using System.Windows.Interop;
#endif

namespace Picturer
{

    public class Package
    {
        public DirTree Tree { get { return tree; } }
        public int Length { get { return tree.Seeds.Count; } }
        public BitmapSource Bmp { get; private set; }
        public string FilePath { get { return Path.Combine(path, tree.Seeds[picNum]); } }
        public bool IsEmpty { get { return !tree.HasSeed; } }

        private readonly DirTree tree;
        private readonly string path;
        private int picNum;
        public ChangeMode picChangeMode;
        private Dispatcher dispatcher;
        private RandomCache randomCache;

        public Package(DirTree tree, ChangeMode picChangeMode, Dispatcher dispatcher, string filename = null)
        {
            this.tree = tree;
            if (this.IsEmpty)
            {
                throw new IndexOutOfRangeException("尝试建立空图包:" + tree.Dir);
            }
            this.picChangeMode = picChangeMode;
            this.dispatcher = dispatcher;
            this.path = GetFilePath("", tree);
            this.randomCache = new RandomCache(Length);
            Reset();
            if (filename != null)
            {
                int start = picNum;
                while (tree.Seeds[picNum] != filename)
                {
                    ChangePicNum(1, PicMode.AllSupport);
                    if (start == picNum)
                    {
                        throw new ArgumentException("参数异常，文件在图包中不存在");
                    }
                }
            }
        }

        public static string GetFilePath(string path,DirTree tree)
        {
            do
            {
                path = Path.Combine(tree.Dir, path);
            } while ((tree = tree.Root) != null);
            return path;
        }
        
        public void Reset()
        {
            switch (picChangeMode)
            {
                case ChangeMode.Sequence:
                    this.picNum = 0;
                    break;
                case ChangeMode.Random:
                    this.picNum = randomCache.reset();
                    break;
            }
        }
        public BitmapSource ChangePic(int picNumChange, PicMode picMode)
        {
            return SetAndGetPic(ChangePicNum(picNumChange, picMode));
        }
        public int ChangePicNum(int changeNum, PicMode picMode)
        {
            int firstIndex = -1;
            do
            {
                switch (picChangeMode)
                {
                    case ChangeMode.Sequence:
                        this.picNum = (picNum + changeNum + tree.Seeds.Count) % tree.Seeds.Count;
                        break;
                    case ChangeMode.Random:
                        this.picNum = randomCache.ChangeIndex(changeNum);
                        break;
                }
                if (firstIndex == -1) firstIndex = this.picNum;
                else if (this.picNum == firstIndex) throw new IndexOutOfRangeException("图包访问发生循环异常,通常因为访问了类型不正确的图包");
            } while ((changeNum = changeNum >= 0 ? 1 : -1) != 0 && !Tools.IsPicImage(tree.Seeds[picNum], picMode));
            return this.picNum;
        }
        public bool SetPicNum(int picNum)
        {
            if (tree.Seeds.Count > picNum)
            {
                this.picNum = picNum;
                return true;
            }
            else
            {
                return false;
            }
        }
        public BitmapSource SetAndGetPic(PicMode picMode = PicMode.AllSupport, IsolatedStorageFile store = null)
        {
            return SetAndGetPic(this.picNum, picMode, store);
        }
        public BitmapSource SetAndGetPic(int picNum, PicMode picMode = PicMode.AllSupport, IsolatedStorageFile store = null)
        {
            if (SetPicNum(picNum))
            {
                if (store == null)
                {
#if WINDOWS_PHONE
                    using (store = IsolatedStorageFile.GetUserStoreForApplication())
#endif
                    {
                        setBmp(store, picMode);
                    }
                }
                else
                {
                    setBmp(store, picMode);
                }
            }
            return Bmp;
        }
        private void setBmp(IsolatedStorageFile store, PicMode picMode)
        {
#if WINDOWS_PHONE
            if (NeedCache && Tools.IsPicImage(tree.Seeds[picNum], PicMode.StaticPic))
            {
                var cachePicMode = picMode == PicMode.GifAsJpg ? picMode : PicMode.StaticPic;
                var cachePaths = new KeyValuePair<int, string>[3];
                cachePaths[0] = new KeyValuePair<int, string>(this.picNum, FilePath);
                cachePaths[1] = new KeyValuePair<int, string>(ChangePicNum(-1, cachePicMode), FilePath);
                ChangePicNum(1, cachePicMode);
                cachePaths[2] = new KeyValuePair<int, string>(ChangePicNum(1, cachePicMode), FilePath);
                ChangePicNum(-1, cachePicMode);
                Bmp = cacheBmp(store, cachePaths[0], true);
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    using (store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        cacheBmp(store, cachePaths[1], false);
                        cacheBmp(store, cachePaths[2], false);
                    }
                    if ((double)Microsoft.Phone.Info.DeviceStatus.ApplicationCurrentMemoryUsage / Microsoft.Phone.Info.DeviceStatus.ApplicationMemoryUsageLimit > 0.5)
                    {
                        for (int i = 0; i < cacheIndexs.Count / 3; i++)
                        {
                            bmpCache.Remove(cacheIndexs[i]);
                        }
                        cacheIndexs.RemoveRange(0, cacheIndexs.Count / 3);
                    }
                });
            }
            else if (Tools.IsPicImage(tree.Seeds[picNum], PicMode.StaticPic) || (Tools.IsPicImage(tree.Seeds[picNum], PicMode.OnlyGif) && picMode == PicMode.GifAsJpg))
            {
                using (var stream = store.OpenFile(FilePath, FileMode.Open))
                {
                    Bmp = new BitmapImage();
                    Bmp.SetSource(stream);
                }
            }
            else if (Tools.IsPicImage(tree.Seeds[picNum], PicMode.OnlyGif))
            {
                if (!isGifDecoderAdded)
                {
                    ImageTools.IO.Decoders.AddDecoder<ImageTools.IO.Gif.GifDecoder>();
                    isGifDecoderAdded = true;
                }
                var stream = store.OpenFile(FilePath, FileMode.Open);
                Bmp = GifAnimater.GetFirstFrameOfGif(stream);
            }
            else throw new ArgumentException("不支持的图片格式:" + FilePath);
#elif !SILVERLIGHT
            Bmp = new BitmapImage(new Uri(FilePath),new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore));
#endif
        }

        public string DeleteCurrent()
        {
            string filePath = FilePath;
            File.Delete(filePath);
            tree.RemoveSeed(picNum);
            if (picChangeMode == ChangeMode.Random)
            {
                randomCache.reset(tree.Seeds.Count);
            }
            return filePath;
        }
#if WINDOWS_PHONE
        private bool isGifDecoderAdded = false;
        private Dictionary<int, BitmapSource> bmpCache = new Dictionary<int, BitmapSource>();
        public bool NeedCache { get; set; }
        private List<int> cacheIndexs = new List<int>();
        private AutoResetEvent bmpLocker = new AutoResetEvent(false);
        private BitmapSource cacheBmp(IsolatedStorageFile store, KeyValuePair<int, string> cachePath, bool isMainThread)
        {
            BitmapSource bmp = null;
            if (bmpCache.ContainsKey(cachePath.Key))
            {
                bmp = bmpCache[cachePath.Key];
            }
            else
            {
                using (var stream = store.OpenFile(cachePath.Value, FileMode.Open))
                {
                    if (isMainThread)
                    {
                        bmp = new BitmapImage();
                        bmp.SetSource(stream);
                    }
                    else
                    {
                        dispatcher.BeginInvoke(() =>
                        {
                            bmp = new BitmapImage();
                            bmp.SetSource(stream);
                            bmpLocker.Set();
                        });
                        bmpLocker.WaitOne();
                    }
                }
                if (!bmpCache.ContainsKey(cachePath.Key))
                {
                    bmpCache.Add(cachePath.Key, bmp);
                }
            }
            cacheIndexs.Remove(cachePath.Key);
            cacheIndexs.Add(cachePath.Key);
            return bmp;
        }
#endif
    }

    public class DirTree
    {
        public string Dir { private set; get; }
        public DirTree Root { private set; get; }
        public List<DirTree> Branchs { private set; get; }
        private List<string> _seeds;
        public List<string> Seeds
        {
            get { return _seeds; }
        }
        public void setSeeds(string[] seedsArray)
        {
            _seeds = new List<string>();
            if (seedsArray != null)
            {
                for (int i = 0; i < seedsArray.Length; i++)
                {
                    _seeds.Add(seedsArray[i]);
                    if (Tools.IsPicImage(seedsArray[i], PicMode.AllSupport))
                    {
                        this.HasPic = true;
                    }
                }
            }
        }
        public bool IsPicPackage { get { return !this.HasBranch && !IsFinalRoot; } }
        public bool IsFinalRoot { get { return Root == null; } }
        public bool HasBranch { get { return Branchs != null && Branchs.Count > 0; } }
        public bool HasSeed { get { return Seeds != null && Seeds.Count > 0; } }
        public bool HasPic { get; private set; }
        public DirTree(string dirPath, DirTree root = null, string[] seeds = null)
        {
            this.Dir = dirPath;
            this.Root = root;
            setSeeds(seeds);
        }
        public void AddBranch(DirTree branch)
        {
            if (Branchs == null)
            {
                Branchs = new List<DirTree>();
            }
            Branchs.Add(branch);
        }
        public bool RemoveSeed(int seedIndex)
        {
            if (_seeds.Count > seedIndex && seedIndex >= 0)
            {
                _seeds.RemoveAt(seedIndex);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class DelayListener
    {
        private int delay;
        public bool? IsTimeOut { get; private set; }

        public DelayListener(int delay)
        {
            this.delay = delay;
        }

        public void Start(Dispatcher dispatcher, Action toDo)
        {
            if (IsTimeOut == null)
            {
                IsTimeOut = false;
                Thread.Sleep(delay);
                dispatcher.BeginInvoke((Action)(() =>
                {
                    if (IsTimeOut == false)
                    {
                        toDo();
                    }
                    IsTimeOut = null;
                }));
            }
        }

        public void TimeOut()
        {
            if (IsTimeOut == false)
            {
                IsTimeOut = true;
            }
        }
    }

    public class RandomCache
    {
        private int index;
        private List<int> cache = new List<int>();
        private int length;
        public RandomCache(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentException("无法建立长度小于1的随机缓存");
            }
            this.length = length;
            reset();
        }
        public int ChangeIndex(int changeNum)
        {
            return cache[index = (index + changeNum + length) % length];
        }
        public int reset()
        {
            var cache0 = new List<int>();
            var random = new Random();
            if (cache.Count != length)
            {
                cache.Clear();
                for (int i = 0; i < length; i++)
                {
                    cache0.Add(i);
                }
            }
            else
            {
                var tmp = cache;
                cache = cache0;
                cache0 = tmp;
            }
            while (cache0.Count > 0)
            {
                int index = random.Next(cache0.Count);
                cache.Add(cache0[index]);
                cache0.RemoveAt(index);
            }
            return cache[0];
        }
        public int reset(int length)
        {
            if (length <= 0)
            {
                throw new ArgumentException("无法建立长度小于1的随机缓存");
            }
            this.length = length;
            return reset();
        }
    }
    
    public enum ChangeMode
    {
        Sequence, Random
    }

    public class PathnameComparer : IComparer<string>
    {
        public virtual int Compare(string x, string y)
        {
            if (x == null || y == null) throw new ArgumentNullException("尝试比较空指针:" + x + ";" + y);
            x = Path.GetFileName(x);
            y = Path.GetFileName(y);
            if (x.Trim() == y.Trim() || (x = x.Trim()).Length == 0 || (y = y.Trim()).Length == 0)
            {
                if (x.Length == y.Length) return x.CompareTo(y);
                else return x.Length - y.Length;
            }
            int xLeftNumCount = 0, xRightNumCount = 0, yLeftNumCount = 0, yRightNumCount = 0;
            for (; xLeftNumCount < x.Length && Tools.IsNumber(x[xLeftNumCount]); xLeftNumCount++) ;
            if (xLeftNumCount > 0 && xLeftNumCount < x.Length && Tools.IsLetter(x[xLeftNumCount - 1])) xLeftNumCount = 0;
            for (; xLeftNumCount != x.Length && Tools.IsNumber(x[x.Length - 1 - xRightNumCount]); xRightNumCount++) ;
            for (; yLeftNumCount < y.Length && Tools.IsNumber(y[yLeftNumCount]); yLeftNumCount++) ;
            if (yLeftNumCount > 0 && yLeftNumCount < y.Length && Tools.IsLetter(y[yLeftNumCount - 1])) yLeftNumCount = 0;
            for (; yLeftNumCount != y.Length && Tools.IsNumber(y[y.Length - 1 - yRightNumCount]); yRightNumCount++) ;
            string xNotNumStr = x.Substring(xLeftNumCount, x.Length - xLeftNumCount - xRightNumCount);
            string yNotNumStr = y.Substring(yLeftNumCount, y.Length - yLeftNumCount - yRightNumCount);
            if (true || xNotNumStr == yNotNumStr)
            {
                string xLeftNum = x.Substring(0, xLeftNumCount);
                string xRightNum = x.Substring(x.Length - xRightNumCount);
                string yLeftNum = y.Substring(0, yLeftNumCount);
                string yRightNum = y.Substring(y.Length - yRightNumCount);
                if ((xLeftNumCount == 0 || yLeftNumCount == 0) && xLeftNumCount != yLeftNumCount)
                {
                    return xLeftNumCount - yLeftNumCount;
                }
                int fixLeftLength = Math.Max(xLeftNumCount, yLeftNumCount);
                for (int i = 0; i < fixLeftLength; i++)
                {
                    char xLeftChar = xLeftNumCount + i - fixLeftLength < 0 ? '0' : xLeftNum[xLeftNumCount + i - fixLeftLength],
                            yLeftChar = yLeftNumCount + i - fixLeftLength < 0 ? '0' : yLeftNum[yLeftNumCount + i - fixLeftLength];
                    if (xLeftChar != yLeftChar)
                    {
                        return xLeftChar - yLeftChar;
                    }
                }
                if (xLeftNumCount != yLeftNumCount)
                {
                    return xLeftNumCount - yLeftNumCount;
                }

                if ((xRightNumCount == 0 || yRightNumCount == 0) && xRightNumCount != yRightNumCount)
                {
                    return xRightNumCount - yRightNumCount;
                }
                int fixRightLength = Math.Max(xRightNumCount, yRightNumCount);
                for (int i = 0; i < fixRightLength; i++)
                {
                    char xRightChar = xRightNumCount + i - fixRightLength < 0 ? '0' : xRightNum[xRightNumCount + i - fixRightLength],
                            yRightChar = yRightNumCount + i - fixRightLength < 0 ? '0' : yRightNum[yRightNumCount + i - fixRightLength];
                    if (xRightChar != yRightChar)
                    {
                        return xRightChar - yRightChar;
                    }
                }
                if (xRightNumCount != yRightNumCount)
                {
                    return xRightNumCount - yRightNumCount;
                }
            }
            if (xNotNumStr != string.Empty && yNotNumStr != string.Empty)
            {
            }
            return xNotNumStr.CompareTo(yNotNumStr);
        }

        //public static void GetMaxCommonStr(string x, string y, out int xCommonBegin, out int yCommonBegin, out int maxCommonLength)
        //{

        //    xCommonBegin = yCommonBegin = maxCommonLength = 0;
        //}

        //public static int EditDistance(string str1, string str2)
        //{
        //    char[] strs1 = str1.ToCharArray();
        //    char[] strs2 = str2.ToCharArray();
        //    int[,] dist = new int[strs1.Length + 1, strs2.Length + 1];
        //    int i, j, tmp;
        //    for (i = 0; i <= strs1.Length; i++) dist[i, 0] = 0;
        //    for (j = 0; j <= strs2.Length; j++) dist[0, i] = 0;
        //    for (i = 1; i <= strs1.Length; i++)
        //    {
        //        for (j = 1; j <= strs2.Length; j++)
        //        {
        //            if (strs1[i - 1] == strs2[j - 1])
        //            {
        //                tmp = dist[i - 1, j - 1] + 1;
        //            }
        //            else
        //            {
        //                tmp = dist[i, j - 1];
        //                if (dist[i - 1, j] > tmp) tmp = dist[i - 1, j];
        //            }
        //            dist[i, j] = tmp;
        //        }
        //    }
        //    tmp = dist[strs1.Length, strs2.Length];
        //    if (strs1.Length > strs2.Length)
        //    {
        //        return strs1.Length - tmp;
        //    }
        //    else
        //    {
        //        return strs2.Length - tmp;
        //    }
        //}
        public static readonly PathnameComparer Instance = new PathnameComparer();
    }

    public class PicnameComparer : PathnameComparer
    {
        public override int Compare(string x, string y)
        {
            x = Path.GetFileNameWithoutExtension(x);
            y = Path.GetFileNameWithoutExtension(y);
            return base.Compare(x, y);
        }
        public new static readonly PicnameComparer Instance = new PicnameComparer();
    }

    public class RandomComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return x == y ? 0 : random.Next(3) - 1;
        }
        private Random random = new Random();
        public static readonly RandomComparer Instance = new RandomComparer();
    }

    public class Tools
    {
        public static bool IsPicImage(string filePath, PicMode picMode)
        {
            if (filePath == null) throw new ArgumentException("尝试判断空指针为PicImage");
            switch (picMode)
            {
                case PicMode.AllSupport:
                case PicMode.GifAsJpg:
                    return filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
#if !SILVERLIGHT
                        || filePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
#endif
                        || filePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                        ;
                case PicMode.StaticPic:
                    return filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || filePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
#if !SILVERLIGHT
                        || filePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
#endif
                        ;
                case PicMode.OnlyGif:
                    return filePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
                default:
                    throw new ArgumentException("PicMode参数不正确:" + picMode);
            }
        }
        public static bool IsPicImageContainer(DirTree tree, PicMode picMode)
        {
            foreach (var seed in tree.Seeds)
            {
                if (Tools.IsPicImage(seed, picMode))
                {
                    return true;
                }
            }
            return false;
        }
        public static bool IsZipPackage(string filePath)
        {
            if (filePath == null) throw new ArgumentException("尝试判断空指针为ZipPackage");
            return filePath.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }
        public static bool IsNumber(char c)
        {
            return c >= '0' && c <= '9';
        }
        public static bool IsLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
        public static string ToCachePath(string filePath, string picPath, string cachePath)
        {
            if (filePath.StartsWith(picPath) && filePath.Length > picPath.Length + 1 && filePath[picPath.Length] == Path.DirectorySeparatorChar)
            {
                return Path.Combine(cachePath, filePath.Substring(picPath.Length + 1));
            }
            else
            {
                throw new ArgumentException("文件夹无法转换为缓存目录>picPath:" + filePath + "> >" + picPath);
            }
        }
        public static bool InOrIsSeqPath(string path)
        {
            return "comic" == path || path.StartsWith("comic" + Path.DirectorySeparatorChar) || "manga" == path || path.StartsWith("manga" + Path.DirectorySeparatorChar);
        }
#if WINDOWS_PHONE
        public static DirTree GetTree(string picPath)
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.DirectoryExists(picPath))
                {
                    store.CreateDirectory(picPath);
                }
                DirTree tree = new DirTree(picPath);
                recursivePath(picPath, tree, store);
                return tree;
            }
        }
        private static void recursivePath(string path, DirTree tree, IsolatedStorageFile store)
        {
            var seedsArray = store.GetFileNames(Path.Combine(path, "*.*"));
            if (InOrIsSeqPath(path))
            {
                Array.Sort(seedsArray, PicnameComparer.Instance);
            }
            else
            {
                Array.Sort(seedsArray, RandomComparer.Instance);
            }
            tree.setSeeds(seedsArray);
            var dirs = store.GetDirectoryNames(Path.Combine(path, "*"));
            Array.Sort(dirs, PathnameComparer.Instance);
            if (dirs.Length > 0)
            {
                foreach (string dir in dirs)
                {
                    var nextPath = Path.Combine(path, dir);
                    var branchTree = new DirTree(dir, tree);
                    tree.AddBranch(branchTree);
                    recursivePath(nextPath, branchTree, store);
                }
            }
        }
#endif
    }

    public enum PicMode
    {
        AllSupport, StaticPic, OnlyGif, GifAsJpg
    }

    public partial class Show
    {
        private DispatcherTimer autoModeTimer;
        private void autoMode_Checked(object sender, RoutedEventArgs e)
        {
            if (autoModeTimer == null)
            {
                autoModeTimer = new DispatcherTimer();
                autoModeTimer.Tick += (s, evt) =>
                {
                    picForward_Click();
                    autoModeTimer.Interval = TimeSpan.FromMilliseconds(autoSlider.Value * 400 + 300);
                };
            }
            autoModeTimer.Start();
        }
        private void autoMode_Unchecked(object sender, RoutedEventArgs e)
        {
            autoModeTimer.Stop();
        }
        private void randomMode_Checked(object sender, RoutedEventArgs e)
        {
            package.picChangeMode = ChangeMode.Random;
        }
        private void randomMode_Unchecked(object sender, RoutedEventArgs e)
        {
            package.picChangeMode = ChangeMode.Sequence;
        }
        private void autoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (autoModeTimer != null && autoModeTimer.IsEnabled)
            {
                autoModeTimer.Stop();
                autoModeTimer.Start();
            }
        }
        private void picForward_Click(object sender = null, RoutedEventArgs e = null)
        {
            //double hOffset = tbShow.Width / Math.Ceiling(tbShow.Width / workWidth),
            //    vOffset = tbShow.Height / Math.Ceiling(tbShow.Height / workHeight);
            //if (rateNum == 1 && (tbShow.Height - workHeight - imageScrollViewer.VerticalOffset) / tbShow.Height > 0.1)
            //    moveScrollTo(imageScrollViewer.HorizontalOffset, imageScrollViewer.VerticalOffset + vOffset);
            //else if (rateNum == 1 && package.picChangeMode == ChangeMode.Sequence && imageScrollViewer.HorizontalOffset / tbShow.Width > 0.1)
            //    moveScrollTo(imageScrollViewer.HorizontalOffset - hOffset, imageScrollViewer.VerticalOffset);
            //else if (rateNum == 1 && package.picChangeMode == ChangeMode.Random && (tbShow.Width - workWidth - imageScrollViewer.HorizontalOffset) / tbShow.Width > 0.1)
            //    moveScrollTo(imageScrollViewer.HorizontalOffset + hOffset, imageScrollViewer.VerticalOffset);
            //else
            changePic(1);
        }
        private void moveScrollTo(double x, double y)
        {
            imageScrollViewer.UpdateLayout();
            imageScrollViewer.ScrollToHorizontalOffset(x);
            imageScrollViewer.ScrollToVerticalOffset(y);
        }
    }
#if WINDOWS_PHONE
    public class GifAnimater
    {
        private System.Windows.Controls.Image _image;
        private DispatcherTimer _animationTimer;
        private List<KeyValuePair<int, ImageSource>> _frames;
        private int _animationFrameIndex;
        public bool IsLoadingImage { get; private set; }
        private Dispatcher dispatcher;
        public bool PlayOnce { get; set; }
        public delegate void AnimateLoadedHandler(ExtendedImage image);
        public event AnimateLoadedHandler AnimateLoaded;
        public event Action AnimateLoadFail;
        public event Action FirstFrameLoaded;

        public GifAnimater(System.Windows.Controls.Image imageControl, Dispatcher dispatcher)
        {
            this._image = imageControl;
            this.dispatcher = dispatcher;
            _animationTimer = new DispatcherTimer();
            _animationTimer.Tick += timer_Tick;
        }

        private bool isFirstFrame;
        private void timer_Tick(object sender, EventArgs e)
        {
            if (_animationFrameIndex < _frames.Count)
            {
                var currentFrame = _frames[_animationFrameIndex];

                if (currentFrame.Value != null)
                {
                    if (_image != null)
                    {
                        _image.Source = currentFrame.Value;
                    }

                    _animationTimer.Interval = new TimeSpan(0, 0, 0, 0, currentFrame.Key * 10);
                    _animationFrameIndex++;

                    if (_animationFrameIndex == _frames.Count)
                    {
                        if (PlayOnce)
                        {
                            Stop();
                        }

                        _animationFrameIndex = 0;
                    }
                }
                if (isFirstFrame)
                {
                    isFirstFrame = false;
                    FirstFrameLoaded();
                }
            }
        }
        public void Start()
        {
            if (_frames != null)
            {
                isFirstFrame = true;

                _animationTimer.Start();
            }
        }
        public void Stop()
        {
            _animationFrameIndex = 0;

            _animationTimer.Stop();
        }
        public static BitmapSource GetFirstFrameOfGif(Stream stream)
        {
            var bitmapLocker = new AutoResetEvent(false);
            BitmapSource bmp = null;
            var animatedBmp = new ExtendedImage();
            animatedBmp.LoadingCompleted += (ob, e) =>
            {
                bitmapLocker.Set();
            };
            animatedBmp.LoadingFailed += (ob, e) =>
            {
                bitmapLocker.Set();
            };
            animatedBmp.SetSource(stream);
            bitmapLocker.WaitOne();
            bmp = animatedBmp.Frames[0].ToBitmap();
            return bmp;
        }
        public void LoadImage(Stream stream)
        {
            var animatedBmp = new ExtendedImage();
            animatedBmp.LoadingCompleted += (ob, e) =>
            {
                LoadImage(animatedBmp);
            };
            animatedBmp.LoadingFailed += (ob, e) =>
            {
                dispatcher.BeginInvoke(() =>
                {
                    AnimateLoadFail();
                });
            };
            animatedBmp.SetSource(stream);
        }
        private void LoadImage(ExtendedImage image)
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                if (!IsLoadingImage)
                {
                    IsLoadingImage = true;

                    if (image.IsFilled)
                    {
                        if (image.IsAnimated)
                        {
                            var __frames = new List<KeyValuePair<int, ImageSource>>();
                            var bitmapLocker = new AutoResetEvent(false);
                            foreach (ImageBase frame in image.Frames)
                            {
                                if (frame != null && frame.IsFilled)
                                {
                                    ImageSource bitmap = null;
                                    dispatcher.BeginInvoke(() =>
                                    {
                                        bitmap = frame.ToBitmap();
                                        bitmapLocker.Set();
                                    });
                                    bitmapLocker.WaitOne();
                                    __frames.Add(new KeyValuePair<int, ImageSource>(frame.DelayTime, bitmap));
                                }
                            }
                            dispatcher.BeginInvoke(() =>
                            {
                                Stop();
                                _frames = __frames;
                                Start();
                                AnimateLoaded(image);
                                IsLoadingImage = false;
                            });
                        }
                    }
                }
            });
        }

    }
#elif !SILVERLIGHT
    public class GIFImageControl : System.Windows.Controls.Image
    {
        delegate void OnFrameChangedDelegate();
        private Bitmap m_Bitmap;
        public string Path { get; set; }
        BitmapSource bitmapSource;
        public void StopAnimate()
        {
            ImageAnimator.StopAnimate(m_Bitmap, OnFrameChanged);
        }

        public void AnimatedImageControl(string path)
        {
            Path = path;
            m_Bitmap = (Bitmap)System.Drawing.Image.FromFile(path);
            ImageAnimator.Animate(m_Bitmap, OnFrameChanged);
            OnFrameChangedInMainThread();
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new OnFrameChangedDelegate(OnFrameChangedInMainThread));
        }

        private void OnFrameChangedInMainThread()
        {
            try
            {
                ImageAnimator.UpdateFrames();
                Source = GetBitmapSource();
                InvalidateVisual();
            }
            catch (Exception) { }
        }

        private BitmapSource GetBitmapSource()
        {
            IntPtr inptr = m_Bitmap.GetHbitmap();
            bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(inptr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(inptr);
            return bitmapSource;
        }

        [System.Runtime.InteropServices.DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);
    }
#endif
}
