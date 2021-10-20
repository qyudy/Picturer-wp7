using System;
using System.Threading;
using System.Windows;
using Microsoft.Phone.Controls;

namespace Picturer
{
    public partial class Interceptor : PhoneApplicationPage
    {
        public Interceptor()
        {
            InitializeComponent();
        }

        private bool hasGone = false;

        private const int DELAY = 0, TAP = 1, DOUBLE_TAP = 2, HOLD = 3;
        private readonly Password[] passwords = { new Password("comic",     new int[] { 0, 1 ,1, 1 })   //0,Delay
                                                , new Password("picture",   new int[] { 1, 1, 2, 1 })   //1,Tap
                                                , new Password("image",     new int[] { 2, 2, 1, 2 })   //2,DoubleTap
                                                , new Password("manga",     new int[] { 2, 1, 1, 1 })   //3,Hold
                                                };
        private int[] passPointer = new int[3];

        private DelayListener listener = new DelayListener(150);

        private void interceptor_Loaded(object sender, RoutedEventArgs e)
        {
            testPassword(DELAY);
        }

        private void interceptor_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            listener.Start(this.Dispatcher, () =>
            {
                testPassword(TAP);
            });
        }

        private void interceptor_DoubleTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            listener.TimeOut();
            testPassword(DOUBLE_TAP);
        }

        private void interceptor_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            listener.TimeOut();
            testPassword(HOLD);
        }

        private void testPassword(int word)
        {
            if (!hasGone)
            {
                switch (word)
                {
                    case DELAY:
                        int delay = 1500;
                        ThreadPool.QueueUserWorkItem((o) =>
                        {
                            Thread.Sleep(delay);
                            if (!hasGone)
                            {
                                this.Dispatcher.BeginInvoke(() =>
                                {
                                    go(word);
                                });
                            }
                        });
                        break;
                    case TAP:
                    case DOUBLE_TAP:
                    case HOLD:
                        go(word);
                        break;
                }
            }
            else
            {
                for (int i = 0; i < passwords.Length; i++)
                {
                    if (passwords[i].fullpass[passwords[i].passIndex] == word)
                    {
                        passwords[i].passIndex++;
                        if (passwords[i].passIndex == passwords[i].fullpass.Length)
                        {
                            go(i);
                            passwords[i].passIndex = 0;
                            word = -1;
                            i = -1;
                        }
                    }
                    else
                    {
                        passwords[i].passIndex = 0;
                    }
                }
            }
        }

        private void go(int word)
        {
            pass.Text = passwords[word].name;
            this.NavigationService.Navigate(new Uri("/MainPage.xaml?href=" + passwords[word].name, UriKind.Relative));
            hasGone = true;
        }

        struct Password
        {
            public readonly string name;
            public readonly int[] fullpass;
            public int passIndex;
            public Password(string name, int[] fullpass)
            {
                this.name = name;
                this.fullpass = fullpass;
                this.passIndex = 0;
            }
        }
    }

}