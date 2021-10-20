using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;

namespace Picturer
{
    public partial class ErrorPage : PhoneApplicationPage
    {
        public ErrorPage()
        {
            InitializeComponent();
        }

        public static Exception Exception;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ErrorText.Text = Exception.StackTrace;
        }

    }
}