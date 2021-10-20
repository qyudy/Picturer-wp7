using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using SharpCompress.Reader;
using System.Collections.Generic;

namespace Picturer
{
    public partial class MainPage : PhoneApplicationPage
    {
        // 构造函数
        public MainPage()
        {
            InitializeComponent();

            Instance = this;
        }

        public static DirTree tree { private set; get; }

        public static string picPath { private set; get; }
        public static string cachePath { private set; get; }

        public static MainPage Instance { private set; get; }

        private void resetButtons()
        {
            MainListBox.Items.Clear();
            if (tree.HasBranch)
            {
                foreach (var branch in tree.Branchs)
                {
                    var dirButton = getNewButton();
                    dirButton.Content = branch.Dir;
                    dirButton.Tag = branch;
                    if (branch.IsPicPackage)
                    {
                        dirButton.Tap += picList_Click;
                    }
                    else
                    {
                        dirButton.Tap += dirButton_click;
                    }
                    dirButton.Hold += dirButton_hold;
                    MainListBox.Items.Add(dirButton);
                }
            }
            if (tree.HasSeed)
            {
                if (tree.HasPic)
                {
                    var dirButton = getNewButton();
                    dirButton.Content = tree.Dir + "...图";
                    dirButton.Tag = tree;
                    dirButton.Tap += picList_Click;
                    MainListBox.Items.Add(dirButton);
                }
                foreach (var seed  in tree.Seeds.Values)
                {
                    if (Tools.IsZipPackage(seed))
                    {
                        var dirButton = getNewButton();
                        dirButton.Content = seed;
                        dirButton.Tag = tree;
                        dirButton.Tap += zipButton_click;
                        MainListBox.Items.Add(dirButton);
                    }
                }
            }
        }
        private Button getNewButton()
        {
            var button = new Button();
            button.Margin = new Thickness(-10, -8, -10, -8);
            button.Padding = new Thickness(0, 0, 0, 0);
            button.BorderThickness = new Thickness(0, 0, 0, 0);
            button.MinWidth = 480;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            return button;
        }

