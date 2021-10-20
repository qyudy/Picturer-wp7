using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Controls;

namespace Picturer
{
    public partial class show : PhoneApplicationPage
    {
        public show()
        {
            InitializeComponent();
        }

        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            //TitleText.Text = MainPage.tree.Dir;
            var query = this.NavigationContext.QueryString;
            int index = query.ContainsKey("index") ? int.Parse(query["index"]) : 0;
            var picChangeMode = query.ContainsKey("randomMode") && bool.Parse(query["randomMode"]) ? ChangeMode.Random : ChangeMode.Sequence;
            package = new Package(MainPage.tree, picChangeMode, this.Dispatcher);
            //package.NeedCache = true;
            initAnimater();
            changePic(index);

            randomMode.IsChecked = picChangeMode == ChangeMode.Random;
        }

        private Package package;
        private int rateNum = 0;
        private double workWidth = Application.Current.Host.Content.ActualWidth,
                       workHeight = Application.Current.Host.Content.ActualHeight;
        private double[] rates = new double[3];
        private void fixSize(int rateNum)
        {
            double uniformRate = Math.Min(workWidth / (tbShow.Source as BitmapSource).PixelWidth, workHeight / (tbShow.Source as BitmapSource).PixelHeight);
            double fillRate = Math.Max(workWidth / (tbShow.Source as BitmapSource).PixelWidth, workHeight / (tbShow.Source as BitmapSource).PixelHeight);
            rates[2] = 1;
            rates[1] = Math.Min(fillRate, 1);
            rates[0] = Math.Min(uniformRate, rates[1]);
            fixRate(rateNum);
            fixPic();
        }
        private int changeRate(int rateChange)
        {
            rateChange = rateChange > 0 ? 1 : rateChange < 0 ? -1 : 0;
            if (rateChange == 0) return this.rateNum;
            int rateNum = (this.rateNum + rateChange + rates.Length) % rates.Length;
            if (fixRate(rateNum) != rateNum && this.rateNum != 0)
            {
                return fixRate((this.rateNum + rateChange * 2 + rates.Length) % rates.Length);
            }
            else return this.rateNum;
        }
        private int fixRate(int rateNum)
        {
            while (rateNum > 0 && rates[rateNum] == rates[rateNum - 1])
            {
                rateNum--;
            }
            return this.rateNum = rateNum;
        }
        private void fixPic()
        {
            tbShow.Width = (tbShow.Source as BitmapSource).PixelWidth * rates[rateNum];
            tbShow.Height = (tbShow.Source as BitmapSource).PixelHeight * rates[rateNum];
        }
        private void resetScroll()
        {
            switch (rateNum)
            {
                case 0:
                    break;
                case 1:
                    switch (package.picChangeMode)
                    {
                        case ChangeMode.Sequence:
                            moveScrollTo(tbShow.Width - workWidth, 0);
                            break;
                        case ChangeMode.Random:
                            moveScrollTo(0, 0);
                            break;
                    }
                    break;
                case 2:
                    moveScrollTo((tbShow.Width - workWidth) / 2, (tbShow.Height - workHeight) / 2);
                    break;
            }
        }

