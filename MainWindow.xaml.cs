using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NLog;

namespace CleanRecent
{
    public enum CleanFilterLevel { Level0, Level1, Level2, Level3 };

    [Serializable]
    public struct CleanFilterItem
    {
        public CleanFilterLevel Level { get; set; }
        public string Label { get; set; }
        public string Data { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime LastModifiedTime { get; set; }
    }

    public struct ExportCleanFilterList
    {
        public List<CleanFilterItem> FilterBlackList { get; set; }
        public List<CleanFilterItem> FilterWhiteList { get; set; }
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly NotifyIcon notifyIcon = new NotifyIcon();

        public void DefaultOpenTestWindow()
        {
            TestWindow testWindow = new TestWindow();
            testWindow.ShowDialog();

            var appWindow = System.Windows.Application.Current.MainWindow;
            appWindow.Hide();

            Logger.Debug("Open test window, hide main window");
        }

        private void OnWindowMenuItemClick(object sender, EventArgs e)
        {
            BuildNotifyIconContextMenu();
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
            }
        }

        private void OnExitMenuItemClick(object sender, EventArgs e)
        {
            Logger.Debug("Exit Program");

            this.notifyIcon.Visible = false;

            Thread.Sleep(1000);

            this.notifyIcon.Dispose();

            System.Windows.Application.Current.Shutdown();

            return;
        }

        private void BuildNotifyIconContextMenu()
        {
            ContextMenuStrip menuStrip = new ContextMenuStrip();
            this.notifyIcon.ContextMenuStrip = menuStrip;

            ToolStripMenuItem windowMenuItem = new ToolStripMenuItem();
            windowMenuItem.Text = this.Visibility == Visibility.Visible ? "Show" : "Hide";
            windowMenuItem.Click += new EventHandler(OnWindowMenuItemClick);

            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem();
            exitMenuItem.Text = "Exit";
            exitMenuItem.Click += new EventHandler(OnExitMenuItemClick);

            menuStrip.Items.Add(windowMenuItem);
            menuStrip.Items.Add(exitMenuItem);
        }

        private void BuildNotifyIcon()
        {
            System.Drawing.Icon _icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("/Assets/icon.ico", UriKind.Relative)).Stream);

            notifyIcon.Visible = true;
            notifyIcon.Icon = _icon;
            notifyIcon.Text = "Clean Recent";

            BuildNotifyIconContextMenu();
        }

        public MainWindow()
        {
            // BuildNotifyIcon();

            InitializeComponent();

            DefaultOpenTestWindow();
        }
    }
}
