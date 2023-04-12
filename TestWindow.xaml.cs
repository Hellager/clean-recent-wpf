using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using NLog;
using Quartz;
using Quartz.Impl;
using CronExpressionDescriptor;

namespace CleanRecent
{
    /// <summary>
    /// TestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TestWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        FileSystemWatcher recentMonitor;
        private StdSchedulerFactory scheduleFactory;
        private IScheduler scheduler;

        [DisallowConcurrentExecution]
        public class CleanJob : IJob
        {
            private static Logger Logger = LogManager.GetCurrentClassLogger();
            public static readonly JobKey Key = new JobKey("cleanJob", "clean");

            public async Task Execute(IJobExecutionContext context)
            {
                if (context.RefireCount > 10)
                {
                    // we might not ever succeed!
                    // maybe log a warning, throw another type of error, inform the engineer on call
                    return;
                }

                try
                {
                    Logger.Debug("Do something in cron job");

                    Properties.Settings.Default.NextRuntime = context.NextFireTimeUtc.GetValueOrDefault().UtcDateTime;
                    Logger.Debug("Next runtime: {}", Properties.Settings.Default.NextRuntime.ToString());
                }
                catch (Exception ex)
                {
                    throw new JobExecutionException(msg: "", refireImmediately: true, cause: ex);
                }

            }
        }

        public TestWindow()
        {
            InitializeComponent();

            InitializeQuartzScheduler();

            LoadAppData();

            this.DataContext = this;
        }

        private void AutoStart_Click(object sender, RoutedEventArgs e)
        {
            var isAutoStartChecked = this.CheckAutoStart.IsChecked;

            var registryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryKeyPath, true);
            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (isAutoStartChecked == true)
            {
                registryKey.SetValue("CleanRecent", appPath);
            }
            else
            {
                registryKey.DeleteValue("CleanRecent", false);
            }

            Logger.Debug("Is Auto Start: {}", isAutoStartChecked);
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            var isDarkMode = this.CheckDarkMode.IsChecked;

