using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HUtill;

namespace Report
{
    public class IniManager
    {
        private IniFile iniFile = new IniFile(Environment.CurrentDirectory + "\\Config.ini");

        public bool BlankIsOk
        {
            get
            {
                return iniFile.GetBoolian("Info", "Blank Is OK", true);
            }
        }

        public int WriteResultLimit
        {
            get
            {
                return iniFile.GetInt32("PLC", "Write Result Limit", 2000);
            }
        }

        public int LogicalStationNumber
        {
            get
            {
                return iniFile.GetInt32("PLC", "Logical Station Number", 0);
            }
            set
            {
                iniFile.WriteValue("PLC", "Logical Station Number", value);
            }
        }

        public int ReadTickInterval
        {
            get
            {
                return iniFile.GetInt32("PLC", "Read Tick Interval", 300);
            }
            set
            {
                iniFile.WriteValue("PLC", "Read Tick Interval", value);
            }
        }

        public string ReadAddr
        {
            get
            {
                return iniFile.GetString("PLC", "Read Addr", "");
            }
            set
            {
                iniFile.WriteValue("PLC", "Read Addr", value);
            }
        }
        public int ReadSize
        {
            get
            {
                return iniFile.GetInt32("PLC", "Read Size", 1);
            }
            set
            {
                iniFile.WriteValue("PLC", "Read Size", value);
            }
        }

        // PLC Address
        public string AddrHeartBeat { get { return iniFile.GetString("PLC Addr", "HeartBeat", "D1500"); } }
        public string AddrComplete { get { return iniFile.GetString("PLC Addr", "Complete", "D1501"); } }
        public string AddrFR_Result { get { return iniFile.GetString("PLC Addr", "FR_Result", "D1502"); } }
        public string AddrRR_Result { get { return iniFile.GetString("PLC Addr", "RR_Result", "D1503"); } }
        public string AddrByPass { get { return iniFile.GetString("PLC Addr", "ByPass", "D5104"); } }
        public string AddrBodyNumber { get { return iniFile.GetString("PLC Addr", "BodyNumber", "D1510"); } }

        // PLC Read Size
        public int ReadSizeHeartBeat { get { return iniFile.GetInt32("PLC Read Size", "HeartBeat", 1); } }
        public int ReadSizeComplete { get { return iniFile.GetInt32("PLC Addr", "Complete", 1); } }
        public int ReadSizeFR_Result { get { return iniFile.GetInt32("PLC Read Size", "FR_Result", 1); } }
        public int ReadSizeRR_Result { get { return iniFile.GetInt32("PLC Read Size", "RR_Result", 1); } }
        public int ReadSizeByPass { get { return iniFile.GetInt32("PLC Addr", "ByPass", 1); } }
        public int ReadSizeBodyNumber { get { return iniFile.GetInt32("PLC Read Size", "BodyNumber", 4); } }
    }
}