        private void picList_Click(object sender, RoutedEventArgs e)
        {
            DirTree tree = (sender as Button).Tag as DirTree;
            if (tree.HasSeed && tree.HasPic)
            {
                MainPage.tree = tree;
                this.NavigationService.Navigate(new Uri("/list.xaml", UriKind.Relative));
            }
            else
            {
                ApplicationTitle.Text = tree.Dir + " is empty of dir and pic";
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    Thread.Sleep(1000);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        ApplicationTitle.Text = picPath;
                    });
                });
            }
        }
        private void zipButton_click(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ContextMenuService.SetContextMenu(sender as DependencyObject, zipMenu);
            zipMenu.Tag = sender;
        }
        private void unzip_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!progress.IsIndeterminate)
            {
                progress.IsIndeterminate = true;
                var button = ((sender as MenuItem).Parent as ContextMenu).Tag as Button;
                string zipPath = Package.getFilePath(button.Content as string, button.Tag as DirTree);
                string zipName = Path.GetFileName(zipPath);
                resultText.Text = zipName + " unziping";
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    bool error = false;
                    try
                    {
                        if (Tools.IsZipPackage(zipPath))
                        {
                            copyByZipPackage(zipPath);
                        }
                    }
                    catch (Exception)
                    {
                        error = true;
                    }
                    tree = Tools.GetTree(picPath);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        resetButtons();
                        progress.IsIndeterminate = false;
                        resultText.Text = zipName + (error ? " has error" : " unziped");
                    });
                });
            }
        }
        private void delete_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!progress.IsIndeterminate)
            {
                progress.IsIndeterminate = true;
                var button = ((sender as MenuItem).Parent as ContextMenu).Tag as Button;
                string zipPath = Package.getFilePath(button.Content as string, button.Tag as DirTree);
                string zipName = Path.GetFileName(zipPath);
                resultText.Text = zipName + " deling";
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        store.DeleteFile(zipPath);
                    }
                    tree = Tools.GetTree(picPath);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        resetButtons();
                        progress.IsIndeterminate = false;
                        resultText.Text = zipName + " deled";
                    });
                });
            }
        }

        private void dirButton_click(object sender, System.Windows.Input.GestureEventArgs e)
        {
            MainPage.tree = (sender as Button).Tag as DirTree;
            resetButtons();
        }
        private void dirButton_hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ContextMenuService.SetContextMenu(sender as DependencyObject, dirMenu);
            dirMenu.Tag = sender;
            if (cut.Tag as string == Package.getFilePath("", (sender as Button).Tag as DirTree))
            {
                cut.Header = "取消剪切";
                cutTo.Visibility = Visibility.Collapsed;
            }
        }
        private void rename_Click(object sender, RoutedEventArgs e)
        {
        }
        private void remove_Click(object sender, RoutedEventArgs e)
        {
            if (!progress.IsIndeterminate)
            {
                progress.IsIndeterminate = true;
                string dirPath = Package.getFilePath("", (((sender as MenuItem).Parent as ContextMenu).Tag as Button).Tag as DirTree);
                resultText.Text = Path.GetFileName(dirPath) + " removing";
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        delPath(dirPath, store);
                        delPath(Tools.ToCachePath(dirPath, picPath, cachePath), store);
                    }
                    tree = Tools.GetTree(picPath);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        resetButtons();
                        progress.IsIndeterminate = false;
                        resultText.Text = Path.GetFileName(dirPath) + " removed";
                    });
                });
            }
        }
        private void delPath(string path, IsolatedStorageFile store)
        {
            if (store.DirectoryExists(path))
            {
                foreach (string file in store.GetFileNames(Path.Combine(path, "*.*")))
                {
                    store.DeleteFile(Path.Combine(path, file));
                }
                foreach (string dir in store.GetDirectoryNames(Path.Combine(path, "*")))
                {
                    delPath(Path.Combine(path, dir), store);
                }
                store.DeleteDirectory(path);
            }
        }
        private void cut_Click(object sender, RoutedEventArgs e)
        {
            string dirPath = Package.getFilePath("", (((sender as MenuItem).Parent as ContextMenu).Tag as Button).Tag as DirTree);
            if (cut.Tag as string != dirPath as string)
            {
                cut.Tag = dirPath;
                cutTo.Header = "从" + Path.GetFileName(dirPath) + "剪切至此处";
                cutTo.Visibility = Visibility.Visible;
            }
            else
            {
                cut.Tag = null;
                cut.Header = "剪切文件";
            }
        }
        private void cutTo_Click(object sender, RoutedEventArgs e)
        {
            cutTo.Visibility = Visibility.Collapsed;
            if (!progress.IsIndeterminate)
            {
                progress.IsIndeterminate = true;
                string cutFrom = cut.Tag as string;
                string parseTo = Package.getFilePath("", (((sender as MenuItem).Parent as ContextMenu).Tag as Button).Tag as DirTree);
                resultText.Text = Path.GetFileName(cutFrom) + " cutTo " + Path.GetFileName(parseTo);
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        bool notEmpty = false;
                        foreach (string filename in store.GetFileNames(Path.Combine(cutFrom, "*.*")))
                        {
                            string filenameTo = Path.Combine(parseTo, filename);
                            if (!store.FileExists(filenameTo))
                            {
                                store.MoveFile(Path.Combine(cutFrom, filename), filenameTo);
                            }
                            else
                            {
                                notEmpty = true;
                            }
                        }
                        foreach (string pathname in store.GetFileNames(Path.Combine(cutFrom, "*")))
                        {
                            string pathnameTo = Path.Combine(parseTo, pathname);
                            if (!store.DirectoryExists(pathnameTo))
                            {
                                store.MoveDirectory(Path.Combine(cutFrom, pathname), pathnameTo);
                            }
                            else
                            {
                                notEmpty = true;
                            }
                        }
                        if (!notEmpty)
                        {
                            store.DeleteDirectory(cutFrom);
                        }
                        delPath(Tools.ToCachePath(cutFrom, picPath, cachePath), store);
                    }
                    tree = Tools.GetTree(picPath);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        resetButtons();
                        progress.IsIndeterminate = false;
                        resultText.Text = Path.GetFileName(cutFrom) + " cuted";
                    });
                });
            }
        }

        private void copyByZipPackage(string seed)
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var stream = store.OpenFile(seed, FileMode.Open))
                {
                    SharpCompress.Common.ArchiveEncoding.Default = DBCSCodePage.DBCSEncoding.GetDBCSEncoding("gb2312");
                    using (var reader = ReaderFactory.Open(stream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (reader.Entry.IsDirectory) continue;
                            string filePath = reader.Entry.FilePath.TrimEnd(new char[] { '/' }).Replace('/', Path.DirectorySeparatorChar);
                            if (filePath.IndexOf(Path.DirectorySeparatorChar) == -1)
                            {
                                string seedPath = seed.Substring(0, seed.Length - 4);
                                if (!store.DirectoryExists(seedPath))
                                {
                                    store.CreateDirectory(seedPath);
                                    System.Diagnostics.Debug.WriteLine(seedPath + " created");
                                }
                                filePath = Path.Combine(seedPath, filePath);
                            }
                            else
                            {
                                filePath = Path.Combine(picPath, filePath);
                            }
                            if (Tools.IsPicImage(filePath, PicMode.AllSupport))
                            {
                                //if (store.FileExists(filePath))
                                //{
                                //    System.Diagnostics.Debug.WriteLine(DateTime.Now +" "+ filePath + " Exists so deleted");
                                //    store.DeleteFile(filePath);
                                //}
                                if (!store.FileExists(filePath))
                                {
                                    var path = Path.GetDirectoryName(filePath);
                                    if (!store.DirectoryExists(path))
                                        store.CreateDirectory(path);
                                    try
                                    {
                                        using (var streamOut = store.CreateFile(filePath))
                                        {
                                            reader.WriteEntryTo(streamOut);
                                            System.Diagnostics.Debug.WriteLine(filePath + " copyed");
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        store.DeleteFile(filePath);
                                        this.Dispatcher.BeginInvoke(() =>
                                        {
                                            resultText.Text = Path.GetFileName(filePath) + " copy fail";
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var query = this.NavigationContext.QueryString;
            if (query.ContainsKey("href"))
            {
                picPath = query["href"];
                cachePath = "cache";
                query.Remove("href");
                ApplicationTitle.Text = picPath;
                progress.IsIndeterminate = true;
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    tree = Tools.GetTree(picPath);
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        resetButtons();
                        progress.IsIndeterminate = false;
                    });
                });
            }
        }

        private void PhoneApplicationPage_BackKeyPress(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (progress.IsIndeterminate == true)
            {
            }
            else
            {
                if (MainPage.tree.IsPicPackage)
                {
                    MainPage.tree = MainPage.tree.Root;
                }
                if (!MainPage.tree.IsFinalRoot)
                {
                    MainPage.tree = MainPage.tree.Root;
                    resetButtons();
                    e.Cancel = true;
                }
                else
                {
                    //App.Quit();
                }
            }
        }

        private void menu_Closed(object sender, RoutedEventArgs e)
        {
            ContextMenuService.SetContextMenu((sender as ContextMenu).Tag as DependencyObject, null);
        }
    }
}