            Logger.Debug("Is Dark Mode: {}", isDarkMode);
        }

        private void LangSelector_DropDownClosed(object sender, EventArgs e)
        {
            var selectedLang = ((ComboBoxItem)this.LangSelector.SelectedItem).Tag.ToString();
            var I18nResourceIndex = 0;

            for (int i = 0; i < System.Windows.Application.Current.Resources.MergedDictionaries.Count; i++)
            {
                var resourcePath = System.Windows.Application.Current.Resources.MergedDictionaries[i].Source.ToString();
                if (resourcePath.Contains("I18n"))
                {
                    I18nResourceIndex = i;
                    break;
                }
            }

            ResourceDictionary I18nResource = new ResourceDictionary();
            I18nResource.Source = new Uri(@"I18n\" + selectedLang + ".xaml", UriKind.Relative);

            if (I18nResource != null)
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.RemoveAt(I18nResourceIndex);
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(I18nResource);
                Logger.Debug(@"Switch language to " + selectedLang);
            }
        }

        private void ParseCron_Click(object sender, RoutedEventArgs e)
        {
            var expression = this.CronExpression.Text;
            var result = "";

            /*
             * https://github.com/bradymholt/cron-expression-descriptor
             * Locale: en-US, zh-CN, zh-TW, fr, ru
             */
            if (Quartz.CronExpression.IsValidExpression(expression))
            {
                result = ExpressionDescriptor.GetDescription(expression, new Options()
                {
                    Locale = "en-US",
                    DayOfWeekStartIndexZero = false,
                    Use24HourTimeFormat = true,
                });
            }
            else
            {
                Options options = new Options() { ThrowExceptionOnParseError = false, Locale = "en-US" };
                ExpressionDescriptor ceh = new ExpressionDescriptor(expression, options);
                
                result = ceh.GetDescription(DescriptionTypeEnum.FULL);
            }

            this.CronDescription.Content = result;
        }

        private async void InitializeQuartzScheduler()
        {
            try
            {
                this.scheduleFactory = new StdSchedulerFactory();
                this.scheduler = await this.scheduleFactory.GetScheduler();

                await this.scheduler.Start();

                Logger.Debug("Quartz Scheduler Service Available");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public void UpdateCronJobNextRuntime(DateTime time)
        {
            this.CronNextRuntime.Content = time.ToString();
            Properties.Settings.Default.NextRuntime = time;
        }

        private async void StartCronJob_Click(object sender, RoutedEventArgs e)
        {
            var expression = this.CronJobExpression.Text;

            IJobDetail cleanJob = JobBuilder.Create<CleanJob>()
                .WithIdentity("cleanJob", "clean")
                .Build();

            ITrigger cronTrigger = TriggerBuilder.Create()
                .WithIdentity("myTrigger", "clean")
                .WithCronSchedule(expression)
                .Build();

            await this.scheduler.ScheduleJob(cleanJob, cronTrigger);

            var nextSchedule = new CronExpression(expression);
            var NextRuntime = nextSchedule.GetNextValidTimeAfter(DateTimeOffset.Now) ?? DateTimeOffset.Now;
            Properties.Settings.Default.NextRuntime = NextRuntime.DateTime;
            Logger.Debug("Next runtime: {}", Properties.Settings.Default.NextRuntime.ToString());

            Logger.Debug("Success add clean job");
        }

        private async void StopCronJob_Click(object sender, RoutedEventArgs e)
        {
            await this.scheduler.PauseJob(new JobKey("cleanJob", "clean"));
            await this.scheduler.DeleteJob(new JobKey("cleanJob", "clean"));

            Logger.Debug("Stop clean cron job.");
        }

        private void RecentMenu_Changed(object sender, FileSystemEventArgs e)
        {
            Logger.Debug("Detect Changed In System Recent Menu.");

            Properties.Settings.Default.LastRuntime = DateTime.Now;

            this.MonitorLastRuntime.Content = DateTime.Now.ToString();
        }

        private void StartMonitorJob_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Start Monitor System Recent Access Menu.");

            var recent_path = Environment.GetFolderPath(Environment.SpecialFolder.Recent);

            this.recentMonitor = new FileSystemWatcher();
            recentMonitor.Path = recent_path;
            recentMonitor.IncludeSubdirectories = true;
            recentMonitor.Changed += RecentMenu_Changed;
            recentMonitor.Created += RecentMenu_Changed;
            recentMonitor.Deleted += RecentMenu_Changed;
            recentMonitor.EnableRaisingEvents = true;
        }

        private void StopMonitorJob_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Stop Monitor System Recent Access Menu.");

            this.recentMonitor.EnableRaisingEvents = false;
        }

        private void EditFilterList_Click(object sender, RoutedEventArgs e)
        {
            FilterListEditorWindow lWindow = new FilterListEditorWindow();
            lWindow.SetEditTarget(0); // Default edit black list
            lWindow.ShowDialog();
        }

        private void EmptyFilterList_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.FilterBlackList = new List<CleanFilterItem>();
            Properties.Settings.Default.FilterWhiteList = new List<CleanFilterItem>();

            Logger.Debug("Empty blacklist and whitelist");
        }

        private void ImportFilterList_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.DefaultExt = ".json";
            dialog.Filter = "JSON File (*.json)|*.json|All(*.*)|*";

            Nullable<bool> result = dialog.ShowDialog(this);
            if (result == true)
            {
                try
                {
                    var jsonString = File.ReadAllText(dialog.FileName);
                    ExportCleanFilterList? listData = JsonSerializer.Deserialize<ExportCleanFilterList>(jsonString);

                    Properties.Settings.Default.FilterBlackList = listData?.FilterBlackList;
                    Properties.Settings.Default.FilterWhiteList = listData?.FilterWhiteList;
                    Logger.Debug("Import data from: {}", dialog.FileName);
                }
                catch (Exception ex)
                {
                    Logger.Error("Invalid json file");
                }

            }
        }

        private void ExportFilterList_Click(object sender, RoutedEventArgs e)
        {
            ExportCleanFilterList exportList = new ExportCleanFilterList()
            {
                FilterBlackList = Properties.Settings.Default.FilterBlackList,
                FilterWhiteList = Properties.Settings.Default.FilterWhiteList,
            };

            var jsonOpt = new JsonSerializerOptions { WriteIndented = true };
            string jsonData = JsonSerializer.Serialize(exportList, jsonOpt);

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = System.Environment.CurrentDirectory.ToString();
            dialog.FileName = "clean_recent_list.json";
            dialog.DefaultExt = ".json";
            dialog.Filter = "JSON File (*.json)|*.json";

            Nullable<bool> result = dialog.ShowDialog();
            if (result == true)
            {
                File.WriteAllText(dialog.FileName, jsonData);
                Logger.Debug("Save list data to json");
            }
        }

        private void TemplateFilterList_Click(object sender, RoutedEventArgs e)
        {
            var curTime = DateTime.Now;
            CleanFilterItem item1 = new CleanFilterItem() { Level = CleanFilterLevel.Level0, Label = "tem1", Data = "item1", CreateTime = curTime, LastModifiedTime = curTime };
            CleanFilterItem item2 = new CleanFilterItem() { Level = CleanFilterLevel.Level1, Label = "tem2", Data = "item2", CreateTime = curTime, LastModifiedTime = curTime };
            CleanFilterItem item3 = new CleanFilterItem() { Level = CleanFilterLevel.Level2, Label = "tem3", Data = "item3", CreateTime = curTime, LastModifiedTime = curTime };

            var testItemList = new List<CleanFilterItem>() { item1, item2, item3 };

            var curBlackList = Properties.Settings.Default.FilterBlackList;
            if (curBlackList.Count > 0)
            {
                foreach (var testItem in testItemList)
                {
                    bool IsInCurList = false;
                    foreach (var originItem in curBlackList)
                    {
                        if (testItem.Data == originItem.Data)
                        {
                            Logger.Debug("Already in blacklist");
                            IsInCurList = true;
                            break;
                        }
                    }

                    if (!IsInCurList)
                    {
                        Properties.Settings.Default.FilterBlackList.Add(testItem);
                    }
                }
            }
            else
            {
                Properties.Settings.Default.FilterBlackList = testItemList;
                Logger.Debug("Initialize black list");
            }


            CleanFilterItem item4 = new CleanFilterItem() { Level = CleanFilterLevel.Level2, Label = "tem3", Data = "item3", CreateTime = curTime, LastModifiedTime = curTime };
            CleanFilterItem item5 = new CleanFilterItem() { Level = CleanFilterLevel.Level1, Label = "tem4", Data = "item4", CreateTime = curTime, LastModifiedTime = curTime };
            CleanFilterItem item6 = new CleanFilterItem() { Level = CleanFilterLevel.Level0, Label = "tem5", Data = "item5", CreateTime = curTime, LastModifiedTime = curTime };

            var testItemList1 = new List<CleanFilterItem>() { item4, item5, item6 };

            var curWhiteList = Properties.Settings.Default.FilterWhiteList;
            if (curWhiteList.Count > 0)
            {
                foreach (var testItem in testItemList1)
                {
                    bool IsInCurList = false;
                    foreach (var originItem in curWhiteList)
                    {
                        if (testItem.Data == originItem.Data)
                        {
                            Logger.Debug("Already in whitelist");
                            IsInCurList = true;
                            break;
                        }
                    }

                    if (!IsInCurList)
                    {
                        Properties.Settings.Default.FilterWhiteList.Add(testItem);
                    }
                }
            }
            else
            {
                Properties.Settings.Default.FilterWhiteList = testItemList1;
                Logger.Debug("Initialize white list");
            }
        }

        private void LoadAppData()
        {
            Properties.Settings.Default.Reload();

            if (Properties.Settings.Default.FilterBlackList == null)
            {
                Properties.Settings.Default.FilterBlackList = new List<CleanFilterItem>();
            }

            if (Properties.Settings.Default.FilterWhiteList == null)
            {
                Properties.Settings.Default.FilterWhiteList = new List<CleanFilterItem>();
            }

            Logger.Debug("Load app data");
        }

        private void LoadData_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reload();

            if (Properties.Settings.Default.FilterBlackList == null)
            {
                Properties.Settings.Default.FilterBlackList = new List<CleanFilterItem>();
            }

            if (Properties.Settings.Default.FilterWhiteList == null)
            {
                Properties.Settings.Default.FilterWhiteList = new List<CleanFilterItem>();
            }

            Logger.Debug("Load app data");
        }

        private void SaveData_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.FilterBlackList = Properties.Settings.Default.FilterBlackList;
            Properties.Settings.Default.FilterWhiteList = Properties.Settings.Default.FilterWhiteList;

            Properties.Settings.Default.Save();

            Logger.Debug("Save data");
        }
    }
}
