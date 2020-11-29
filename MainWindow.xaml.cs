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
        }

        private void CatspawMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var copyright = (AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyCopyrightAttribute));
            var version = (AssemblyFileVersionAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyFileVersionAttribute));

            TxtVersion.Text = copyright.Copyright + " - Version: " + version.Version;
        }
    }
}
