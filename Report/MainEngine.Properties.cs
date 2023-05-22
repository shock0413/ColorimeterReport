using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Report
{
    public partial class MainEngine : INotifyPropertyChanged
    {
        private StructPassRange selectedPassRange = null;
        public StructPassRange SelectedPassRange
        {
            get
            {
                return selectedPassRange;
            }
            set
            {
                selectedPassRange = value;
                NotifyPropertyChanged("SelectedPassRange");
            }
        }

        public bool FrontSymmetric
        {
            get
            {
                bool tmp = m_Config.GetBoolian("Info", "FrontSymmetric", false);

                if (tmp)
                {
                    FrontYesSymmetric = Visibility.Visible;
                    FrontNoSymmetric = Visibility.Hidden;
                }
                else
                {
                    FrontYesSymmetric = Visibility.Hidden;
                    FrontNoSymmetric = Visibility.Visible;
                }

                return tmp;
            }
            set
            {
                m_Config.WriteValue("Info", "FrontSymmetric", value);
                NotifyPropertyChanged("FrontSymmetric");
            }
        }

        public bool RearSymmetric
        {
            get
            {
                bool tmp = m_Config.GetBoolian("Info", "RearSymmetric", false);

                if (tmp)
                {
                    RearYesSymmetric = Visibility.Visible;
                    RearNoSymmetric = Visibility.Hidden;
                }
                else
                {
                    RearYesSymmetric = Visibility.Hidden;
                    RearNoSymmetric = Visibility.Visible;
                }

                return tmp;
            }
            set
            {
                m_Config.WriteValue("Info", "RearSymmetric", value);
                NotifyPropertyChanged("RearSymmetric");
            }
        }

        private Visibility frontNoSymmetric = Visibility.Hidden;
        public Visibility FrontNoSymmetric
        {
            get
            {
                return frontNoSymmetric;
            }
            set
            {
                frontNoSymmetric = value;
                NotifyPropertyChanged("FrontNoSymmetric");
            }
        }

        private Visibility frontYesSymmetric = Visibility.Hidden;
        public Visibility FrontYesSymmetric
        {
            get
            {
                return frontYesSymmetric;
            }
            set
            {
                frontYesSymmetric = value;
                NotifyPropertyChanged("FrontYesSymmetric");
            }
        }

        private Visibility rearNoSymmetric = Visibility.Hidden;
        public Visibility RearNoSymmetric
        {
            get
            {
                return rearNoSymmetric;
            }
            set
            {
                rearNoSymmetric = value;
                NotifyPropertyChanged("RearNoSymmetric");
            }
        }

        private Visibility rearYesSymmetric = Visibility.Hidden;
        public Visibility RearYesSymmetric
        {
            get
            {
                return rearYesSymmetric;
            }
            set
            {
                rearYesSymmetric = value;
                NotifyPropertyChanged("RearYesSymmetric");
            }
        }

        private string selectedCarKind = "ALL";
        public string SelectedCarKind
        {
            get
            {
                return selectedCarKind;
            }
            set
            {
                selectedCarKind = value;
                NotifyPropertyChanged("SelectedCarKind");
            }
        }

        private ObservableCollection<string> carKindList = new ObservableCollection<string>();
        public ObservableCollection<string> CarKindList
        {
            get
            {
                return carKindList;
            }
            set
            {
                carKindList = value;
                NotifyPropertyChanged("CarKindList");
            }
        }

        private string selectedColor = "ALL";
        public string SelectedColor
        {
            get
            {
                return selectedColor;
            }
            set
            {
                selectedColor = value;
                NotifyPropertyChanged("SelectedColor");
            }
        }

        private ObservableCollection<string> colorList = new ObservableCollection<string>();
        public ObservableCollection<string> ColorList
        {
            get
            {
                return colorList;
            }
            set
            {
                colorList = value;
                NotifyPropertyChanged("ColorList");
            }
        }

        private string selectedResult = "";
        public string SelectedResult
        {
            get
            {
                return selectedResult;
            }
            set
            {
                selectedResult = value;
                NotifyPropertyChanged("SelectedResult");
            }
        }

        private ObservableCollection<string> resultList = new ObservableCollection<string>();
        public ObservableCollection<string> ResultList
        {
            get
            {
                return resultList;
            }
            set
            {
                resultList = value;
                NotifyPropertyChanged("ResultList");
            }
        }

        private DateTime startDateTime = DateTime.Now;
        public DateTime StartDateTime
        {
            get
            {
                return startDateTime;
            }
            set
            {
                startDateTime = value;
                NotifyPropertyChanged("StartDateTime");
            }
        }

        private int startHour = 6;
        public int StartHour
        {
            get
            {
                return startHour;
            }
            set
            {
                startHour = value;
                NotifyPropertyChanged("StartHour");
            }
        }

        private int startMinute = 0;
        public int StartMinute
        {
            get
            {
                return startMinute;
            }
            set
            {
                startMinute = value;
                NotifyPropertyChanged("StartMinute");
            }
        }

        private DateTime endDateTime = DateTime.Now.AddDays(1);
        public DateTime EndDateTime
        {
            get
            {
                return endDateTime;
            }
            set
            {
                endDateTime = value;
                NotifyPropertyChanged("EndDateTime");
            }
        }

        private int endHour = 6;
        public int EndHour
        {
            get
            {
                return endHour;
            }
            set
            {
                endHour = value;
                NotifyPropertyChanged("EndHour");
            }
        }

        private int endMinute = 0;
        public int EndMinute
        {
            get
            {
                return endMinute;
            }
            set
            {
                endMinute = value;
                NotifyPropertyChanged("EndMinute");
            }
        }

        private string searchString = "";
        public string SearchString
        {
            get
            {
                return searchString;
            }
            set
            {
                searchString = value;
                NotifyPropertyChanged("SearchString");
            }
        }

        private SolidColorBrush plcStateColor = Brushes.Red;
        public SolidColorBrush PLCStateColor
        {
            get
            {
                return plcStateColor;
            }
            set
            {
                plcStateColor = value;
                NotifyPropertyChanged("PLCStateColor");
            }
        }

        private SolidColorBrush dbStateColor = Brushes.DarkGray;
        public SolidColorBrush DBStateColor
        {
            get
            {
                return dbStateColor;
            }
            set
            {
                dbStateColor = value;
                NotifyPropertyChanged("DBStateColor");
            }
        }

        private SolidColorBrush plcHeartBeatColor = Brushes.DarkGray;
        public SolidColorBrush PLCHeartBeatColor
        {
            get
            {
                return plcHeartBeatColor;
            }
            set
            {
                plcHeartBeatColor = value;
                NotifyPropertyChanged("PLCHeartBeatColor");
            }
        }

        private bool is_dE_Minus15 = false;
        public bool Is_dE_Minus15
        {
            get
            {
                return is_dE_Minus15;
            }
            set
            {
                is_dE_Minus15 = value;

                if (is_dE_Minus15)
                {
                    Visib_dE_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dE_Minus15");
            }
        }

        private bool is_dE_15 = false;
        public bool Is_dE_15
        {
            get
            {
                return is_dE_15;
            }
            set
            {
                is_dE_15 = value;

                if (is_dE_15)
                {
                    Visib_dE_15 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dE_15");
            }
        }

        private bool is_dE_25 = false;
        public bool Is_dE_25
        {
            get
            {
                return is_dE_25;
            }
            set
            {
                is_dE_25 = value;

                if (is_dE_25)
                {
                    Visib_dE_25 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dE_25");
            }
        }

        private bool is_dE_45 = false;
        public bool Is_dE_45
        {
            get
            {
                return is_dE_45;
            }
            set
            {
                is_dE_45 = value;

                if (is_dE_45)
                {
                    Visib_dE_45 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dE_45");
            }
        }

        private bool is_dE_75 = false;
        public bool Is_dE_75
        {
            get
            {
                return is_dE_75;
            }
            set
            {
                is_dE_75 = value;

                if (is_dE_75)
                {
                    Visib_dE_75 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dE_75");
            }
        }

        private bool is_dE_110 = false;
        public bool Is_dE_110
        {
            get
            {
                return is_dE_110;
            }
            set
            {
                is_dE_110 = value;

                NotifyPropertyChanged("Is_dE_110");

                if (is_dE_110)
                {
                    Visib_dE_110 = Visibility.Visible;
                }
                else
                {
                    Visib_dE_110 = Visibility.Hidden;
                }
            }
        }

        private bool is_L_Minus15 = true;
        public bool Is_L_Minus15
        {
            get
            {
                return is_L_Minus15;
            }
            set
            {
                is_L_Minus15 = value;

                if (is_L_Minus15)
                {
                    Visib_L_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_L_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_Minus15");
            }
        }

        private bool is_a_Minus15 = true;
        public bool Is_a_Minus15
        {
            get
            {
                return is_a_Minus15;
            }
            set
            {
                is_a_Minus15 = value;

                if (is_a_Minus15)
                {
                    Visib_a_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_a_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_Minus15");
            }
        }

        private bool is_b_Minus15 = true;
        public bool Is_b_Minus15
        {
            get
            {
                return is_b_Minus15;
            }
            set
            {
                is_b_Minus15 = value;

                if (is_b_Minus15)
                {
                    Visib_b_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_b_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_Minus15");
            }
        }

        private bool is_L_15 = true;
        public bool Is_L_15
        {
            get
            {
                return is_L_15;
            }
            set
            {
                is_L_15 = value;

                if (is_L_15)
                {
                    Visib_L_15 = Visibility.Visible;
                }
                else
                {
                    Visib_L_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_15");
            }
        }

        private bool is_a_15 = true;
        public bool Is_a_15
        {
            get
            {
                return is_a_15;
            }
            set
            {
                is_a_15 = value;

                if (is_a_15)
                {
                    Visib_a_15 = Visibility.Visible;
                }
                else
                {
                    Visib_a_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_15");
            }
        }

        private bool is_b_15 = true;
        public bool Is_b_15
        {
            get
            {
                return is_b_15;
            }
            set
            {
                is_b_15 = value;

                if (is_b_15)
                {
                    Visib_b_15 = Visibility.Visible;
                }
                else
                {
                    Visib_b_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_15");
            }
        }

        private bool is_L_25 = true;
        public bool Is_L_25
        {
            get
            {
                return is_L_25;
            }
            set
            {
                is_L_25 = value;

                if (is_L_25)
                {
                    Visib_L_25 = Visibility.Visible;
                }
                else
                {
                    Visib_L_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_25");
            }
        }

        private bool is_a_25 = true;
        public bool Is_a_25
        {
            get
            {
                return is_a_25;
            }
            set
            {
                is_a_25 = value;

                if (is_a_25)
                {
                    Visib_a_25 = Visibility.Visible;
                }
                else
                {
                    Visib_a_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_25");
            }
        }

        private bool is_b_25 = true;
        public bool Is_b_25
        {
            get
            {
                return is_b_25;
            }
            set
            {
                is_b_25 = value;

                if (is_b_25)
                {
                    Visib_b_25 = Visibility.Visible;
                }
                else
                {
                    Visib_b_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_25");
            }
        }

        private bool is_L_45 = true;
        public bool Is_L_45
        {
            get
            {
                return is_L_45;
            }
            set
            {
                is_L_45 = value;

                if (is_L_45)
                {
                    Visib_L_45 = Visibility.Visible;
                }
                else
                {
                    Visib_L_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_45");
            }
        }

        private bool is_a_45 = true;
        public bool Is_a_45
        {
            get
            {
                return is_a_45;
            }
            set
            {
                is_a_45 = value;

                if (is_a_45)
                {
                    Visib_a_45 = Visibility.Visible;
                }
                else
                {
                    Visib_a_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_45");
            }
        }

        private bool is_b_45 = true;
        public bool Is_b_45
        {
            get
            {
                return is_b_45;
            }
            set
            {
                is_b_45 = value;

                if (is_b_45)
                {
                    Visib_b_45 = Visibility.Visible;
                }
                else
                {
                    Visib_b_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_45");
            }
        }

        private bool is_L_75 = true;
        public bool Is_L_75
        {
            get
            {
                return is_L_75;
            }
            set
            {
                is_L_75 = value;

                if (is_L_75)
                {
                    Visib_L_75 = Visibility.Visible;
                }
                else
                {
                    Visib_L_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_75");
            }
        }

        private bool is_a_75 = true;
        public bool Is_a_75
        {
            get
            {
                return is_a_75;
            }
            set
            {
                is_a_75 = value;

                if (is_a_75)
                {
                    Visib_a_75 = Visibility.Visible;
                }
                else
                {
                    Visib_a_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_75");
            }
        }

        private bool is_b_75 = true;
        public bool Is_b_75
        {
            get
            {
                return is_b_75;
            }
            set
            {
                is_b_75 = value;

                if (is_b_75)
                {
                    Visib_b_75 = Visibility.Visible;
                }
                else
                {
                    Visib_b_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_75");
            }
        }

        private bool is_L_110 = true;
        public bool Is_L_110
        {
            get
            {
                return is_L_110;
            }
            set
            {
                is_L_110 = value;

                if (is_L_110)
                {
                    Visib_L_110 = Visibility.Visible;
                }
                else
                {
                    Visib_L_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_L_110");
            }
        }

        private bool is_a_110 = true;
        public bool Is_a_110
        {
            get
            {
                return is_a_110;
            }
            set
            {
                is_a_110 = value;

                if (is_a_110)
                {
                    Visib_a_110 = Visibility.Visible;
                }
                else
                {
                    Visib_a_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_a_110");
            }
        }

        private bool is_b_110 = true;
        public bool Is_b_110
        {
            get
            {
                return is_b_110;
            }
            set
            {
                is_b_110 = value;

                if (is_b_110)
                {
                    Visib_b_110 = Visibility.Visible;
                }
                else
                {
                    Visib_b_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_b_110");
            }
        }

        private bool is_dL_Minus15 = false;
        public bool Is_dL_Minus15
        {
            get
            {
                return is_dL_Minus15;
            }
            set
            {
                is_dL_Minus15 = value;

                if (is_dL_Minus15)
                {
                    Visib_dL_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_Minus15");
            }
        }

        private bool is_da_Minus15 = false;
        public bool Is_da_Minus15
        {
            get
            {
                return is_da_Minus15;
            }
            set
            {
                is_da_Minus15 = value;

                if (is_da_Minus15)
                {
                    Visib_da_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_da_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_Minus15");
            }
        }

        private bool is_db_Minus15 = false;
        public bool Is_db_Minus15
        {
            get
            {
                return is_db_Minus15;
            }
            set
            {
                is_db_Minus15 = value;

                if (is_db_Minus15)
                {
                    Visib_db_Minus15 = Visibility.Visible;
                }
                else
                {
                    Visib_db_Minus15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_Minus15");
            }
        }

        private bool is_dL_15 = false;
        public bool Is_dL_15
        {
            get
            {
                return is_dL_15;
            }
            set
            {
                is_dL_15 = value;

                if (is_dL_15)
                {
                    Visib_dL_15 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_15");
            }
        }

        private bool is_da_15 = false;
        public bool Is_da_15
        {
            get
            {
                return is_da_15;
            }
            set
            {
                is_da_15 = value;

                if (is_da_15)
                {
                    Visib_da_15 = Visibility.Visible;
                }
                else
                {
                    Visib_da_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_15");
            }
        }

        private bool is_db_15 = false;
        public bool Is_db_15
        {
            get
            {
                return is_db_15;
            }
            set
            {
                is_db_15 = value;

                if (is_db_15)
                {
                    Visib_db_15 = Visibility.Visible;
                }
                else
                {
                    Visib_db_15 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_15");
            }
        }

        private bool is_dL_25 = false;
        public bool Is_dL_25
        {
            get
            {
                return is_dL_25;
            }
            set
            {
                is_dL_25 = value;

                if (is_dL_25)
                {
                    Visib_dL_25 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_25");
            }
        }

        private bool is_da_25 = false;
        public bool Is_da_25
        {
            get
            {
                return is_da_25;
            }
            set
            {
                is_da_25 = value;

                if (is_da_25)
                {
                    Visib_da_25 = Visibility.Visible;
                }
                else
                {
                    Visib_da_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_25");
            }
        }

        private bool is_db_25 = false;
        public bool Is_db_25
        {
            get
            {
                return is_db_25;
            }
            set
            {
                is_db_25 = value;

                if (is_db_25)
                {
                    Visib_db_25 = Visibility.Visible;
                }
                else
                {
                    Visib_db_25 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_25");
            }
        }

        private bool is_dL_45 = false;
        public bool Is_dL_45
        {
            get
            {
                return is_dL_45;
            }
            set
            {
                is_dL_45 = value;

                if (is_dL_45)
                {
                    Visib_dL_45 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_45");
            }
        }

        private bool is_da_45 = false;
        public bool Is_da_45
        {
            get
            {
                return is_da_45;
            }
            set
            {
                is_da_45 = value;

                if (is_da_45)
                {
                    Visib_da_45 = Visibility.Visible;
                }
                else
                {
                    Visib_da_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_45");
            }
        }

        private bool is_db_45 = false;
        public bool Is_db_45
        {
            get
            {
                return is_db_45;
            }
            set
            {
                is_db_45 = value;

                if (is_db_45)
                {
                    Visib_db_45 = Visibility.Visible;
                }
                else
                {
                    Visib_db_45 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_45");
            }
        }

        private bool is_dL_75 = false;
        public bool Is_dL_75
        {
            get
            {
                return is_dL_75;
            }
            set
            {
                is_dL_75 = value;

                if (is_dL_75)
                {
                    Visib_dL_75 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_75");
            }
        }

        private bool is_da_75 = false;
        public bool Is_da_75
        {
            get
            {
                return is_da_75;
            }
            set
            {
                is_da_75 = value;

                if (is_da_75)
                {
                    Visib_da_75 = Visibility.Visible;
                }
                else
                {
                    Visib_da_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_75");
            }
        }

        private bool is_db_75 = false;
        public bool Is_db_75
        {
            get
            {
                return is_db_75;
            }
            set
            {
                is_db_75 = value;

                if (is_db_75)
                {
                    Visib_db_75 = Visibility.Visible;
                }
                else
                {
                    Visib_db_75 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_75");
            }
        }

        private bool is_dL_110 = false;
        public bool Is_dL_110
        {
            get
            {
                return is_dL_110;
            }
            set
            {
                is_dL_110 = value;

                if (is_dL_110)
                {
                    Visib_dL_110 = Visibility.Visible;
                }
                else
                {
                    Visib_dL_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_dL_110");
            }
        }

        private bool is_da_110 = false;
        public bool Is_da_110
        {
            get
            {
                return is_da_110;
            }
            set
            {
                is_da_110 = value;

                if (is_da_110)
                {
                    Visib_da_110 = Visibility.Visible;
                }
                else
                {
                    Visib_da_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_da_110");
            }
        }

        private bool is_db_110 = false;
        public bool Is_db_110
        {
            get
            {
                return is_db_110;
            }
            set
            {
                is_db_110 = value;

                if (is_db_110)
                {
                    Visib_db_110 = Visibility.Visible;
                }
                else
                {
                    Visib_db_110 = Visibility.Hidden;
                }

                NotifyPropertyChanged("Is_db_110");
            }
        }

        private Visibility visib_dE_Minus15 = Visibility.Collapsed;
        public Visibility Visib_dE_Minus15
        {
            get
            {
                return visib_dE_Minus15;
            }
            set
            {
                visib_dE_Minus15 = value;
                NotifyPropertyChanged("Visib_dE_Minus15");
            }
        }

        private Visibility visib_dE_15 = Visibility.Hidden;
        public Visibility Visib_dE_15
        {
            get
            {
                return visib_dE_15;
            }
            set
            {
                visib_dE_15 = value;
                NotifyPropertyChanged("Visib_dE_15");
            }
        }

        private Visibility visib_dE_25 = Visibility.Hidden;
        public Visibility Visib_dE_25
        {
            get
            {
                return visib_dE_25;
            }
            set
            {
                visib_dE_25 = value;
                NotifyPropertyChanged("Visib_dE_25");
            }
        }

        private Visibility visib_dE_45 = Visibility.Hidden;
        public Visibility Visib_dE_45
        {
            get
            {
                return visib_dE_45;
            }
            set
            {
                visib_dE_45 = value;
                NotifyPropertyChanged("Visib_dE_45");
            }
        }

        private Visibility visib_dE_75 = Visibility.Hidden;
        public Visibility Visib_dE_75
        {
            get
            {
                return visib_dE_75;
            }
            set
            {
                visib_dE_75 = value;
                NotifyPropertyChanged("Visib_dE_75");
            }
        }

        private Visibility visib_dE_110 = Visibility.Hidden;
        public Visibility Visib_dE_110
        {
            get
            {
                return visib_dE_110;
            }
            set
            {
                visib_dE_110 = value;
                NotifyPropertyChanged("Visib_dE_110");
            }
        }

        private Visibility visib_L_Minus15 = Visibility.Visible;
        public Visibility Visib_L_Minus15
        {
            get
            {
                return visib_L_Minus15;
            }
            set
            {
                visib_L_Minus15 = value;
                NotifyPropertyChanged("Visib_L_Minus15");
            }
        }

        private Visibility visib_a_Minus15 = Visibility.Visible;
        public Visibility Visib_a_Minus15
        {
            get
            {
                return visib_a_Minus15;
            }
            set
            {
                visib_a_Minus15 = value;
                NotifyPropertyChanged("Visib_a_Minus15");
            }
        }

        private Visibility visib_b_Minus15 = Visibility.Visible;
        public Visibility Visib_b_Minus15
        {
            get
            {
                return visib_b_Minus15;
            }
            set
            {
                visib_b_Minus15 = value;
                NotifyPropertyChanged("Visib_b_Minus15");
            }
        }

        private Visibility visib_L_15 = Visibility.Visible;
        public Visibility Visib_L_15
        {
            get
            {
                return visib_L_15;
            }
            set
            {
                visib_L_15 = value;
                NotifyPropertyChanged("Visib_L_15");
            }
        }

        private Visibility visib_a_15 = Visibility.Visible;
        public Visibility Visib_a_15
        {
            get
            {
                return visib_a_15;
            }
            set
            {
                visib_a_15 = value;
                NotifyPropertyChanged("Visib_a_15");
            }
        }

        private Visibility visib_b_15 = Visibility.Visible;
        public Visibility Visib_b_15
        {
            get
            {
                return visib_b_15;
            }
            set
            {
                visib_b_15 = value;
                NotifyPropertyChanged("Visib_b_15");
            }
        }

        private Visibility visib_L_25 = Visibility.Visible;
        public Visibility Visib_L_25
        {
            get
            {
                return visib_L_25;
            }
            set
            {
                visib_L_25 = value;
                NotifyPropertyChanged("Visib_L_25");
            }
        }

        private Visibility visib_a_25 = Visibility.Visible;
        public Visibility Visib_a_25
        {
            get
            {
                return visib_a_25;
            }
            set
            {
                visib_a_25 = value;
                NotifyPropertyChanged("Visib_a_25");
            }
        }

        private Visibility visib_b_25 = Visibility.Visible;
        public Visibility Visib_b_25
        {
            get
            {
                return visib_b_25;
            }
            set
            {
                visib_b_25 = value;
                NotifyPropertyChanged("Visib_b_25");
            }
        }

        private Visibility visib_L_45 = Visibility.Visible;
        public Visibility Visib_L_45
        {
            get
            {
                return visib_L_45;
            }
            set
            {
                visib_L_45 = value;
                NotifyPropertyChanged("Visib_L_45");
            }
        }

        private Visibility visib_a_45 = Visibility.Visible;
        public Visibility Visib_a_45
        {
            get
            {
                return visib_a_45;
            }
            set
            {
                visib_a_45 = value;
                NotifyPropertyChanged("Visib_a_45");
            }
        }

        private Visibility visib_b_45 = Visibility.Visible;
        public Visibility Visib_b_45
        {
            get
            {
                return visib_b_45;
            }
            set
            {
                visib_b_45 = value;
                NotifyPropertyChanged("Visib_b_45");
            }
        }

        private Visibility visib_L_75 = Visibility.Visible;
        public Visibility Visib_L_75
        {
            get
            {
                return visib_L_75;
            }
            set
            {
                visib_L_75 = value;
                NotifyPropertyChanged("Visib_L_75");
            }
        }

        private Visibility visib_a_75 = Visibility.Visible;
        public Visibility Visib_a_75
        {
            get
            {
                return visib_a_75;
            }
            set
            {
                visib_a_75 = value;
                NotifyPropertyChanged("Visib_a_75");
            }
        }

        private Visibility visib_b_75 = Visibility.Visible;
        public Visibility Visib_b_75
        {
            get
            {
                return visib_b_75;
            }
            set
            {
                visib_b_75 = value;
                NotifyPropertyChanged("Visib_b_75");
            }
        }

        private Visibility visib_L_110 = Visibility.Visible;
        public Visibility Visib_L_110
        {
            get
            {
                return visib_L_110;
            }
            set
            {
                visib_L_110 = value;
                NotifyPropertyChanged("Visib_L_110");
            }
        }

        private Visibility visib_a_110 = Visibility.Visible;
        public Visibility Visib_a_110
        {
            get
            {
                return visib_a_110;
            }
            set
            {
                visib_a_110 = value;
                NotifyPropertyChanged("Visib_a_110");
            }
        }

        private Visibility visib_b_110 = Visibility.Visible;
        public Visibility Visib_b_110
        {
            get
            {
                return visib_b_110;
            }
            set
            {
                visib_b_110 = value;
                NotifyPropertyChanged("Visib_b_110");
            }
        }

        private Visibility visib_dL_Minus15 = Visibility.Hidden;
        public Visibility Visib_dL_Minus15
        {
            get
            {
                return visib_dL_Minus15;
            }
            set
            {
                visib_dL_Minus15 = value;
                NotifyPropertyChanged("Visib_dL_Minus15");
            }
        }

        private Visibility visib_da_Minus15 = Visibility.Hidden;
        public Visibility Visib_da_Minus15
        {
            get
            {
                return visib_da_Minus15;
            }
            set
            {
                visib_da_Minus15 = value;
                NotifyPropertyChanged("Visib_da_Minus15");
            }
        }

        private Visibility visib_db_Minus15 = Visibility.Hidden;
        public Visibility Visib_db_Minus15
        {
            get
            {
                return visib_db_Minus15;
            }
            set
            {
                visib_db_Minus15 = value;
                NotifyPropertyChanged("Visib_db_Minus15");
            }
        }

        private Visibility visib_dL_15 = Visibility.Hidden;
        public Visibility Visib_dL_15
        {
            get
            {
                return visib_dL_15;
            }
            set
            {
                visib_dL_15 = value;
                NotifyPropertyChanged("Visib_dL_15");
            }
        }

        private Visibility visib_da_15 = Visibility.Hidden;
        public Visibility Visib_da_15
        {
            get
            {
                return visib_da_15;
            }
            set
            {
                visib_da_15 = value;
                NotifyPropertyChanged("Visib_da_15");
            }
        }

        private Visibility visib_db_15 = Visibility.Hidden;
        public Visibility Visib_db_15
        {
            get
            {
                return visib_db_15;
            }
            set
            {
                visib_db_15 = value;
                NotifyPropertyChanged("Visib_db_15");
            }
        }

        private Visibility visib_dL_25 = Visibility.Hidden;
        public Visibility Visib_dL_25
        {
            get
            {
                return visib_dL_25;
            }
            set
            {
                visib_dL_25 = value;
                NotifyPropertyChanged("Visib_dL_25");
            }
        }

        private Visibility visib_da_25 = Visibility.Hidden;
        public Visibility Visib_da_25
        {
            get
            {
                return visib_da_25;
            }
            set
            {
                visib_da_25 = value;
                NotifyPropertyChanged("Visib_da_25");
            }
        }

        private Visibility visib_db_25 = Visibility.Hidden;
        public Visibility Visib_db_25
        {
            get
            {
                return visib_db_25;
            }
            set
            {
                visib_db_25 = value;
                NotifyPropertyChanged("Visib_db_25");
            }
        }

        private Visibility visib_dL_45 = Visibility.Hidden;
        public Visibility Visib_dL_45
        {
            get
            {
                return visib_dL_45;
            }
            set
            {
                visib_dL_45 = value;
                NotifyPropertyChanged("Visib_dL_45");
            }
        }

        private Visibility visib_da_45 = Visibility.Hidden;
        public Visibility Visib_da_45
        {
            get
            {
                return visib_da_45;
            }
            set
            {
                visib_da_45 = value;
                NotifyPropertyChanged("Visib_da_45");
            }
        }

        private Visibility visib_db_45 = Visibility.Hidden;
        public Visibility Visib_db_45
        {
            get
            {
                return visib_db_45;
            }
            set
            {
                visib_db_45 = value;
                NotifyPropertyChanged("Visib_db_45");
            }
        }

        private Visibility visib_dL_75 = Visibility.Hidden;
        public Visibility Visib_dL_75
        {
            get
            {
                return visib_dL_75;
            }
            set
            {
                visib_dL_75 = value;
                NotifyPropertyChanged("Visib_dL_75");
            }
        }

        private Visibility visib_da_75 = Visibility.Hidden;
        public Visibility Visib_da_75
        {
            get
            {
                return visib_da_75;
            }
            set
            {
                visib_da_75 = value;
                NotifyPropertyChanged("Visib_da_75");
            }
        }

        private Visibility visib_db_75 = Visibility.Hidden;
        public Visibility Visib_db_75
        {
            get
            {
                return visib_db_75;
            }
            set
            {
                visib_db_75 = value;
                NotifyPropertyChanged("Visib_db_75");
            }
        }

        private Visibility visib_dL_110 = Visibility.Hidden;
        public Visibility Visib_dL_110
        {
            get
            {
                return visib_dL_110;
            }
            set
            {
                visib_dL_110 = value;
                NotifyPropertyChanged("Visib_dL_110");
            }
        }

        private Visibility visib_da_110 = Visibility.Hidden;
        public Visibility Visib_da_110
        {
            get
            {
                return visib_da_110;
            }
            set
            {
                visib_da_110 = value;
                NotifyPropertyChanged("Visib_da_110");
            }
        }

        private Visibility visib_db_110 = Visibility.Hidden;
        public Visibility Visib_db_110
        {
            get
            {
                return visib_db_110;
            }
            set
            {
                visib_db_110 = value;
                NotifyPropertyChanged("Visib_db_110");
            }
        }

        public SolidColorBrush Color_dE_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dE_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dE_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dE_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dE_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dE_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dE110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_L_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "L110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_a_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "a110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_b_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "b110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_Minus15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db-15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_15
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db15", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_25
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db25", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_45
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db45", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_75
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db75", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_dL_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "dL110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_da_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "da110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        public SolidColorBrush Color_db_110
        {
            get
            {
                string colorName = m_Config.GetString("Setting", "db110", "Red");
                ColorConverter colorConverter = new ColorConverter();
                Color color = (Color)colorConverter.ConvertFromInvariantString(colorName);
                SolidColorBrush result = new SolidColorBrush(color);
                return result;
            }
        }

        private ObservableCollection<StructReportData> reportDataList = new ObservableCollection<StructReportData>();
        public ObservableCollection<StructReportData> ReportDataList
        {
            get
            {
                return reportDataList;
            }
            set
            {
                reportDataList = value;
                NotifyPropertyChanged("ReportDataList");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
