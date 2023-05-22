using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HUtill
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]

    public class ProcessManager
    {

        PerformanceCounter cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        PerformanceCounter ram = new PerformanceCounter("Memory", "Available MBytes");
        string processName = Process.GetCurrentProcess().ProcessName;

        private PerformanceCounter processCpu = new PerformanceCounter("process", "% Processor Time", Process.GetCurrentProcess().ProcessName);

        public double GetCPUSystemUsagePer()
        {
            return cpu.NextValue();
        }

        public double GetRamSystemUsageValue()
        {
            return ram.NextValue();
        }

        public double GetRamSystemInstalledValue()
        {
            return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        }
         
         
    }
}
