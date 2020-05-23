using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reflection;
using System.Diagnostics;

namespace Catspaw
{
    /// <summary>
    /// Catspaw main window class
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Create Catspaw main window
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            DataContext = ((App)Application.Current).LogText;
        }

        private void WinCatspaw_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CatspawMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var copyright = (AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyCopyrightAttribute));
            var version = (AssemblyFileVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyFileVersionAttribute));

            TxtVersion.Text = copyright.Copyright + " - Version: " + version.Version;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ScrollViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            scrollViewer.VerticalScrollBarVisibility = scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        private void ScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            scrollViewer.VerticalScrollBarVisibility = scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            Process.Start(app.LogFile);
        }
    }
}
