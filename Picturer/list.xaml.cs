using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;

namespace Picturer
{
    public partial class list : PhoneApplicationPage
    {
        public list()
        {
            InitializeComponent();

            if (MainPage.tree.HasSeed && MainPage.tree.HasPic)
            {
                this.package = new Package(MainPage.tree, ChangeMode.Sequence, this.Dispatcher);

                TitleText.Text = MainPage.tree.Dir;
                loadProgress.IsIndeterminate = true;
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    loadPic();
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        loadProgress.IsIndeterminate = false;
                    });
                });
            }
            else
            {
                throw new ArgumentNullException(MainPage.tree.Dir + " is empty");
            }
        }

        private Package package;

        private bool loadEnded = false;
        private const double width = 120;
        private const double height = 120;
        private void loadPic()
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                var streamLocker = new AutoResetEvent(false);
                List<int> loadedIndies = new List<int>();
                Thickness thickness = new Thickness(30, 15, 0, 15);
                for (int picIndex = package.ChangePicNum(0, PicMode.AllSupport), marginIndex = 0; !loadEnded && !loadedIndies.Contains(picIndex); picIndex = package.ChangePicNum(1, PicMode.AllSupport), marginIndex++)
                {
                    loadedIndies.Add(picIndex);
                    if (DeviceStatus.ApplicationMemoryUsageLimit - DeviceStatus.ApplicationCurrentMemoryUsage > 1024 * 1024 * 10)
                    {
                        Stream stream;
                        string cachedPath = Tools.ToCachePath(package.FilePath, MainPage.picPath, MainPage.cachePath);
                        string path = Path.GetDirectoryName(cachedPath);
                        if (!store.DirectoryExists(path))
                        {
                            store.CreateDirectory(path);
                        }
                        WriteableBitmap wb = null;
                        if (!store.FileExists(cachedPath))
                        {
                            stream = store.CreateFile(cachedPath);
                            this.Dispatcher.BeginInvoke(() =>
                            {
                                try
                                {
                                    var bmp = package.SetAndGetPic(PicMode.AllSupport, store);
                                    wb = new WriteableBitmap(bmp);
                                }
                                catch (Exception)
                                {
                                    TitleText.Text = package.FilePath + "文件读取失败";
                                }
                                finally
                                {
                                    streamLocker.Set();
                                }
                            });
                            //System.Diagnostics.Debug.WriteLine(DateTime.Now + " " + filePath + " cached");
                            streamLocker.WaitOne();
                            if (wb != null)
                            {
                                double present = Math.Min(width / wb.PixelWidth, height / wb.PixelHeight);
                                int wbWidth = (int)(wb.PixelWidth * present),
                                    wbHeight = (int)(wb.PixelHeight * present);
                                wbWidth = wbWidth < 1 ? 1 : wbWidth;
                                wbHeight = wbHeight < 1 ? 1 : wbHeight;
                                wb.SaveJpeg(stream, wbWidth, wbHeight, 0, 100);
                            }
                        }
                        else
                        {
                            stream = store.OpenFile(cachedPath, FileMode.Open);
                        }
                        bool error = false;
                        this.Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.SetSource(stream);
                                var image = new Image();
                                image.Width = width;
                                image.Height = height;
                                image.Margin = thickness;
                                image.Tag = picIndex;
                                image.Tap += imageTap;
                                image.Hold += imageHold;
                                image.Source = bmp;
                                picList.Items.Add(image);

                                //TitleText.Text = DeviceStatus.ApplicationCurrentMemoryUsage / 1024 / 1024.0 + "M/" + DeviceStatus.ApplicationMemoryUsageLimit / 1024 / 1024.0 + "M";
                            }
                            catch (Exception)
                            {
                                error = true;
                                TitleText.Text = cachedPath + "读取异常,缓存删除";
                            }
                            finally
                            {
                                streamLocker.Set();
                            }
                        });
                        streamLocker.WaitOne();
                        stream.Dispose();
                        if (error)
                        {
                            store.DeleteFile(cachedPath);
                        }
                    }
                    else
                    {
                        this.Dispatcher.BeginInvoke(() =>
                        {
                            TitleText.Text = "内存即将溢出 " + DeviceStatus.ApplicationCurrentMemoryUsage / 1024 / 1024.0 + "M/" + DeviceStatus.ApplicationMemoryUsageLimit / 1024 / 1024.0 + "M";
                        });
                        break;
                    }
                }
            }
            loadEnded = true;
        }

        private void PhoneApplicationPage_BackKeyPress(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!loadEnded)
            {
                loadEnded = true;
                e.Cancel = true;
            }
        }

        private void imageTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/show.xaml?index=" + (sender as Image).Tag + "&randomMode=" + (randomMode.IsChecked == true), UriKind.Relative));
        }

        private void imageHold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ContextMenuService.SetContextMenu(sender as DependencyObject, picMenu);
            picMenu.Tag = sender;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Image toDel = ((sender as MenuItem).Parent as ContextMenu).Tag as Image;
            int picIndex = (int)toDel.Tag;
            package.SetPicNum(picIndex);
            string filePath = package.FilePath;
            string cachedPath = Tools.ToCachePath(filePath, MainPage.picPath, MainPage.cachePath);
            (toDel.Parent as ListBox).Items.Remove(toDel);
            package.Tree.RemoveSeed(picIndex);
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                store.DeleteFile(filePath);
                store.DeleteFile(cachedPath);
            }
        }

        private void picMenu_Closed(object sender, RoutedEventArgs e)
        {
            ContextMenuService.SetContextMenu((sender as ContextMenu).Tag as DependencyObject, null);
        }
    }
}