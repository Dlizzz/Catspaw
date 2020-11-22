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

namespace Catspaw
{
    /// <summary>
    /// Logique d'interaction pour VolumePopup.xaml
    /// </summary>
    public partial class VolumePopup
    {
        /// <summary>
        /// Initialize new instance of Volume Popup
        /// </summary>
        public VolumePopup()
        {
            InitializeComponent();

            DataContext = ((App)Application.Current).PioneerAvr;
        }
    }
}
