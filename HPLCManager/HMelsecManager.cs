using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPLCManager
{
    public class HMelsecManager
    {
        private int logicalStationNumber = 0;
        public int LogicalStationNumber { get { return logicalStationNumber; } }

        ActUtlTypeLib.ActUtlType act;
        public HMelsecManager(int logicalStationNumber)
        {
            act = new ActUtlTypeLib.ActUtlType();
            act.ActLogicalStationNumber = logicalStationNumber;
        }

        public bool Open()
        {
            int result = act.Open();
            if (result == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Close()
        {
            int result = act.Close();
            if (result == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ReadBlock(string addr, int size, out int[] readData)
        {
            int[] data = new int[size];
            int result = act.ReadDeviceBlock(addr, size, out data[0]);
            readData = data;

            if (result == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool WriteBlock(string addr, int size, int[] data)
        {
            int result = act.WriteDeviceBlock(addr, size, data[0]);
            if (result == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
