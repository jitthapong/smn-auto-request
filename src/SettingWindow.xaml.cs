using SMN_INV_AUTO_SYNC.Properties;
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
using System.Windows.Shapes;

namespace SMN_INV_AUTO_SYNC
{
    /// <summary>
    /// Interaction logic for Setting.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        public SettingWindow()
        {
            InitializeComponent();

            txtDbServer.Text = Settings.Default.DBServer;
            txtDbName.Text = Settings.Default.DBName;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.DBServer = txtDbServer.Text;
            Settings.Default.DBName = txtDbName.Text;
            Settings.Default.Save();
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
