using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using NLog;

namespace CleanRecent
{
    /// <summary>
    /// FilterListEditorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FilterListEditorWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public UInt16 EditTarget;
        public ObservableCollection<DataGridItem> DataGridBindingList = new ObservableCollection<DataGridItem>();

        public class DataGridItem : INotifyPropertyChanged
        {
            private Int32 _id { get; set; }
            private CleanFilterLevel _level { get; set; }
            private string _label { get; set; }
            private string _data { get; set; }
            private DateTime _createTime { get; set; }
            private DateTime _lastModifiedTime { get; set; }

            private bool _isSelected { get; set; }

            public Int32 ID
            {
                get { return _id; }
                set { this._id = value; OnPropertyChanged("ID"); }
            }

            public CleanFilterLevel Level
            {
                get { return _level; }
                set { this._level = value; OnPropertyChanged("Level"); }
            }

            public string Label
            {
                get { return _label; }
                set { this._label = value; OnPropertyChanged("Level"); }
            }

            public string Data
            {
                get { return _data; }
                set { this._data = value; OnPropertyChanged("Level"); }
            }

            public DateTime CreateTime
            {
                get { return _createTime; }
                //set { this._createTime = value; OnPropertyChanged("Level"); }
            }

            public DateTime LastModifiedTime
            {
                get { return _lastModifiedTime; }
                //set { this._lastModifiedTime = value; OnPropertyChanged("Level"); }
            }

            public bool IsSelected
            {
                get { return _isSelected; }
                set { this._isSelected = value; OnPropertyChanged("IsSelected"); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            void OnPropertyChanged(string name)
            {
                if (PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs(name));
                    var curTime = DateTime.Now;
                    this._lastModifiedTime = curTime;
                }
            }

            public DataGridItem(Int32 id, string data, string label)
            {
                this.ID = id;
                this.Level = CleanFilterLevel.Level0;
                this.Label = label;
                this.Data = data;
                var curTime = DateTime.Now;
                this._createTime = curTime;
                this._lastModifiedTime = curTime;
                this._isSelected = false;
            }
        }

        public FilterListEditorWindow()
        {
            InitializeComponent();
        }

        public void SetEditTarget(UInt16 data)
        {
            this.EditTarget = data;
            this.Title = (data == 1 ? "Filter White List" : "Filter Black List");
            var targetList = data == 1 ? Properties.Settings.Default.FilterWhiteList : Properties.Settings.Default.FilterBlackList;

            for (Int32 i = 0; i < targetList.Count; i++)
            {
                this.DataGridBindingList.Add(new DataGridItem(i, targetList[i].Data, targetList[i].Label));
            }

            this.ListTable.DataContext = this.DataGridBindingList;
        }

        private void GridCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var CurCheckBox = sender as CheckBox;
            var CurCheckBoxUID = int.Parse(CurCheckBox.Uid);

            if (CurCheckBoxUID == -1)
            {
                foreach (var item in this.DataGridBindingList)
                {
                    item.IsSelected = true;
                }

                CurCheckBox.Uid = "-2";
                return;
            }
            else if (CurCheckBoxUID == -2)
            {
                foreach (var item in this.DataGridBindingList)
                {
                    item.IsSelected = false;
                }

                CurCheckBox.Uid = "-1";
                return;
            }

            var CurItemIsSelected = this.DataGridBindingList[CurCheckBoxUID].IsSelected;
            this.DataGridBindingList[CurCheckBoxUID].IsSelected = (CurItemIsSelected ? false : true);
        }

        private void AddNewItem_Click(object sender, RoutedEventArgs e)
        {
            this.DataGridBindingList.Add(new DataGridItem(this.DataGridBindingList.Count - 1, this.NewItemDataInput.Text, this.NewItemDataInput.Text));
            Logger.Info("Add new item to datagrid");
        }

        private void DeleteNewItem_Click(object sender, RoutedEventArgs e)
        {
            var SelectedItems = from item in this.DataGridBindingList
                                where item.IsSelected == true
                                select item;

            var SelectedItemsList = SelectedItems.ToList();
            if (SelectedItemsList.Count == 0) return;

            for (Int32 i = 0; i < SelectedItemsList.Count; i++)
            {
                for (Int32 j = 0; j < this.DataGridBindingList.Count; j++)
                {
                    if (this.DataGridBindingList[j].Data == SelectedItemsList[i].Data)
                    {
                        this.DataGridBindingList.Remove(SelectedItemsList[i]);
                    }
                }
            }

            for (Int32 i = 0; i < this.DataGridBindingList.Count; i++)
            {
                this.DataGridBindingList[i].ID = i;
            }
        }

        private void ButtonConfirm_Click(object sender, RoutedEventArgs e)
        {
            var CurList = new List<CleanFilterItem>();

            foreach (var item in this.DataGridBindingList)
            {
                CurList.Add(new CleanFilterItem()
                {
                    Data = item.Data,
                    Label = item.Label,
                    Level = item.Level,
                    CreateTime = item.CreateTime,
                    LastModifiedTime = item.LastModifiedTime,
                });
            }

            if (this.EditTarget == 1)
            {
                Properties.Settings.Default.FilterWhiteList = CurList;
            }
            else
            {
                Properties.Settings.Default.FilterBlackList = CurList;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