        private DelayListener tbShowTapListener = new DelayListener(150);
        private void tbShow_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            tbShowTapListener.Start(this.Dispatcher, () =>
            {
                if (autoModeTimer != null && autoModeTimer.IsEnabled)
                {
                    autoMode.IsChecked = false;
                }
                else
                {
                    picForward_Click();
                }
            });
        }
        private void tbShow_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            tbShowTapListener.TimeOut();
            int pastRateNum = rateNum;
            changeRate(1);
            fixPic();
            double x = e.GetPosition(tbShow).X * rates[rateNum] / rates[pastRateNum] - workWidth / 2;
            double y = e.GetPosition(tbShow).Y * rates[rateNum] / rates[pastRateNum] - workHeight / 2;
            moveScrollTo(x, y);
        }

        //private void animationMoveScroll(double horizontalOffset,double verticalOffset)
        //{
        //    if (horizontalOffset * verticalOffset != 0) return;
        //    double offsetOnetime = horizontalOffset + verticalOffset > 0 ? 1 : -1;
        //    int millesecondOnetime = 10;
        //    ThreadPool.QueueUserWorkItem((o) =>
        //    {
        //        while ((horizontalOffset + verticalOffset) * offsetOnetime > 0)
        //        {
        //            Thread.Sleep(millesecondOnetime);
        //            this.Dispatcher.BeginInvoke(() =>
        //            {
        //                moveScrollTo(imageScrollViewer.HorizontalOffset + horizontalOffset == 0 ? 0 : offsetOnetime, imageScrollViewer.VerticalOffset + verticalOffset == 0 ? 0 : offsetOnetime);
        //            });
        //            if (horizontalOffset != 0) horizontalOffset -= offsetOnetime;
        //            if (verticalOffset != 0) verticalOffset -= offsetOnetime;
        //        }
        //    });
        //}

        private void picBack_Click(object sender, RoutedEventArgs e)
        {
            if (!is_picBack_Hold)
            {
                changePic(-1);
                autoMode.IsChecked = false;
            }
            else
            {
                is_picBack_Hold = false;
            }
        }
        private GifAnimater gifAnimater;
        private void initAnimater()
        {
            ImageTools.IO.Decoders.AddDecoder<ImageTools.IO.Gif.GifDecoder>();
            gifAnimater = new GifAnimater(tbShow, this.Dispatcher);
            gifAnimater.AnimateLoaded += (image) =>
            {
                TitleText.Text = Path.GetFileName(package.FilePath);
                progress.IsIndeterminate = false;
            };
            gifAnimater.AnimateLoadFail += () =>
            {
                TitleText.Text = Path.GetFileName(package.FilePath) + " load fail";
                progress.IsIndeterminate = false;
            };
            gifAnimater.FirstFrameLoaded += () =>
            {
                fixSize(2);
                resetScroll();
            };
        }
        private void changePic(int picNumChange)
        {
            if (gifAnimater == null || !gifAnimater.IsLoadingImage)
            {
                package.ChangePicNum(picNumChange, PicMode.AllSupport);
                progress.IsIndeterminate = true;
                if (Tools.IsPicImage(package.FilePath, PicMode.OnlyGif) && MainPage.picPath != MainPage.cachePath)
                {
                    ThreadPool.QueueUserWorkItem((o) =>
                    {
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            var stream = store.OpenFile(package.FilePath, FileMode.Open);
                            gifAnimater.LoadImage(stream);
                        }
                    });
                }
                else if (Tools.IsPicImage(package.FilePath, PicMode.StaticPic) || MainPage.picPath == MainPage.cachePath)
                {
                    if (gifAnimater != null) gifAnimater.Stop();
                    try
                    {
                        tbShow.Source = package.SetAndGetPic(PicMode.GifAsJpg);
                        fixSize(rateNum);
                        resetScroll();
                        TitleText.Text = Path.GetFileName(package.FilePath); //DeviceStatus.ApplicationCurrentMemoryUsage / 1024 / 1024.0 + "M/" + DeviceStatus.ApplicationMemoryUsageLimit / 1024 / 1024.0 + "M";
                    }
                    catch (Exception e)
                    {
                        TitleText.Text = Path.GetFileName(package.FilePath) + ":error:" + e.Message;
                    }
                    progress.IsIndeterminate = false;
                }
                else TitleText.Text = "不支持的文件格式:" + Path.GetFileName(package.FilePath);
            }
        }
        private void picBack_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            is_picBack_Hold = true;
            package.reset();
            changePic(0);
        }
        private bool is_picBack_Hold = false;

        private void orientationChange_Click(object sender, RoutedEventArgs e)
        {
            switch (this.SupportedOrientations)
            {
                case SupportedPageOrientation.Portrait:
                    this.SupportedOrientations = SupportedPageOrientation.Landscape;
                    workWidth = Application.Current.Host.Content.ActualHeight;
                    workHeight = Application.Current.Host.Content.ActualWidth;
                    break;
                case SupportedPageOrientation.Landscape:
                    this.SupportedOrientations = SupportedPageOrientation.Portrait;
                    workWidth = Application.Current.Host.Content.ActualWidth;
                    workHeight = Application.Current.Host.Content.ActualHeight;
                    break;
            }
            fixSize(rateNum);
            resetScroll();
        }

        //private void PhoneApplicationPage_OrientationChanged(object sender, OrientationChangedEventArgs e)
        //{
        //    switch (this.Orientation)
        //    {
        //        case PageOrientation.PortraitUp:
        //            orientationChange.HorizontalAlignment = HorizontalAlignment.Left;
        //            orientationChange.VerticalAlignment = VerticalAlignment.Top;
        //            picBack.HorizontalAlignment = HorizontalAlignment.Left;
        //            picBack.VerticalAlignment = VerticalAlignment.Bottom;
        //            //randomMode.HorizontalAlignment = HorizontalAlignment.Right;
        //            //randomMode.VerticalAlignment = VerticalAlignment.Top;
        //            autoMode.HorizontalAlignment = HorizontalAlignment.Right;
        //            autoMode.VerticalAlignment = VerticalAlignment.Bottom;
        //            //autoSlider.Height = 270;
        //            //autoSlider.Width = 100;
        //            //autoSlider.Margin = new Thickness(400,460,-20,70);
        //            //autoSlider.Orientation = System.Windows.Controls.Orientation.Vertical;
        //            break;
        //        case PageOrientation.LandscapeLeft:
        //            orientationChange.HorizontalAlignment = HorizontalAlignment.Left;
        //            orientationChange.VerticalAlignment = VerticalAlignment.Bottom;
        //            picBack.HorizontalAlignment = HorizontalAlignment.Right;
        //            picBack.VerticalAlignment = VerticalAlignment.Bottom;
        //            //randomMode.HorizontalAlignment = HorizontalAlignment.Left;
        //            //randomMode.VerticalAlignment = VerticalAlignment.Top;
        //            autoMode.HorizontalAlignment = HorizontalAlignment.Right;
        //            autoMode.VerticalAlignment = VerticalAlignment.Top;
        //            //autoSlider.Height = 100;
        //            //autoSlider.Width = 270;
        //            //autoSlider.Margin = new Thickness(460, -20, 70, 400);
        //            //autoSlider.Orientation = System.Windows.Controls.Orientation.Horizontal;
        //            break;
        //        case PageOrientation.LandscapeRight:
        //            orientationChange.HorizontalAlignment = HorizontalAlignment.Right;
        //            orientationChange.VerticalAlignment = VerticalAlignment.Top;
        //            picBack.HorizontalAlignment = HorizontalAlignment.Left;
        //            picBack.VerticalAlignment = VerticalAlignment.Top;
        //            //randomMode.HorizontalAlignment = HorizontalAlignment.Right;
        //            //randomMode.VerticalAlignment = VerticalAlignment.Bottom;
        //            autoMode.HorizontalAlignment = HorizontalAlignment.Left;
        //            autoMode.VerticalAlignment = VerticalAlignment.Bottom;
        //            //autoSlider.Height = 100;
        //            //autoSlider.Width = 270;
        //            //autoSlider.Margin = new Thickness(70, 400, 460, -20);
        //            //autoSlider.Orientation = System.Windows.Controls.Orientation.Horizontal;
        //            break;
        //    }
        //}

        //private double[] tmpScalePersent = new double[2]{1,1};
        //private void tbShow_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        //{
        //    Point scale = e.DeltaManipulation.Scale;
        //    Point origin = e.ManipulationOrigin;
        //    //System.Diagnostics.Debug.WriteLine(DateTime.Now + " scale.X:" + scale.X + " scale.Y:" + scale.Y + " \r\n ");
        //    //System.Diagnostics.Debug.WriteLine(DateTime.Now + " origin.X:" + origin.X + " origin.Y:" + origin.Y + " \r\n ");
        //    if (scale.X > 0&&scale.Y > 0)
        //    {
        //        double scalePersent = Math.Sqrt(scale.X *  scale.Y);
        //        if ((tmpScalePersent[0] >= 1 && tmpScalePersent[1] >= 1 && scalePersent >= 1) || (tmpScalePersent[0] <= 1 && tmpScalePersent[1] <= 1 && scalePersent <= 1))
        //        {
        //            if (persent * scalePersent > persent[0] && persent * scalePersent < persent[2])
        //            {
        //                fixSize(persent * scalePersent);
        //                double x = (imageScrollViewer.HorizontalOffset + origin.X) * scalePersent - origin.X;
        //                double y = (imageScrollViewer.VerticalOffset + origin.Y) * scalePersent - origin.Y;
        //                moveScrollTo(x, y);
        //            }
        //            if (persent - persent[0] < 0.01)
        //            {
        //                fixSize(persent[0]);
        //            }
        //            if (maxPersent - persent[2] < 0.01)
        //            {
        //                fixSize(persent[2]);
        //            }
        //        }
        //        tmpScalePersent[0] = tmpScalePersent[1];
        //        tmpScalePersent[1] = scalePersent;
        //    }
        //}

        //private void tbShow_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        //{
        //    ThreadPool.QueueUserWorkItem((o) =>
        //    {
        //        Thread.Sleep(1000);
        //        this.Dispatcher.BeginInvoke(() =>
        //        {
        //            imageScrollViewer.ScrollToHorizontalOffset(imageScrollViewer.HorizontalOffset);
        //            imageScrollViewer.ScrollToVerticalOffset(imageScrollViewer.VerticalOffset);
        //            imageScrollViewer.UpdateLayout();
        //        });
        //    });
        //    if (scrollCountDowner == null)
        //    {
        //        scrollCountDowner = (o) =>
        //        {
        //            countDown = 3;
        //            do
        //            {
        //                Thread.Sleep(200);
        //                countDown--;
        //            } while (countDown < 1);
        //            this.Dispatcher.BeginInvoke(() =>
        //            {
        //                imageScrollViewer.ScrollToHorizontalOffset(imageScrollViewer.HorizontalOffset);
        //                imageScrollViewer.ScrollToVerticalOffset(imageScrollViewer.VerticalOffset);
        //                imageScrollViewer.UpdateLayout();
        //            });
        //            scrollCountDowner = null;
        //        };
        //        ThreadPool.QueueUserWorkItem(scrollCountDowner);
        //    }
        //    else
        //    {
        //        countDown = 3;
        //    }
        //}

        //private WaitCallback scrollCountDowner;
        //private int countDown;
    }
}