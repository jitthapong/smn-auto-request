using NLog;
using SMN_INV_AUTO_SYNC.Properties;
using SMN_INV_AUTO_SYNC.Resources;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SMN_INV_AUTO_SYNC
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        private System.Windows.Forms.NotifyIcon _trayIcon;
        private System.Windows.Forms.ToolStripMenuItem _settingMenu;
        private System.Windows.Forms.ToolStripMenuItem _exitMenu;

        public App()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon()
            {
                Icon = AppResource.appicon,
                Text = "SMN_INV_AUTO_SYNC",
                ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(),
                Visible = true,
                BalloonTipTitle = "SMN_INV_AUTO_SYNC"
            };

            _settingMenu = new System.Windows.Forms.ToolStripMenuItem("Setting", null, Setting, "Setting");
            _exitMenu = new System.Windows.Forms.ToolStripMenuItem("Exit", null, Exit, "Exit");
            _trayIcon.ContextMenuStrip.Items.AddRange(
                new System.Windows.Forms.ToolStripItem[] {
                    _settingMenu,
                    _exitMenu,
                });
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _trayIcon.ShowBalloonTip(5000, "", "SMN_INV_AUTO_SYNC is running", System.Windows.Forms.ToolTipIcon.Info);

            await InitWorker();
        }

        private async Task InitWorker()
        {
            try
            {
                await Worker.Instance.InitializeAsync(Settings.Default.DBServer, Settings.Default.DBName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Initialize error");
            }
        }

        private void Setting(object sender, EventArgs e)
        {
            var settingWindow = new SettingWindow();
            settingWindow.ShowDialog();

            InitWorker();
        }

        private void Exit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Shutdown();
        }
    }
}
