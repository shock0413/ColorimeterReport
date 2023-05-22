using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using HUtill;
using Microsoft.WindowsAPICodePack.Dialogs;
using HPLCManager;
using System.Threading;
using Hansero;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Controls;
using System.Diagnostics;

namespace Report
{
    public partial class MainEngine
    {
        private MetroWindow window;
        private IDialogCoordinator dialogCoordinator;
        private IniFile m_Config;
        private HMelsecManager plcManager;
        private LogManager logManager;
        private IniManager iniManager;
        private Thread plcThread;
        private Thread plcHeartBeatThread;
        private DBManager.DBManager dbManager;

        public MainEngine()
        {
            logManager = new LogManager(true, true);
            m_Config = new IniFile(Environment.CurrentDirectory + "\\Config.ini");
        }

        public MainEngine(MetroWindow window, IDialogCoordinator instance)
        {
            logManager = new LogManager(true, true);
            m_Config = new IniFile(Environment.CurrentDirectory + "\\Config.ini");
            iniManager = new IniManager();
            this.window = window;
            this.dialogCoordinator = instance;

            // 차종 콤보박스 리스트 추가
            InitCarKindList();

            // 색상 콤보박스 리스트 추가
            InitColorList();

            // PLC 초기화
            InitPLC();

            // 데이터베이스 초기화
            InitDB();
        }

        private void InitDB()
        {
            dbManager = new DBManager.DBManager();

            // dbManager.Open();

            Thread dbThread = new Thread(new ThreadStart(dbThreadDo));
            dbThread.Start();

            /*
            Thread dbResultThread = new Thread(new ThreadStart(dbResultThreadDo));
            dbResultThread.Start();
            */
        }

        private void dbResultThreadDo()
        {
            while (isRunning)
            {
                try
                {
                    if (!isRunning)
                    {
                        break;
                    }

                    // 결과 판정 확인 및 정리
                    int loadDays = m_Config.GetInt32("DB", "Load Days", 0) * -1;
                    DateTime stDateTime = DateTime.Now.AddDays(loadDays);
                    

                    int threadDelay = m_Config.GetInt32("DB", "Thread Delay", 1000);
                    Thread.Sleep(threadDelay);
                }
                catch (Exception e)
                {
                    logManager.Equals("DB Result Thread 에러 : " + e.Message);
                }
            }
        }

        private void dbThreadDo()
        {
            while (isRunning)
            {
                try
                {
                    string dir = m_Config.GetString("Directory", "Result", "E:\\Result");

                    if (Directory.Exists(dir))
                    {
                        // string[] files = Directory.GetFiles(dir).ToList().OrderBy(x => x).ToArray();

                        int loadDays = m_Config.GetInt32("DB", "Load Days", 0) * -1;
                        DateTime stDateTime = DateTime.Now.AddDays(loadDays);

                        List<string> fileList = Directory.GetFiles(dir).ToList();

                        fileList.RemoveAll(x => Convert.ToDateTime(Path.GetFileNameWithoutExtension(x).Substring(0, 4) + "-" + Path.GetFileNameWithoutExtension(x).Substring(4, 2) + "-" + Path.GetFileNameWithoutExtension(x).Substring(6, 2) + " " + Path.GetFileNameWithoutExtension(x).Substring(8, 2) + ":" + Path.GetFileNameWithoutExtension(x).Substring(10, 2) + ":" + Path.GetFileNameWithoutExtension(x).Substring(12, 2)) < stDateTime);
                        string[] files = fileList.OrderBy(x => x).ToArray();

                        for (int i = 0; i < files.Length; i++)
                        {
                            if (!isRunning)
                            {
                                break;
                            }

                            StructReportData structReportData = new StructReportData();

                            string filepath = files[i];

                            string filename = Path.GetFileNameWithoutExtension(filepath);
                            int year = Convert.ToInt32(filename.Substring(0, 4));
                            int month = Convert.ToInt32(filename.Substring(4, 2));
                            int day = Convert.ToInt32(filename.Substring(6, 2));
                            int hour = Convert.ToInt32(filename.Substring(8, 2));
                            int min = Convert.ToInt32(filename.Substring(10, 2));
                            int sec = Convert.ToInt32(filename.Substring(12, 2));
                            string dateTime = year + "-" + month + "-" + day + " " + hour + ":" + min + ":" + sec;
                            DateTime startDateTime = new DateTime(year, month, day, hour, min, sec);

                            string model = "";
                            string color = "";
                            string comment = "";
                            string bodyNumber = "";

                            string allText = File.ReadAllText(filepath);

                            string[] headerSplit = allText.Split(new string[] { "</header>" }, StringSplitOptions.None);

                            if (headerSplit.Length > 0)
                            {
                                XmlDocument xmlFile = new XmlDocument();

                                try
                                {
                                    headerSplit[0] += "</header>";
                                    xmlFile.LoadXml(headerSplit[0]);

                                    XmlNodeList xmlList = xmlFile.GetElementsByTagName("header");

                                    for (int j = 0; j < xmlList.Count; j++)
                                    {
                                        XmlNode item = xmlList[j];
                                        model = item["model"].InnerText;
                                        color = item["color"].InnerText;
                                        comment = item["comment"].InnerText;
                                        bodyNumber = item["bodyNumber"].InnerText;
                                    }
                                }
                                catch (Exception e)
                                {
                                    logManager.Error(e.Message);
                                }

                                if (headerSplit.Length > 1)
                                {
                                    string[] measurementSplit = headerSplit[1].Split(new string[] { "</measurement>" }, StringSplitOptions.None);

                                    if (measurementSplit.Length > 0)
                                    {
                                        structReportData.StartDateTime = startDateTime;
                                        structReportData.DateTime = startDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                        structReportData.Model = model;
                                        structReportData.Color = color;
                                        structReportData.Comment = comment;
                                        structReportData.BodyNumber = bodyNumber;

                                        for (int j = 0; j < measurementSplit.Length; j++)
                                        {
                                            if (measurementSplit[j].Contains("<measurement>"))
                                            {
                                                try
                                                {
                                                    measurementSplit[j] += "</measurement>";
                                                    xmlFile.LoadXml(measurementSplit[j]);

                                                    string measurementText = xmlFile["measurement"].InnerXml;

                                                    xmlFile.LoadXml(measurementText);

                                                    XmlNodeList xmlList = xmlFile.GetElementsByTagName("checkzone");

                                                    for (int k = 0; k < xmlList.Count; k++)
                                                    {
                                                        XmlNode item = xmlList[k];
                                                        string index = item["index"].InnerText;
                                                        string dE_minus15 = "0";

                                                        if (item["dE-15"].InnerText != null)
                                                        {
                                                            dE_minus15 = item["dE-15"].InnerText;
                                                        }

                                                        string dE_15 = "0";

                                                        if (item["dE15"] != null)
                                                        {
                                                            dE_15 = item["dE15"].InnerText;
                                                        }

                                                        string dE_25 = "0";

                                                        if (item["dE25"] != null)
                                                        {
                                                            dE_25 = item["dE25"].InnerText;
                                                        }

                                                        string dE_45 = "0";

                                                        if (item["dE45"] != null)
                                                        {
                                                            dE_45 = item["dE45"].InnerText;
                                                        }

                                                        string dE_75 = "0";

                                                        if (item["dE75"] != null)
                                                        {
                                                            dE_75 = item["dE75"].InnerText;
                                                        }

                                                        string dE_110 = "0";

                                                        if (item["dE110"] != null)
                                                        {
                                                            dE_110 = item["dE110"].InnerText;
                                                        }

                                                        string L_minus15 = "0";

                                                        if (item["L-15"] != null)
                                                        {
                                                            L_minus15 = item["L-15"].InnerText;
                                                        }

                                                        string a_minus15 = "0";

                                                        if (item["a-15"] != null)
                                                        {
                                                            a_minus15 = item["a-15"].InnerText;
                                                        }

                                                        string b_minus15 = "0";

                                                        if (item["b-15"] != null)
                                                        {
                                                            b_minus15 = item["b-15"].InnerText;
                                                        }

                                                        string L_15 = "0";

                                                        if (item["L15"] != null)
                                                        {
                                                            L_15 = item["L15"].InnerText;
                                                        }

                                                        string a_15 = "0";

                                                        if (item["a15"] != null)
                                                        {
                                                            a_15 = item["a15"].InnerText;
                                                        }

                                                        string b_15 = "0";

                                                        if (item["b15"] != null)
                                                        {
                                                            b_15 = item["b15"].InnerText;
                                                        }

                                                        string L_25 = "0";

                                                        if (item["L25"] != null)
                                                        {
                                                            L_25 = item["L25"].InnerText;
                                                        }

                                                        string a_25 = "0";

                                                        if (item["a25"] != null)
                                                        {
                                                            a_25 = item["a25"].InnerText;
                                                        }

                                                        string b_25 = "0";

                                                        if (item["b25"] != null)
                                                        {
                                                            b_25 = item["b25"].InnerText;
                                                        }

                                                        string L_45 = "0";

                                                        if (item["L45"] != null)
                                                        {
                                                            L_45 = item["L45"].InnerText;
                                                        }

                                                        string a_45 = "0";

                                                        if (item["a45"] != null)
                                                        {
                                                            a_45 = item["a45"].InnerText;
                                                        }

                                                        string b_45 = "0";

                                                        if (item["b45"] != null)
                                                        {
                                                            b_45 = item["b45"].InnerText;
                                                        }

                                                        string L_75 = "0";

                                                        if (item["L75"] != null)
                                                        {
                                                            L_75 = item["L75"].InnerText;
                                                        }

                                                        string a_75 = "0";

                                                        if (item["a75"] != null)
                                                        {
                                                            a_75 = item["a75"].InnerText;
                                                        }

                                                        string b_75 = "0";

                                                        if (item["b75"] != null)
                                                        {
                                                            b_75 = item["b75"].InnerText;
                                                        }

                                                        string L_110 = "0";

                                                        if (item["L110"] != null)
                                                        {
                                                            L_110 = item["L110"].InnerText;
                                                        }

                                                        string a_110 = "0";

                                                        if (item["a110"] != null)
                                                        {
                                                            a_110 = item["a110"].InnerText;
                                                        }

                                                        string b_110 = "0";

                                                        if (item["b110"] != null)
                                                        {
                                                            b_110 = item["b110"].InnerText;
                                                        }

                                                        string dL_minus15 = "0";

                                                        if (item["dL-15"] != null)
                                                        {
                                                            dL_minus15 = item["dL-15"].InnerText;
                                                        }

                                                        string da_minus15 = "0";

                                                        if (item["da-15"] != null)
                                                        {
                                                            da_minus15 = item["da-15"].InnerText;
                                                        }

                                                        string db_minus15 = "0";

                                                        if (item["db-15"] != null)
                                                        {
                                                            db_minus15 = item["db-15"].InnerText;
                                                        }

                                                        string dL_15 = "0";

                                                        if (item["dL15"] != null)
                                                        {
                                                            dL_15 = item["dL15"].InnerText;
                                                        }

                                                        string da_15 = "0";

                                                        if (item["da15"] != null)
                                                        {
                                                            da_15 = item["da15"].InnerText;
                                                        }

                                                        string db_15 = "0";

                                                        if (item["db15"] != null)
                                                        {
                                                            db_15 = item["db15"].InnerText;
                                                        }

                                                        string dL_25 = "0";

                                                        if (item["dL25"] != null)
                                                        {
                                                            dL_25 = item["dL25"].InnerText;
                                                        }

                                                        string da_25 = "0";

                                                        if (item["da25"] != null)
                                                        {
                                                            da_25 = item["da25"].InnerText;
                                                        }

                                                        string db_25 = "0";

                                                        if (item["db25"] != null)
                                                        {
                                                            db_25 = item["db25"].InnerText;
                                                        }

                                                        string dL_45 = "0";

                                                        if (item["dL45"] != null)
                                                        {
                                                            dL_45 = item["dL45"].InnerText;
                                                        }

                                                        string da_45 = "0";

                                                        if (item["da45"] != null)
                                                        {
                                                            da_45 = item["da45"].InnerText;
                                                        }

                                                        string db_45 = "0";

                                                        if (item["db45"] != null)
                                                        {
                                                            db_45 = item["db45"].InnerText;
                                                        }

                                                        string dL_75 = "0";

                                                        if (item["dL75"] != null)
                                                        {
                                                            dL_75 = item["dL75"].InnerText;
                                                        }

                                                        string da_75 = "0";

                                                        if (item["da75"] != null)
                                                        {
                                                            da_75 = item["da75"].InnerText;
                                                        }

                                                        string db_75 = "0";

                                                        if (item["db75"] != null)
                                                        {
                                                            db_75 = item["db75"].InnerText;
                                                        }

                                                        string dL_110 = "0";

                                                        if (item["dL110"] != null)
                                                        {
                                                            dL_110 = item["dL110"].InnerText;
                                                        }

                                                        string da_110 = "0";

                                                        if (item["da110"] != null)
                                                        {
                                                            da_110 = item["da110"].InnerText;
                                                        }

                                                        string db_110 = "0";

                                                        if (item["db110"] != null)
                                                        {
                                                            db_110 = item["db110"].InnerText;
                                                        }

                                                        StructMeasurement structMeasurement = new StructMeasurement(index, dE_minus15, dE_15, dE_25, dE_45, dE_75, dE_110, L_minus15, a_minus15, b_minus15, L_15, a_15, b_15, L_25, a_25, b_25, L_45, a_45, b_45, L_75, a_75, b_75, L_110, a_110, b_110, dL_minus15, da_minus15, db_minus15, dL_15, da_15, db_15, dL_25, da_25, db_25, dL_45, da_45, db_45, dL_75, da_75, db_75, dL_110, da_110, db_110);
                                                        StructSensorData structSensorData = new StructSensorData(dateTime, model, color, bodyNumber, structMeasurement);

                                                        if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "FR_FENDA", "1"))
                                                        {
                                                            structReportData.FR_FENDA_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.FR_FENDA_dE_15 = structMeasurement.dE_15;
                                                            structReportData.FR_FENDA_dE_25 = structMeasurement.dE_25;
                                                            structReportData.FR_FENDA_dE_45 = structMeasurement.dE_45;
                                                            structReportData.FR_FENDA_dE_75 = structMeasurement.dE_75;
                                                            structReportData.FR_FENDA_dE_110 = structMeasurement.dE_110;
                                                            structReportData.FR_FENDA_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.FR_FENDA_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.FR_FENDA_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.FR_FENDA_L_15 = structMeasurement.L_15;
                                                            structReportData.FR_FENDA_a_15 = structMeasurement.a_15;
                                                            structReportData.FR_FENDA_b_15 = structMeasurement.b_15;
                                                            structReportData.FR_FENDA_L_25 = structMeasurement.L_25;
                                                            structReportData.FR_FENDA_a_25 = structMeasurement.a_25;
                                                            structReportData.FR_FENDA_b_25 = structMeasurement.b_25;
                                                            structReportData.FR_FENDA_L_45 = structMeasurement.L_45;
                                                            structReportData.FR_FENDA_a_45 = structMeasurement.a_45;
                                                            structReportData.FR_FENDA_b_45 = structMeasurement.b_45;
                                                            structReportData.FR_FENDA_L_75 = structMeasurement.L_75;
                                                            structReportData.FR_FENDA_a_75 = structMeasurement.a_75;
                                                            structReportData.FR_FENDA_b_75 = structMeasurement.b_75;
                                                            structReportData.FR_FENDA_L_110 = structMeasurement.L_110;
                                                            structReportData.FR_FENDA_a_110 = structMeasurement.a_110;
                                                            structReportData.FR_FENDA_b_110 = structMeasurement.b_110;
                                                            structReportData.FR_FENDA_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.FR_FENDA_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.FR_FENDA_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.FR_FENDA_dL_15 = structMeasurement.dL_15;
                                                            structReportData.FR_FENDA_da_15 = structMeasurement.da_15;
                                                            structReportData.FR_FENDA_db_15 = structMeasurement.db_15;
                                                            structReportData.FR_FENDA_dL_25 = structMeasurement.dL_25;
                                                            structReportData.FR_FENDA_da_25 = structMeasurement.da_25;
                                                            structReportData.FR_FENDA_db_25 = structMeasurement.db_25;
                                                            structReportData.FR_FENDA_dL_45 = structMeasurement.dL_45;
                                                            structReportData.FR_FENDA_da_45 = structMeasurement.da_45;
                                                            structReportData.FR_FENDA_db_45 = structMeasurement.db_45;
                                                            structReportData.FR_FENDA_dL_75 = structMeasurement.dL_75;
                                                            structReportData.FR_FENDA_da_75 = structMeasurement.da_75;
                                                            structReportData.FR_FENDA_db_75 = structMeasurement.db_75;
                                                            structReportData.FR_FENDA_dL_110 = structMeasurement.dL_110;
                                                            structReportData.FR_FENDA_da_110 = structMeasurement.da_110;
                                                            structReportData.FR_FENDA_db_110 = structMeasurement.db_110;
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "FR_BUMPER", "2"))
                                                        {
                                                            structReportData.FR_BUMPER_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.FR_BUMPER_dE_15 = structMeasurement.dE_15;
                                                            structReportData.FR_BUMPER_dE_25 = structMeasurement.dE_25;
                                                            structReportData.FR_BUMPER_dE_45 = structMeasurement.dE_45;
                                                            structReportData.FR_BUMPER_dE_75 = structMeasurement.dE_75;
                                                            structReportData.FR_BUMPER_dE_110 = structMeasurement.dE_110;
                                                            structReportData.FR_BUMPER_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.FR_BUMPER_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.FR_BUMPER_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.FR_BUMPER_L_15 = structMeasurement.L_15;
                                                            structReportData.FR_BUMPER_a_15 = structMeasurement.a_15;
                                                            structReportData.FR_BUMPER_b_15 = structMeasurement.b_15;
                                                            structReportData.FR_BUMPER_L_25 = structMeasurement.L_25;
                                                            structReportData.FR_BUMPER_a_25 = structMeasurement.a_25;
                                                            structReportData.FR_BUMPER_b_25 = structMeasurement.b_25;
                                                            structReportData.FR_BUMPER_L_45 = structMeasurement.L_45;
                                                            structReportData.FR_BUMPER_a_45 = structMeasurement.a_45;
                                                            structReportData.FR_BUMPER_b_45 = structMeasurement.b_45;
                                                            structReportData.FR_BUMPER_L_75 = structMeasurement.L_75;
                                                            structReportData.FR_BUMPER_a_75 = structMeasurement.a_75;
                                                            structReportData.FR_BUMPER_b_75 = structMeasurement.b_75;
                                                            structReportData.FR_BUMPER_L_110 = structMeasurement.L_110;
                                                            structReportData.FR_BUMPER_a_110 = structMeasurement.a_110;
                                                            structReportData.FR_BUMPER_b_110 = structMeasurement.b_110;
                                                            structReportData.FR_BUMPER_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.FR_BUMPER_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.FR_BUMPER_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.FR_BUMPER_dL_15 = structMeasurement.dL_15;
                                                            structReportData.FR_BUMPER_da_15 = structMeasurement.da_15;
                                                            structReportData.FR_BUMPER_db_15 = structMeasurement.db_15;
                                                            structReportData.FR_BUMPER_dL_25 = structMeasurement.dL_25;
                                                            structReportData.FR_BUMPER_da_25 = structMeasurement.da_25;
                                                            structReportData.FR_BUMPER_db_25 = structMeasurement.db_25;
                                                            structReportData.FR_BUMPER_dL_45 = structMeasurement.dL_45;
                                                            structReportData.FR_BUMPER_da_45 = structMeasurement.da_45;
                                                            structReportData.FR_BUMPER_db_45 = structMeasurement.db_45;
                                                            structReportData.FR_BUMPER_dL_75 = structMeasurement.dL_75;
                                                            structReportData.FR_BUMPER_da_75 = structMeasurement.da_75;
                                                            structReportData.FR_BUMPER_db_75 = structMeasurement.db_75;
                                                            structReportData.FR_BUMPER_dL_110 = structMeasurement.dL_110;
                                                            structReportData.FR_BUMPER_da_110 = structMeasurement.da_110;
                                                            structReportData.FR_BUMPER_db_110 = structMeasurement.db_110;
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "RR_QTR", "3"))
                                                        {
                                                            structReportData.RR_QTR_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.RR_QTR_dE_15 = structMeasurement.dE_15;
                                                            structReportData.RR_QTR_dE_25 = structMeasurement.dE_25;
                                                            structReportData.RR_QTR_dE_45 = structMeasurement.dE_45;
                                                            structReportData.RR_QTR_dE_75 = structMeasurement.dE_75;
                                                            structReportData.RR_QTR_dE_110 = structMeasurement.dE_110;
                                                            structReportData.RR_QTR_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.RR_QTR_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.RR_QTR_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.RR_QTR_L_15 = structMeasurement.L_15;
                                                            structReportData.RR_QTR_a_15 = structMeasurement.a_15;
                                                            structReportData.RR_QTR_b_15 = structMeasurement.b_15;
                                                            structReportData.RR_QTR_L_25 = structMeasurement.L_25;
                                                            structReportData.RR_QTR_a_25 = structMeasurement.a_25;
                                                            structReportData.RR_QTR_b_25 = structMeasurement.b_25;
                                                            structReportData.RR_QTR_L_45 = structMeasurement.L_45;
                                                            structReportData.RR_QTR_a_45 = structMeasurement.a_45;
                                                            structReportData.RR_QTR_b_45 = structMeasurement.b_45;
                                                            structReportData.RR_QTR_L_75 = structMeasurement.L_75;
                                                            structReportData.RR_QTR_a_75 = structMeasurement.a_75;
                                                            structReportData.RR_QTR_b_75 = structMeasurement.b_75;
                                                            structReportData.RR_QTR_L_110 = structMeasurement.L_110;
                                                            structReportData.RR_QTR_a_110 = structMeasurement.a_110;
                                                            structReportData.RR_QTR_b_110 = structMeasurement.b_110;
                                                            structReportData.RR_QTR_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.RR_QTR_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.RR_QTR_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.RR_QTR_dL_15 = structMeasurement.dL_15;
                                                            structReportData.RR_QTR_da_15 = structMeasurement.da_15;
                                                            structReportData.RR_QTR_db_15 = structMeasurement.db_15;
                                                            structReportData.RR_QTR_dL_25 = structMeasurement.dL_25;
                                                            structReportData.RR_QTR_da_25 = structMeasurement.da_25;
                                                            structReportData.RR_QTR_db_25 = structMeasurement.db_25;
                                                            structReportData.RR_QTR_dL_45 = structMeasurement.dL_45;
                                                            structReportData.RR_QTR_da_45 = structMeasurement.da_45;
                                                            structReportData.RR_QTR_db_45 = structMeasurement.db_45;
                                                            structReportData.RR_QTR_dL_75 = structMeasurement.dL_75;
                                                            structReportData.RR_QTR_da_75 = structMeasurement.da_75;
                                                            structReportData.RR_QTR_db_75 = structMeasurement.db_75;
                                                            structReportData.RR_QTR_dL_110 = structMeasurement.dL_110;
                                                            structReportData.RR_QTR_da_110 = structMeasurement.da_110;
                                                            structReportData.RR_QTR_db_110 = structMeasurement.db_110;
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "RR_BUMPER", "4"))
                                                        {
                                                            structReportData.RR_BUMPER_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.RR_BUMPER_dE_15 = structMeasurement.dE_15;
                                                            structReportData.RR_BUMPER_dE_25 = structMeasurement.dE_25;
                                                            structReportData.RR_BUMPER_dE_45 = structMeasurement.dE_45;
                                                            structReportData.RR_BUMPER_dE_75 = structMeasurement.dE_75;
                                                            structReportData.RR_BUMPER_dE_110 = structMeasurement.dE_110;
                                                            structReportData.RR_BUMPER_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.RR_BUMPER_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.RR_BUMPER_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.RR_BUMPER_L_15 = structMeasurement.L_15;
                                                            structReportData.RR_BUMPER_a_15 = structMeasurement.a_15;
                                                            structReportData.RR_BUMPER_b_15 = structMeasurement.b_15;
                                                            structReportData.RR_BUMPER_L_25 = structMeasurement.L_25;
                                                            structReportData.RR_BUMPER_a_25 = structMeasurement.a_25;
                                                            structReportData.RR_BUMPER_b_25 = structMeasurement.b_25;
                                                            structReportData.RR_BUMPER_L_45 = structMeasurement.L_45;
                                                            structReportData.RR_BUMPER_a_45 = structMeasurement.a_45;
                                                            structReportData.RR_BUMPER_b_45 = structMeasurement.b_45;
                                                            structReportData.RR_BUMPER_L_75 = structMeasurement.L_75;
                                                            structReportData.RR_BUMPER_a_75 = structMeasurement.a_75;
                                                            structReportData.RR_BUMPER_b_75 = structMeasurement.b_75;
                                                            structReportData.RR_BUMPER_L_110 = structMeasurement.L_110;
                                                            structReportData.RR_BUMPER_a_110 = structMeasurement.a_110;
                                                            structReportData.RR_BUMPER_b_110 = structMeasurement.b_110;
                                                            structReportData.RR_BUMPER_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.RR_BUMPER_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.RR_BUMPER_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.RR_BUMPER_dL_15 = structMeasurement.dL_15;
                                                            structReportData.RR_BUMPER_da_15 = structMeasurement.da_15;
                                                            structReportData.RR_BUMPER_db_15 = structMeasurement.db_15;
                                                            structReportData.RR_BUMPER_dL_25 = structMeasurement.dL_25;
                                                            structReportData.RR_BUMPER_da_25 = structMeasurement.da_25;
                                                            structReportData.RR_BUMPER_db_25 = structMeasurement.db_25;
                                                            structReportData.RR_BUMPER_dL_45 = structMeasurement.dL_45;
                                                            structReportData.RR_BUMPER_da_45 = structMeasurement.da_45;
                                                            structReportData.RR_BUMPER_db_45 = structMeasurement.db_45;
                                                            structReportData.RR_BUMPER_dL_75 = structMeasurement.dL_75;
                                                            structReportData.RR_BUMPER_da_75 = structMeasurement.da_75;
                                                            structReportData.RR_BUMPER_db_75 = structMeasurement.db_75;
                                                            structReportData.RR_BUMPER_dL_110 = structMeasurement.dL_110;
                                                            structReportData.RR_BUMPER_da_110 = structMeasurement.da_110;
                                                            structReportData.RR_BUMPER_db_110 = structMeasurement.db_110;
                                                        }

                                                        string qry = "select * from result_tbl where model = '" + structReportData.Model + "' and bodyNumber = '" + structReportData.BodyNumber + "' and seq = '" + structReportData.Comment + "';";
                                                        List<Dictionary<string, string>> list = dbManager.ExecuteReader(qry);

                                                        try
                                                        {
                                                            window.Dispatcher.Invoke(() =>
                                                            {
                                                                DBStateColor = Brushes.LimeGreen;
                                                            });
                                                        }
                                                        catch
                                                        {

                                                        }

                                                        string fr_fenda_L_Minus15 = structReportData.FR_FENDA_L_Minus15 == null || structReportData.FR_FENDA_L_Minus15 == "" ? "null" : structReportData.FR_FENDA_L_Minus15;
                                                        string fr_fenda_a_Minus15 = structReportData.FR_FENDA_a_Minus15 == null || structReportData.FR_FENDA_a_Minus15 == "" ? "null" : structReportData.FR_FENDA_a_Minus15;
                                                        string fr_fenda_b_Minus15 = structReportData.FR_FENDA_b_Minus15 == null || structReportData.FR_FENDA_b_Minus15 == "" ? "null" : structReportData.FR_FENDA_b_Minus15;
                                                        string fr_fenda_L_15 = structReportData.FR_FENDA_L_15 == null || structReportData.FR_FENDA_L_15 == "" ? "null" : structReportData.FR_FENDA_L_15;
                                                        string fr_fenda_a_15 = structReportData.FR_FENDA_a_15 == null || structReportData.FR_FENDA_a_15 == "" ? "null" : structReportData.FR_FENDA_a_15;
                                                        string fr_fenda_b_15 = structReportData.FR_FENDA_b_15 == null || structReportData.FR_FENDA_b_15 == "" ? "null" : structReportData.FR_FENDA_b_15;
                                                        string fr_fenda_L_25 = structReportData.FR_FENDA_L_25 == null || structReportData.FR_FENDA_L_25 == "" ? "null" : structReportData.FR_FENDA_L_25;
                                                        string fr_fenda_a_25 = structReportData.FR_FENDA_a_25 == null || structReportData.FR_FENDA_a_25 == "" ? "null" : structReportData.FR_FENDA_a_25;
                                                        string fr_fenda_b_25 = structReportData.FR_FENDA_b_25 == null || structReportData.FR_FENDA_b_25 == "" ? "null" : structReportData.FR_FENDA_b_25;
                                                        string fr_fenda_L_45 = structReportData.FR_FENDA_L_45 == null || structReportData.FR_FENDA_L_45 == "" ? "null" : structReportData.FR_FENDA_L_45;
                                                        string fr_fenda_a_45 = structReportData.FR_FENDA_a_45 == null || structReportData.FR_FENDA_a_45 == "" ? "null" : structReportData.FR_FENDA_a_45;
                                                        string fr_fenda_b_45 = structReportData.FR_FENDA_b_45 == null || structReportData.FR_FENDA_b_45 == "" ? "null" : structReportData.FR_FENDA_b_45;
                                                        string fr_fenda_L_75 = structReportData.FR_FENDA_L_75 == null || structReportData.FR_FENDA_L_75 == "" ? "null" : structReportData.FR_FENDA_L_75;
                                                        string fr_fenda_a_75 = structReportData.FR_FENDA_a_75 == null || structReportData.FR_FENDA_a_75 == "" ? "null" : structReportData.FR_FENDA_a_75;
                                                        string fr_fenda_b_75 = structReportData.FR_FENDA_b_75 == null || structReportData.FR_FENDA_b_75 == "" ? "null" : structReportData.FR_FENDA_b_75;
                                                        string fr_fenda_L_110 = structReportData.FR_FENDA_L_110 == null || structReportData.FR_FENDA_L_110 == "" ? "null" : structReportData.FR_FENDA_L_110;
                                                        string fr_fenda_a_110 = structReportData.FR_FENDA_a_110 == null || structReportData.FR_FENDA_a_110 == "" ? "null" : structReportData.FR_FENDA_a_110;
                                                        string fr_fenda_b_110 = structReportData.FR_FENDA_b_110 == null || structReportData.FR_FENDA_b_110 == "" ? "null" : structReportData.FR_FENDA_b_110;
                                                        string fr_bumper_L_Minus15 = structReportData.FR_BUMPER_L_Minus15 == null || structReportData.FR_BUMPER_L_Minus15 == "" ? "null" : structReportData.FR_BUMPER_L_Minus15;
                                                        string fr_bumper_a_Minus15 = structReportData.FR_BUMPER_a_Minus15 == null || structReportData.FR_BUMPER_a_Minus15 == "" ? "null" : structReportData.FR_BUMPER_a_Minus15;
                                                        string fr_bumper_b_Minus15 = structReportData.FR_BUMPER_b_Minus15 == null || structReportData.FR_BUMPER_b_Minus15 == "" ? "null" : structReportData.FR_BUMPER_b_Minus15;
                                                        string fr_bumper_L_15 = structReportData.FR_BUMPER_L_15 == null || structReportData.FR_BUMPER_L_15 == "" ? "null" : structReportData.FR_BUMPER_L_15;
                                                        string fr_bumper_a_15 = structReportData.FR_BUMPER_a_15 == null || structReportData.FR_BUMPER_a_15 == "" ? "null" : structReportData.FR_BUMPER_a_15;
                                                        string fr_bumper_b_15 = structReportData.FR_BUMPER_b_15 == null || structReportData.FR_BUMPER_b_15 == "" ? "null" : structReportData.FR_BUMPER_b_15;
                                                        string fr_bumper_L_25 = structReportData.FR_BUMPER_L_25 == null || structReportData.FR_BUMPER_L_25 == "" ? "null" : structReportData.FR_BUMPER_L_25;
                                                        string fr_bumper_a_25 = structReportData.FR_BUMPER_a_25 == null || structReportData.FR_BUMPER_a_25 == "" ? "null" : structReportData.FR_BUMPER_a_25;
                                                        string fr_bumper_b_25 = structReportData.FR_BUMPER_b_25 == null || structReportData.FR_BUMPER_b_25 == "" ? "null" : structReportData.FR_BUMPER_b_25;
                                                        string fr_bumper_L_45 = structReportData.FR_BUMPER_L_45 == null || structReportData.FR_BUMPER_L_45 == "" ? "null" : structReportData.FR_BUMPER_L_45;
                                                        string fr_bumper_a_45 = structReportData.FR_BUMPER_a_45 == null || structReportData.FR_BUMPER_a_45 == "" ? "null" : structReportData.FR_BUMPER_a_45;
                                                        string fr_bumper_b_45 = structReportData.FR_BUMPER_b_45 == null || structReportData.FR_BUMPER_b_45 == "" ? "null" : structReportData.FR_BUMPER_b_45;
                                                        string fr_bumper_L_75 = structReportData.FR_BUMPER_L_75 == null || structReportData.FR_BUMPER_L_75 == "" ? "null" : structReportData.FR_BUMPER_L_75;
                                                        string fr_bumper_a_75 = structReportData.FR_BUMPER_a_75 == null || structReportData.FR_BUMPER_a_75 == "" ? "null" : structReportData.FR_BUMPER_a_75;
                                                        string fr_bumper_b_75 = structReportData.FR_BUMPER_b_75 == null || structReportData.FR_BUMPER_b_75 == "" ? "null" : structReportData.FR_BUMPER_b_75;
                                                        string fr_bumper_L_110 = structReportData.FR_BUMPER_L_110 == null || structReportData.FR_BUMPER_L_110 == "" ? "null" : structReportData.FR_BUMPER_L_110;
                                                        string fr_bumper_a_110 = structReportData.FR_BUMPER_a_110 == null || structReportData.FR_BUMPER_a_110 == "" ? "null" : structReportData.FR_BUMPER_a_110;
                                                        string fr_bumper_b_110 = structReportData.FR_BUMPER_b_110 == null || structReportData.FR_BUMPER_b_110 == "" ? "null" : structReportData.FR_BUMPER_b_110;
                                                        string rr_qtr_L_Minus15 = structReportData.RR_QTR_L_Minus15 == null || structReportData.RR_QTR_L_Minus15 == "" ? "null" : structReportData.RR_QTR_L_Minus15;
                                                        string rr_qtr_a_Minus15 = structReportData.RR_QTR_a_Minus15 == null || structReportData.RR_QTR_a_Minus15 == "" ? "null" : structReportData.RR_QTR_a_Minus15;
                                                        string rr_qtr_b_Minus15 = structReportData.RR_QTR_b_Minus15 == null || structReportData.RR_QTR_b_Minus15 == "" ? "null" : structReportData.RR_QTR_b_Minus15;
                                                        string rr_qtr_L_15 = structReportData.RR_QTR_L_15 == null || structReportData.RR_QTR_L_15 == "" ? "null" : structReportData.RR_QTR_L_15;
                                                        string rr_qtr_a_15 = structReportData.RR_QTR_a_15 == null || structReportData.RR_QTR_a_15 == "" ? "null" : structReportData.RR_QTR_a_15;
                                                        string rr_qtr_b_15 = structReportData.RR_QTR_b_15 == null || structReportData.RR_QTR_b_15 == "" ? "null" : structReportData.RR_QTR_b_15;
                                                        string rr_qtr_L_25 = structReportData.RR_QTR_L_25 == null || structReportData.RR_QTR_L_25 == "" ? "null" : structReportData.RR_QTR_L_25;
                                                        string rr_qtr_a_25 = structReportData.RR_QTR_a_25 == null || structReportData.RR_QTR_a_25 == "" ? "null" : structReportData.RR_QTR_a_25;
                                                        string rr_qtr_b_25 = structReportData.RR_QTR_b_25 == null || structReportData.RR_QTR_b_25 == "" ? "null" : structReportData.RR_QTR_b_25;
                                                        string rr_qtr_L_45 = structReportData.RR_QTR_L_45 == null || structReportData.RR_QTR_L_45 == "" ? "null" : structReportData.RR_QTR_L_45;
                                                        string rr_qtr_a_45 = structReportData.RR_QTR_a_45 == null || structReportData.RR_QTR_a_45 == "" ? "null" : structReportData.RR_QTR_a_45;
                                                        string rr_qtr_b_45 = structReportData.RR_QTR_b_45 == null || structReportData.RR_QTR_b_45 == "" ? "null" : structReportData.RR_QTR_b_45;
                                                        string rr_qtr_L_75 = structReportData.RR_QTR_L_75 == null || structReportData.RR_QTR_L_75 == "" ? "null" : structReportData.RR_QTR_L_75;
                                                        string rr_qtr_a_75 = structReportData.RR_QTR_a_75 == null || structReportData.RR_QTR_a_75 == "" ? "null" : structReportData.RR_QTR_a_75;
                                                        string rr_qtr_b_75 = structReportData.RR_QTR_b_75 == null || structReportData.RR_QTR_b_75 == "" ? "null" : structReportData.RR_QTR_b_75;
                                                        string rr_qtr_L_110 = structReportData.RR_QTR_L_110 == null || structReportData.RR_QTR_L_110 == "" ? "null" : structReportData.RR_QTR_L_110;
                                                        string rr_qtr_a_110 = structReportData.RR_QTR_a_110 == null || structReportData.RR_QTR_a_110 == "" ? "null" : structReportData.RR_QTR_a_110;
                                                        string rr_qtr_b_110 = structReportData.RR_QTR_b_110 == null || structReportData.RR_QTR_b_110 == "" ? "null" : structReportData.RR_QTR_b_110;
                                                        string rr_bumper_L_Minus15 = structReportData.RR_BUMPER_L_Minus15 == null || structReportData.RR_BUMPER_L_Minus15 == "" ? "null" : structReportData.RR_BUMPER_L_Minus15;
                                                        string rr_bumper_a_Minus15 = structReportData.RR_BUMPER_a_Minus15 == null || structReportData.RR_BUMPER_a_Minus15 == "" ? "null" : structReportData.RR_BUMPER_a_Minus15;
                                                        string rr_bumper_b_Minus15 = structReportData.RR_BUMPER_b_Minus15 == null || structReportData.RR_BUMPER_b_Minus15 == "" ? "null" : structReportData.RR_BUMPER_b_Minus15;
                                                        string rr_bumper_L_15 = structReportData.RR_BUMPER_L_15 == null || structReportData.RR_BUMPER_L_15 == "" ? "null" : structReportData.RR_BUMPER_L_15;
                                                        string rr_bumper_a_15 = structReportData.RR_BUMPER_a_15 == null || structReportData.RR_BUMPER_a_15 == "" ? "null" : structReportData.RR_BUMPER_a_15;
                                                        string rr_bumper_b_15 = structReportData.RR_BUMPER_b_15 == null || structReportData.RR_BUMPER_b_15 == "" ? "null" : structReportData.RR_BUMPER_b_15;
                                                        string rr_bumper_L_25 = structReportData.RR_BUMPER_L_25 == null || structReportData.RR_BUMPER_L_25 == "" ? "null" : structReportData.RR_BUMPER_L_25;
                                                        string rr_bumper_a_25 = structReportData.RR_BUMPER_a_25 == null || structReportData.RR_BUMPER_a_25 == "" ? "null" : structReportData.RR_BUMPER_a_25;
                                                        string rr_bumper_b_25 = structReportData.RR_BUMPER_b_25 == null || structReportData.RR_BUMPER_b_25 == "" ? "null" : structReportData.RR_BUMPER_b_25;
                                                        string rr_bumper_L_45 = structReportData.RR_BUMPER_L_45 == null || structReportData.RR_BUMPER_L_45 == "" ? "null" : structReportData.RR_BUMPER_L_45;
                                                        string rr_bumper_a_45 = structReportData.RR_BUMPER_a_45 == null || structReportData.RR_BUMPER_a_45 == "" ? "null" : structReportData.RR_BUMPER_a_45;
                                                        string rr_bumper_b_45 = structReportData.RR_BUMPER_b_45 == null || structReportData.RR_BUMPER_b_45 == "" ? "null" : structReportData.RR_BUMPER_b_45;
                                                        string rr_bumper_L_75 = structReportData.RR_BUMPER_L_75 == null || structReportData.RR_BUMPER_L_75 == "" ? "null" : structReportData.RR_BUMPER_L_75;
                                                        string rr_bumper_a_75 = structReportData.RR_BUMPER_a_75 == null || structReportData.RR_BUMPER_a_75 == "" ? "null" : structReportData.RR_BUMPER_a_75;
                                                        string rr_bumper_b_75 = structReportData.RR_BUMPER_b_75 == null || structReportData.RR_BUMPER_b_75 == "" ? "null" : structReportData.RR_BUMPER_b_75;
                                                        string rr_bumper_L_110 = structReportData.RR_BUMPER_L_110 == null || structReportData.RR_BUMPER_L_110 == "" ? "null" : structReportData.RR_BUMPER_L_110;
                                                        string rr_bumper_a_110 = structReportData.RR_BUMPER_a_110 == null || structReportData.RR_BUMPER_a_110 == "" ? "null" : structReportData.RR_BUMPER_a_110;
                                                        string rr_bumper_b_110 = structReportData.RR_BUMPER_b_110 == null || structReportData.RR_BUMPER_b_110 == "" ? "null" : structReportData.RR_BUMPER_b_110;
                                                        string fr_delta = structReportData.FR_DELTA == null || structReportData.FR_DELTA == "" ? "null" : structReportData.FR_DELTA;
                                                        string fr_result = structReportData.FR_Result == null || structReportData.FR_Result == "" ? "NG" : structReportData.FR_Result == "NG" ? "NG" : "OK";
                                                        string rr_delta = structReportData.RR_DELTA == null || structReportData.RR_DELTA == "" ? "null" : structReportData.RR_DELTA;
                                                        string rr_result = structReportData.RR_Result == null || structReportData.RR_Result == "" ? "NG" : structReportData.RR_Result == "NG" ? "NG" : "OK";

                                                        if (list.Count > 0)
                                                        {
                                                            Dictionary<string, string> _item = list[0];

                                                            bool isNull = false;

                                                            if (_item["fr_fenda_L_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_L15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_L25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_L45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_L75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_L110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_a110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_fenda_b110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_L110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_a110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["fr_bumper_b110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_L110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_a110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_qtr_b110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b_minus15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b15"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b25"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b45"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b75"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_L110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_a110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (_item["rr_bumper_b110"] == "")
                                                            {
                                                                isNull = true;
                                                            }

                                                            if (isNull)
                                                            {
                                                                bool isCheck = false;

                                                                qry = "update result_tbl set ";

                                                                if (_item["fr_fenda_L_minus15"] == "" && fr_fenda_L_Minus15 != "null")
                                                                {
                                                                    qry += "fr_fenda_L_minus15 = " + fr_fenda_L_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a_minus15"] == "" && fr_fenda_a_Minus15 != "null")
                                                                {
                                                                    qry += "fr_fenda_a_minus15 = " + fr_fenda_a_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b_minus15"] == "" && fr_fenda_b_Minus15 != "null")
                                                                {
                                                                    qry += "fr_fenda_b_minus15 = " + fr_fenda_b_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_L15"] == "" && fr_fenda_L_15 != "null")
                                                                {
                                                                    qry += "fr_fenda_L15 = " + fr_fenda_L_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a15"] == "" && fr_fenda_a_15 != "null")
                                                                {
                                                                    qry += "fr_fenda_a15 = " + fr_fenda_a_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b15"] == "" && fr_fenda_b_15 != "null")
                                                                {
                                                                    qry += "fr_fenda_b15 = " + fr_fenda_b_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_L25"] == "" && fr_fenda_L_25 != "null")
                                                                {
                                                                    qry += "fr_fenda_L25 = " + fr_fenda_L_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a25"] == "" && fr_fenda_a_25 != "null")
                                                                {
                                                                    qry += "fr_fenda_a25 = " + fr_fenda_a_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b25"] == "" && fr_fenda_b_25 != "null")
                                                                {
                                                                    qry += "fr_fenda_b25 = " + fr_fenda_b_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_L45"] == "" && fr_fenda_L_45 != "null")
                                                                {
                                                                    qry += "fr_fenda_L45 = " + fr_fenda_L_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a45"] == "" && fr_fenda_a_45 != "null")
                                                                {
                                                                    qry += "fr_fenda_a45 = " + fr_fenda_a_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b45"] == "" && fr_fenda_b_45 != "null")
                                                                {
                                                                    qry += "fr_fenda_b45 = " + fr_fenda_b_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_L75"] == "" && fr_fenda_L_75 != "null")
                                                                {
                                                                    qry += "fr_fenda_L75 = " + fr_fenda_L_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a75"] == "" && fr_fenda_a_75 != "null")
                                                                {
                                                                    qry += "fr_fenda_a75 = " + fr_fenda_a_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b75"] == "" && fr_fenda_b_75 != "null")
                                                                {
                                                                    qry += "fr_fenda_b75 = " + fr_fenda_b_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_L110"] == "" && fr_fenda_L_110 != "null")
                                                                {
                                                                    qry += "fr_fenda_L110 = " + fr_fenda_L_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_a110"] == "" && fr_fenda_a_110 != "null")
                                                                {
                                                                    qry += "fr_fenda_a110 = " + fr_fenda_a_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_fenda_b110"] == "" && fr_fenda_b_110 != "null")
                                                                {
                                                                    qry += "fr_fenda_b110 = " + fr_fenda_b_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L_minus15"] == "" && fr_bumper_L_Minus15 != "null")
                                                                {
                                                                    qry += "fr_bumper_L_minus15 = " + fr_bumper_L_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a_minus15"] == "" && fr_bumper_a_Minus15 != "null")
                                                                {
                                                                    qry += "fr_bumper_a_minus15 = " + fr_bumper_a_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b_minus15"] == "" && fr_bumper_b_Minus15 != "null")
                                                                {
                                                                    qry += "fr_bumper_b_minus15 = " + fr_bumper_b_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L15"] == "" && fr_bumper_L_15 != "null")
                                                                {
                                                                    qry += "fr_bumper_L15 = " + fr_bumper_L_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a15"] == "" && fr_bumper_a_15 != "null")
                                                                {
                                                                    qry += "fr_bumper_a15 = " + fr_bumper_a_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b15"] == "" && fr_bumper_b_15 != "null")
                                                                {
                                                                    qry += "fr_bumper_b15 = " + fr_bumper_b_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L25"] == "" && fr_bumper_L_25 != "null")
                                                                {
                                                                    qry += "fr_bumper_L25 = " + fr_bumper_L_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a25"] == "" && fr_bumper_a_25 != "null")
                                                                {
                                                                    qry += "fr_bumper_a25 = " + fr_bumper_a_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b25"] == "" && fr_bumper_b_25 != "null")
                                                                {
                                                                    qry += "fr_bumper_b25 = " + fr_bumper_b_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L45"] == "" && fr_bumper_L_45 != "null")
                                                                {
                                                                    qry += "fr_bumper_L45 = " + fr_bumper_L_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a45"] == "" && fr_bumper_a_45 != "null")
                                                                {
                                                                    qry += "fr_bumper_a45 = " + fr_bumper_a_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b45"] == "" && fr_bumper_b_45 != "null")
                                                                {
                                                                    qry += "fr_bumper_b45 = " + fr_bumper_b_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L75"] == "" && fr_bumper_L_75 != "null")
                                                                {
                                                                    qry += "fr_bumper_L75 = " + fr_bumper_L_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a75"] == "" && fr_bumper_a_75 != "null")
                                                                {
                                                                    qry += "fr_bumper_a75 = " + fr_bumper_a_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b75"] == "" && fr_bumper_b_75 != "null")
                                                                {
                                                                    qry += "fr_bumper_b75 = " + fr_bumper_b_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_L110"] == "" && fr_bumper_L_110 != "null")
                                                                {
                                                                    qry += "fr_bumper_L110 = " + fr_bumper_L_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_a110"] == "" && fr_bumper_a_110 != "null")
                                                                {
                                                                    qry += "fr_bumper_a110 = " + fr_bumper_a_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["fr_bumper_b110"] == "" && fr_bumper_b_110 != "null")
                                                                {
                                                                    qry += "fr_bumper_b110 = " + fr_bumper_b_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L_minus15"] == "" && rr_qtr_L_Minus15 != "null")
                                                                {
                                                                    qry += "rr_qtr_L_minus15 = " + rr_qtr_L_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a_minus15"] == "" && rr_qtr_a_Minus15 != "null")
                                                                {
                                                                    qry += "rr_qtr_a_minus15 = " + rr_qtr_a_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b_minus15"] == "" && rr_qtr_b_Minus15 != "null")
                                                                {
                                                                    qry += "rr_qtr_b_minus15 = " + rr_qtr_b_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L15"] == "" && rr_qtr_L_15 != "null")
                                                                {
                                                                    qry += "rr_qtr_L15 = " + rr_qtr_L_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a15"] == "" && rr_qtr_a_15 != "null")
                                                                {
                                                                    qry += "rr_qtr_a15 = " + rr_qtr_a_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b15"] == "" && rr_qtr_b_15 != "null")
                                                                {
                                                                    qry += "rr_qtr_b15 = " + rr_qtr_b_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L25"] == "" && rr_qtr_L_25 != "null")
                                                                {
                                                                    qry += "rr_qtr_L25 = " + rr_qtr_L_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a25"] == "" && rr_qtr_a_25 != "null")
                                                                {
                                                                    qry += "rr_qtr_a25 = " + rr_qtr_a_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b25"] == "" && rr_qtr_b_25 != "null")
                                                                {
                                                                    qry += "rr_qtr_b25 = " + rr_qtr_b_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L45"] == "" && rr_qtr_L_45 != "null")
                                                                {
                                                                    qry += "rr_qtr_L45 = " + rr_qtr_L_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a45"] == "" && rr_qtr_a_45 != "null")
                                                                {
                                                                    qry += "rr_qtr_a45 = " + rr_qtr_a_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b45"] == "" && rr_qtr_b_45 != "null")
                                                                {
                                                                    qry += "rr_qtr_b45 = " + rr_qtr_b_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L75"] == "" && rr_qtr_L_75 != "null")
                                                                {
                                                                    qry += "rr_qtr_L75 = " + rr_qtr_L_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a75"] == "" && rr_qtr_a_75 != "null")
                                                                {
                                                                    qry += "rr_qtr_a75 = " + rr_qtr_a_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b75"] == "" && rr_qtr_b_75 != "null")
                                                                {
                                                                    qry += "rr_qtr_b75 = " + rr_qtr_b_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_L110"] == "" && rr_qtr_L_110 != "null")
                                                                {
                                                                    qry += "rr_qtr_L110 = " + rr_qtr_L_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_a110"] == "" && rr_qtr_a_110 != "null")
                                                                {
                                                                    qry += "rr_qtr_a110 = " + rr_qtr_a_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_qtr_b110"] == "" && rr_qtr_b_110 != "null")
                                                                {
                                                                    qry += "rr_qtr_b110 = " + rr_qtr_b_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L_minus15"] == "" && rr_bumper_L_Minus15 != "null")
                                                                {
                                                                    qry += "rr_bumper_L_minus15 = " + rr_bumper_L_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a_minus15"] == "" && rr_bumper_a_Minus15 != "null")
                                                                {
                                                                    qry += "rr_bumper_a_minus15 = " + rr_bumper_a_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b_minus15"] == "" && rr_bumper_b_Minus15 != "null")
                                                                {
                                                                    qry += "rr_bumper_b_minus15 = " + rr_bumper_b_Minus15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L15"] == "" && rr_bumper_L_15 != "null")
                                                                {
                                                                    qry += "rr_bumper_L15 = " + rr_bumper_L_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a15"] == "" && rr_bumper_a_15 != "null")
                                                                {
                                                                    qry += "rr_bumper_a15 = " + rr_bumper_a_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b15"] == "" && rr_bumper_b_15 != "null")
                                                                {
                                                                    qry += "rr_bumper_b15 = " + rr_bumper_b_15 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L25"] == "" && rr_bumper_L_25 != "null")
                                                                {
                                                                    qry += "rr_bumper_L25 = " + rr_bumper_L_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a25"] == "" && rr_bumper_a_25 != "null")
                                                                {
                                                                    qry += "rr_bumper_a25 = " + rr_bumper_a_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b25"] == "" && rr_bumper_b_25 != "null")
                                                                {
                                                                    qry += "rr_bumper_b25 = " + rr_bumper_b_25 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L45"] == "" && rr_bumper_L_45 != "null")
                                                                {
                                                                    qry += "rr_bumper_L45 = " + rr_bumper_L_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a45"] == "" && rr_bumper_a_45 != "null")
                                                                {
                                                                    qry += "rr_bumper_a45 = " + rr_bumper_a_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b45"] == "" && rr_bumper_b_45 != "null")
                                                                {
                                                                    qry += "rr_bumper_b45 = " + rr_bumper_b_45 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L75"] == "" && rr_bumper_L_75 != "null")
                                                                {
                                                                    qry += "rr_bumper_L75 = " + rr_bumper_L_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a75"] == "" && rr_bumper_a_75 != "null")
                                                                {
                                                                    qry += "rr_bumper_a75 = " + rr_bumper_a_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b75"] == "" && rr_bumper_b_75 != "null")
                                                                {
                                                                    qry += "rr_bumper_b75 = " + rr_bumper_b_75 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_L110"] == "" && rr_bumper_L_110 != "null")
                                                                {
                                                                    qry += "rr_bumper_L110 = " + rr_bumper_L_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_a110"] == "" && rr_bumper_a_110 != "null")
                                                                {
                                                                    qry += "rr_bumper_a110 = " + rr_bumper_a_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (_item["rr_bumper_b110"] == "" && rr_bumper_b_110 != "null")
                                                                {
                                                                    qry += "rr_bumper_b110 = " + rr_bumper_b_110 + ",";
                                                                    isCheck = true;
                                                                }

                                                                if (isCheck)
                                                                {
                                                                    if (qry.EndsWith(","))
                                                                    {
                                                                        qry = qry.Substring(0, qry.Length - 1);
                                                                    }

                                                                    qry += " where model = '" + structReportData.Model + "' and bodyNumber = '" + structReportData.BodyNumber + "' and seq = '" + structReportData.Comment + "';";

                                                                    dbManager.ExecuteNonQuery(qry);

                                                                    try
                                                                    {
                                                                        window.Dispatcher.Invoke(() =>
                                                                        {
                                                                            DBStateColor = Brushes.LimeGreen;
                                                                        });
                                                                    }
                                                                    catch
                                                                    {

                                                                    }
                                                                }

                                                                qry = "select * from result_tbl where model = '" + structReportData.Model + "' and bodyNumber = '" + structReportData.BodyNumber + "' and seq = '" + structReportData.Comment + "';";
                                                                List<Dictionary<string, string>> result = dbManager.ExecuteReader(qry);

                                                                try
                                                                {
                                                                    window.Dispatcher.Invoke(() =>
                                                                    {
                                                                        DBStateColor = Brushes.LimeGreen;
                                                                    });
                                                                }
                                                                catch
                                                                {

                                                                }

                                                                if (result.Count > 0)
                                                                {
                                                                    Dictionary<string, string> tmp = result[0];

                                                                    bool isChecked = false;

                                                                    if (tmp["fr_fenda_L_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_L15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_L25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_L45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_L75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_L110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_a110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_fenda_b110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_L110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_a110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["fr_bumper_b110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_L110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_a110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_qtr_b110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b_minus15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b15"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b25"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b45"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b75"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_L110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_a110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (tmp["rr_bumper_b110"] == "")
                                                                    {
                                                                        isChecked = true;
                                                                    }

                                                                    if (!isChecked)
                                                                    {

                                                                        structReportData.FR_FENDA_L_Minus15 = tmp["fr_fenda_L_minus15"];
                                                                        structReportData.FR_FENDA_a_Minus15 = tmp["fr_fenda_a_minus15"];
                                                                        structReportData.FR_FENDA_b_Minus15 = tmp["fr_fenda_b_minus15"];
                                                                        structReportData.FR_FENDA_L_15 = tmp["fr_fenda_L15"];
                                                                        structReportData.FR_FENDA_a_15 = tmp["fr_fenda_a15"];
                                                                        structReportData.FR_FENDA_b_15 = tmp["fr_fenda_b15"];
                                                                        structReportData.FR_FENDA_L_25 = tmp["fr_fenda_L25"];
                                                                        structReportData.FR_FENDA_a_25 = tmp["fr_fenda_a25"];
                                                                        structReportData.FR_FENDA_b_25 = tmp["fr_fenda_b25"];
                                                                        structReportData.FR_FENDA_L_45 = tmp["fr_fenda_L45"];
                                                                        structReportData.FR_FENDA_a_45 = tmp["fr_fenda_a45"];
                                                                        structReportData.FR_FENDA_b_45 = tmp["fr_fenda_b45"];
                                                                        structReportData.FR_FENDA_L_75 = tmp["fr_fenda_L75"];
                                                                        structReportData.FR_FENDA_a_75 = tmp["fr_fenda_a75"];
                                                                        structReportData.FR_FENDA_b_75 = tmp["fr_fenda_b75"];
                                                                        structReportData.FR_FENDA_L_110 = tmp["fr_fenda_L110"];
                                                                        structReportData.FR_FENDA_a_110 = tmp["fr_fenda_a110"];
                                                                        structReportData.FR_FENDA_b_110 = tmp["fr_fenda_b110"];

                                                                        structReportData.FR_BUMPER_L_Minus15 = tmp["fr_bumper_L_minus15"];
                                                                        structReportData.FR_BUMPER_a_Minus15 = tmp["fr_bumper_a_minus15"];
                                                                        structReportData.FR_BUMPER_b_Minus15 = tmp["fr_bumper_b_minus15"];
                                                                        structReportData.FR_BUMPER_L_15 = tmp["fr_bumper_L15"];
                                                                        structReportData.FR_BUMPER_a_15 = tmp["fr_bumper_a15"];
                                                                        structReportData.FR_BUMPER_b_15 = tmp["fr_bumper_b15"];
                                                                        structReportData.FR_BUMPER_L_25 = tmp["fr_bumper_L25"];
                                                                        structReportData.FR_BUMPER_a_25 = tmp["fr_bumper_a25"];
                                                                        structReportData.FR_BUMPER_b_25 = tmp["fr_bumper_b25"];
                                                                        structReportData.FR_BUMPER_L_45 = tmp["fr_bumper_L45"];
                                                                        structReportData.FR_BUMPER_a_45 = tmp["fr_bumper_a45"];
                                                                        structReportData.FR_BUMPER_b_45 = tmp["fr_bumper_b45"];
                                                                        structReportData.FR_BUMPER_L_75 = tmp["fr_bumper_L75"];
                                                                        structReportData.FR_BUMPER_a_75 = tmp["fr_bumper_a75"];
                                                                        structReportData.FR_BUMPER_b_75 = tmp["fr_bumper_b75"];
                                                                        structReportData.FR_BUMPER_L_110 = tmp["fr_bumper_L110"];
                                                                        structReportData.FR_BUMPER_a_110 = tmp["fr_bumper_a110"];
                                                                        structReportData.FR_BUMPER_b_110 = tmp["fr_bumper_b110"];

                                                                        structReportData.RR_QTR_L_Minus15 = tmp["rr_qtr_L_minus15"];
                                                                        structReportData.RR_QTR_a_Minus15 = tmp["rr_qtr_a_minus15"];
                                                                        structReportData.RR_QTR_b_Minus15 = tmp["rr_qtr_b_minus15"];
                                                                        structReportData.RR_QTR_L_15 = tmp["rr_qtr_L15"];
                                                                        structReportData.RR_QTR_a_15 = tmp["rr_qtr_a15"];
                                                                        structReportData.RR_QTR_b_15 = tmp["rr_qtr_b15"];
                                                                        structReportData.RR_QTR_L_25 = tmp["rr_qtr_L25"];
                                                                        structReportData.RR_QTR_a_25 = tmp["rr_qtr_a25"];
                                                                        structReportData.RR_QTR_b_25 = tmp["rr_qtr_b25"];
                                                                        structReportData.RR_QTR_L_45 = tmp["rr_qtr_L45"];
                                                                        structReportData.RR_QTR_a_45 = tmp["rr_qtr_a45"];
                                                                        structReportData.RR_QTR_b_45 = tmp["rr_qtr_b45"];
                                                                        structReportData.RR_QTR_L_75 = tmp["rr_qtr_L75"];
                                                                        structReportData.RR_QTR_a_75 = tmp["rr_qtr_a75"];
                                                                        structReportData.RR_QTR_b_75 = tmp["rr_qtr_b75"];
                                                                        structReportData.RR_QTR_L_110 = tmp["rr_qtr_L110"];
                                                                        structReportData.RR_QTR_a_110 = tmp["rr_qtr_a110"];
                                                                        structReportData.RR_QTR_b_110 = tmp["rr_qtr_b110"];

                                                                        structReportData.RR_BUMPER_L_Minus15 = tmp["rr_bumper_L_minus15"];
                                                                        structReportData.RR_BUMPER_a_Minus15 = tmp["rr_bumper_a_minus15"];
                                                                        structReportData.RR_BUMPER_b_Minus15 = tmp["rr_bumper_b_minus15"];
                                                                        structReportData.RR_BUMPER_L_15 = tmp["rr_bumper_L15"];
                                                                        structReportData.RR_BUMPER_a_15 = tmp["rr_bumper_a15"];
                                                                        structReportData.RR_BUMPER_b_15 = tmp["rr_bumper_b15"];
                                                                        structReportData.RR_BUMPER_L_25 = tmp["rr_bumper_L25"];
                                                                        structReportData.RR_BUMPER_a_25 = tmp["rr_bumper_a25"];
                                                                        structReportData.RR_BUMPER_b_25 = tmp["rr_bumper_b25"];
                                                                        structReportData.RR_BUMPER_L_45 = tmp["rr_bumper_L45"];
                                                                        structReportData.RR_BUMPER_a_45 = tmp["rr_bumper_a45"];
                                                                        structReportData.RR_BUMPER_b_45 = tmp["rr_bumper_b45"];
                                                                        structReportData.RR_BUMPER_L_75 = tmp["rr_bumper_L75"];
                                                                        structReportData.RR_BUMPER_a_75 = tmp["rr_bumper_a75"];
                                                                        structReportData.RR_BUMPER_b_75 = tmp["rr_bumper_b75"];
                                                                        structReportData.RR_BUMPER_L_110 = tmp["rr_bumper_L110"];
                                                                        structReportData.RR_BUMPER_a_110 = tmp["rr_bumper_a110"];
                                                                        structReportData.RR_BUMPER_b_110 = tmp["rr_bumper_b110"];

                                                                        Total_Result(structReportData);

                                                                        qry = "update result_tbl set fr_delta_value = " + structReportData.FR_DELTA + ", fr_result = '" + structReportData.FR_Result + "', rr_delta_value = " + structReportData.RR_DELTA + ", rr_result = '" + structReportData.RR_Result + "' where model = '" + structReportData.Model + "' and bodyNumber = '" + structReportData.BodyNumber + "' and seq = '" + structReportData.Comment + "';";
                                                                        dbManager.ExecuteNonQuery(qry);

                                                                        try
                                                                        {
                                                                            window.Dispatcher.Invoke(() =>
                                                                            {
                                                                                DBStateColor = Brushes.LimeGreen;
                                                                            });
                                                                        }
                                                                        catch
                                                                        {

                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            qry = "insert into result_tbl values(null,'" + structReportData.StartDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "','" + structReportData.Model + "','" + structReportData.Comment + "','" + structReportData.Color.Trim() + "','" + structReportData.BodyNumber + "'," + fr_fenda_L_Minus15 + "," + fr_fenda_a_Minus15 + "," + fr_fenda_b_Minus15 + "," + fr_fenda_L_15 + "," + fr_fenda_a_15 + "," + fr_fenda_b_15 + "," + fr_fenda_L_25 + "," + fr_fenda_a_25 + "," + fr_fenda_b_25 + "," + fr_fenda_L_45 + "," + fr_fenda_a_45 + "," + fr_fenda_b_45 + "," + fr_fenda_L_75 + "," + fr_fenda_a_75 + "," + fr_fenda_b_75 + "," + fr_fenda_L_110 + "," + fr_fenda_a_110 + "," + fr_fenda_b_110 + "," + fr_bumper_L_Minus15 + "," + fr_bumper_a_Minus15 + "," + fr_bumper_b_Minus15 + "," + fr_bumper_L_15 + "," + fr_bumper_a_15 + "," + fr_bumper_b_15 + "," + fr_bumper_L_25 + "," + fr_bumper_a_25 + "," + fr_bumper_b_25 + "," + fr_bumper_L_45 + "," + fr_bumper_a_45 + "," + fr_bumper_b_45 + "," + fr_bumper_L_75 + "," + fr_bumper_a_75 + "," + fr_bumper_b_75 + "," + fr_bumper_L_110 + "," + fr_bumper_a_110 + "," + fr_bumper_b_110 + "," + rr_qtr_L_Minus15 + "," + rr_qtr_a_Minus15 + "," + rr_qtr_b_Minus15 + "," + rr_qtr_L_15 + "," + rr_qtr_a_15 + "," + rr_qtr_b_15 + "," + rr_qtr_L_25 + "," + rr_qtr_a_25 + "," + rr_qtr_b_25 + "," + rr_qtr_L_45 + "," + rr_qtr_a_45 + "," + rr_qtr_b_45 + "," + rr_qtr_L_75 + "," + rr_qtr_a_75 + "," + rr_qtr_b_75 + "," + rr_qtr_L_110 + "," + rr_qtr_a_110 + "," + rr_qtr_b_110 + "," + rr_bumper_L_Minus15 + "," + rr_bumper_a_Minus15 + "," + rr_bumper_b_Minus15 + "," + rr_bumper_L_15 + "," + rr_bumper_a_15 + "," + rr_bumper_b_15 + "," + rr_bumper_L_25 + "," + rr_bumper_a_25 + "," + rr_bumper_b_25 + "," + rr_bumper_L_45 + "," + rr_bumper_a_45 + "," + rr_bumper_b_45 + "," + rr_bumper_L_75 + "," + rr_bumper_a_75 + "," + rr_bumper_b_75 + "," + rr_bumper_L_110 + "," + rr_bumper_a_110 + "," + rr_bumper_b_110 + "," + fr_delta + ",'" + fr_result + "'," + rr_delta + ",'" + rr_result + "',false,null);";
                                                            dbManager.ExecuteNonQuery(qry);

                                                            try
                                                            {
                                                                window.Dispatcher.Invoke(() =>
                                                                {
                                                                    DBStateColor = Brushes.LimeGreen;
                                                                });
                                                            }
                                                            catch
                                                            {

                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    logManager.Error(e.Message);
                                                    
                                                    try
                                                    {
                                                        window.Dispatcher.Invoke(() =>
                                                        {
                                                            DBStateColor = Brushes.Red;
                                                        });
                                                    }
                                                    catch
                                                    {

                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // File.Delete(filepath);

                            Thread.Sleep(1);
                        }
                    }
                }
                catch (Exception e)
                {
                    logManager.Error("DB 쓰레드 에러 : " + e.Message);
                }

                try
                {
                    int threadDelay = m_Config.GetInt32("DB", "Thread Delay", 1000);
                    Thread.Sleep(threadDelay);
                }
                catch
                {

                }
            }
        }

        private void InitCarKindList()
        {
            string temp = selectedCarKind;

            CarKindList.Clear();

            carKindList.Add("ALL");

            string[] lines = File.ReadAllLines(carKindFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line != null)
                {
                    CarKindList.Add(line);
                }
            }

            SelectedCarKind = temp;
        }

        private void InitColorList()
        {
            string temp = selectedColor;

            ColorList.Clear();

            ColorList.Add("ALL");

            string[] lines = File.ReadAllLines(passRangeFilePath);
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] split = line.Split(',');

                if (split.Length > 1)
                {
                    ColorList.Add(split[0]);
                }
            }

            SelectedColor = temp;
        }

        private bool isRunning = true;
        private bool plcState = false;
        
        private void InitPLC()
        {
            new Thread(new ThreadStart(() =>
            {
                while (isRunning)
                {
                    try
                    {
                        if (!plcState)
                        {
                            logManager.Trace("Init Plc");

                            if (plcManager != null)
                            {
                                plcManager.Close();
                            }

                            plcManager = new HMelsecManager(iniManager.LogicalStationNumber);
                            logManager.Trace("Try to Open Plc");

                            if (plcManager.Open())
                            {
                                logManager.Trace("Start Plc Tick Thread");

                                StartPlcThread();
                                plcState = true;

                                window.Dispatcher.Invoke(() =>
                                {
                                    PLCStateColor = Brushes.LimeGreen;
                                });
                            }
                            else
                            {
                                logManager.Fatal("Failed To Open Plc");
                                plcState = false;

                                window.Dispatcher.Invoke(() =>
                                {
                                    PLCStateColor = Brushes.Red;
                                });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logManager.Error("PLC 접속 실패 : " + e.Message);
                        plcState = false;

                        try
                        {
                            window.Dispatcher.Invoke(() =>
                            {
                                PLCStateColor = Brushes.Red;
                            });
                        }
                        catch
                        {

                        }
                    }

                    try
                    {
                        Thread.Sleep(300);
                    }
                    catch
                    {

                    }
                }
            })).Start();

            StartPlcHeartBeatThread();
        }

        private void StartPlcThread()
        {
            plcThread = new Thread(new ThreadStart(PlcThreadDo));
            plcThread.Start();
        }

        private void StartPlcHeartBeatThread()
        {
            plcHeartBeatThread = new Thread(new ThreadStart(StartPlcHeartBeatThreadDo));
            plcHeartBeatThread.Start();
        }

        private void StartPlcHeartBeatThreadDo()
        {
            while (isRunning)
            {
                try
                {
                    if (plcState)
                    {
                        int tickInterval = iniManager.ReadTickInterval;
                        string readAddr = iniManager.ReadAddr;
                        int readSize = iniManager.ReadSize;
                        string addr_Heartbeat = iniManager.AddrHeartBeat;
                        int pos_Heartbeat = Convert.ToInt32(addr_Heartbeat.Replace("D", ""));
                        int startPos = Convert.ToInt32(readAddr.Replace("D", ""));
                        pos_Heartbeat -= startPos;
                        int[] readData = new int[readSize];

                        if (plcManager.ReadBlock(readAddr, readSize, out readData) == false)
                        {
                            plcState = false;

                            window.Dispatcher.Invoke(() =>
                            {
                                PLCStateColor = Brushes.Red;
                            });

                            continue;
                        }

                        int input_Heartbeat = readData[pos_Heartbeat];

                        if (input_Heartbeat == 0)
                        {
                            window.Dispatcher.Invoke(() =>
                            {
                                PLCHeartBeatColor = Brushes.DarkGray;
                            });

                            plcManager.WriteBlock(addr_Heartbeat, 1, new int[] { 1 });
                        }
                        else
                        {
                            window.Dispatcher.Invoke(() =>
                            {
                                PLCHeartBeatColor = Brushes.LimeGreen;
                            });
                        }

                        Thread.Sleep(tickInterval);
                    }
                }
                catch
                {

                }
            }
        }

        private string old_BodyNumber = "";
        private bool isChangedBodyNumber = false;

        private void PlcThreadDo()
        {
            int tickInterval = iniManager.ReadTickInterval;
            string readAddr = iniManager.ReadAddr;
            int readSize = iniManager.ReadSize;

            // PLC Address
            string addr_Heartbeat = iniManager.AddrHeartBeat;
            string addr_Complete = iniManager.AddrComplete;
            string addr_FR_Result = iniManager.AddrFR_Result;
            string addr_RR_Result = iniManager.AddrRR_Result;
            string addr_ByPass = iniManager.AddrByPass;
            string addr_BodyNumber = iniManager.AddrBodyNumber;

            int pos_Heartbeat = Convert.ToInt32(addr_Heartbeat.Replace("D", ""));
            int pos_Complete = Convert.ToInt32(addr_Complete.Replace("D", ""));
            int pos_FR_Result = Convert.ToInt32(addr_FR_Result.Replace("D", ""));
            int pos_RR_Result = Convert.ToInt32(addr_RR_Result.Replace("D", ""));
            int pos_ByPass = Convert.ToInt32(addr_Complete.Replace("D", ""));
            int pos_BodyNumber = Convert.ToInt32(addr_BodyNumber.Replace("D", ""));

            int startPos = Convert.ToInt32(readAddr.Replace("D", ""));
            pos_Heartbeat -= startPos;
            pos_Complete -= startPos;
            pos_FR_Result -= startPos;
            pos_RR_Result -= startPos;
            pos_ByPass -= startPos;
            pos_BodyNumber -= startPos;

            int[] readData = new int[readSize];
            
            while (isRunning)
            {
                try
                {
                    if (plcManager.ReadBlock(readAddr, readSize, out readData) == false)
                    {
                        plcState = false;

                        window.Dispatcher.Invoke(() =>
                        {
                            PLCStateColor = Brushes.Red;
                        });

                        continue;
                    }

                    /*
                    int[] bodyNumberArr = new int[iniManager.ReadSizeBodyNumber];
                    Array.Copy(readData, pos_BodyNumber, bodyNumberArr, 0, iniManager.ReadSizeBodyNumber);
                    string input_BodyNumber = string.Join("", ConvertIntToString(bodyNumberArr));
                    */

                    // input_BodyNumber = "KFKFKFKF";

                    string dir = m_Config.GetString("Directory", "Result", "E:\\Result");
                    string[] files = Directory.GetFiles(dir);
                    string file = files[files.Length - 1];
                    string bodyNumber = file.Split(' ')[file.Split(' ').Length - 1];
                    bodyNumber = bodyNumber.Substring(2, bodyNumber.Length - 2);

                    int[] arr = ConvertIntToIntArr(bodyNumber);

                    string[] matchs = files.Where(x => x.Contains(bodyNumber)).ToArray();

                    if (matchs.Length > 1)
                    {
                        if (bodyNumber != "" && old_BodyNumber != bodyNumber)
                        {
                            old_BodyNumber = bodyNumber;
                            isChangedBodyNumber = true;

                            for (int i = 0; i < arr.Length; i++)
                            {
                                string tmp = iniManager.AddrBodyNumber.Replace("D", "");
                                int pos = Convert.ToInt32(tmp) + i;
                                string addr = "D" + pos;

                                plcManager.WriteBlock(addr, 1, new int[] { arr[i] });
                            }

                            
                        }

                        while (isChangedBodyNumber)
                        {
                            bool isChecked = false;
                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            while (!isChecked && sw.ElapsedMilliseconds < iniManager.WriteResultLimit)
                            {
                                Result result = GetResult(bodyNumber);

                                if (result == Result.ALL_OK)
                                {
                                    plcManager.WriteBlock(iniManager.AddrByPass, 1, new int[] { 0 });
                                    plcManager.WriteBlock(iniManager.AddrFR_Result, 1, new int[] { 1 });
                                    plcManager.WriteBlock(iniManager.AddrRR_Result, 1, new int[] { 1 });
                                }
                                else if (result == Result.FR_OK_RR_NG)
                                {
                                    plcManager.WriteBlock(iniManager.AddrByPass, 1, new int[] { 0 });
                                    plcManager.WriteBlock(iniManager.AddrFR_Result, 1, new int[] { 1 });
                                    plcManager.WriteBlock(iniManager.AddrRR_Result, 1, new int[] { 2 });
                                }
                                else if (result == Result.FR_NG_RR_OK)
                                {
                                    plcManager.WriteBlock(iniManager.AddrByPass, 1, new int[] { 0 });
                                    plcManager.WriteBlock(iniManager.AddrFR_Result, 1, new int[] { 2 });
                                    plcManager.WriteBlock(iniManager.AddrRR_Result, 1, new int[] { 1 });
                                }
                                else if (result == Result.ALL_NG)
                                {
                                    plcManager.WriteBlock(iniManager.AddrByPass, 1, new int[] { 0 });
                                    plcManager.WriteBlock(iniManager.AddrFR_Result, 1, new int[] { 2 });
                                    plcManager.WriteBlock(iniManager.AddrRR_Result, 1, new int[] { 2 });
                                }
                                else
                                {
                                    plcManager.WriteBlock(iniManager.AddrByPass, 1, new int[] { 1 });
                                }

                                plcManager.WriteBlock(iniManager.AddrComplete, 1, new int[] { 1 });

                                logManager.Trace("PLC Respon Wait.");

                                if (result != Result.NONE)
                                {
                                    int input_Complete = 0;

                                    while (input_Complete != 11 && sw.ElapsedMilliseconds < iniManager.WriteResultLimit)
                                    {
                                        logManager.Trace("PLC 응답값 = 11 대기");

                                        if (plcManager.ReadBlock(readAddr, readSize, out readData) == false)
                                        {
                                            plcState = false;

                                            window.Dispatcher.Invoke(() =>
                                            {
                                                PLCStateColor = Brushes.Red;
                                            });

                                            continue;
                                        }

                                        input_Complete = readData[pos_Complete];

                                        if (input_Complete == 11)
                                        {
                                            plcManager.WriteBlock(iniManager.AddrComplete, 1, new int[] { 2 });

                                            break;
                                        }
                                    }

                                    while (!isChecked && sw.ElapsedMilliseconds < iniManager.WriteResultLimit)
                                    {
                                        logManager.Trace("PLC 응답값 = 22 대기");

                                        if (plcManager.ReadBlock(readAddr, readSize, out readData) == false)
                                        {
                                            plcState = false;

                                            window.Dispatcher.Invoke(() =>
                                            {
                                                PLCStateColor = Brushes.Red;
                                            });

                                            continue;
                                        }

                                        input_Complete = readData[pos_Complete];

                                        if (input_Complete == 22)
                                        {
                                            isChecked = true;
                                        }
                                    }

                                    /*
                                    if (result == Result.ALL_OK || result == Result.FR_OK_RR_NG)
                                    {
                                        if (input_FR_Result == 11)
                                        {
                                            isChecked = true;
                                        }
                                    }
                                    else if (result == Result.ALL_NG || result == Result.FR_NG_RR_OK)
                                    {
                                        if (input_FR_Result == 22)
                                        {
                                            isChecked = true;
                                        }
                                    }
                                    else if (result == Result.ALL_PASS || result == Result.FR_PASS_RR_OK || result == Result.FR_PASS_RR_NG)
                                    {
                                        if (input_FR_Result == 33)
                                        {
                                            isChecked = true;
                                        }
                                    }
                                    */
                                }

                                Thread.Sleep(100);
                            }

                            isChangedBodyNumber = false;
                        }
                    }

                    plcState = true;

                    window.Dispatcher.Invoke(() =>
                    {
                        PLCStateColor = Brushes.LimeGreen;
                    });

                    Thread.Sleep(tickInterval);
                }
                catch (Exception e)
                {
                    logManager.Fatal("Plc Fatal Error : " + e.Message);
                    plcState = false;

                    try
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            PLCStateColor = Brushes.Red;
                        });
                    }
                    catch
                    {

                    }
                }
            }

            plcState = false;

            /*
            window.Dispatcher.Invoke(() =>
            {
                PLCStateColor = Brushes.Red;
            });
            */
        }

        private string[] ConvertIntToString(int[] Input)
        {
            //0~7은 데이터가 없고 8~15엔 데이터가 있는경우는 상정안됨. 하지만 될것 같음.

            List<string> ConvString = new List<string>();

            foreach (int data in Input)
            {
                string bianryData = Convert.ToString(data, 2).PadLeft(16, '0');

                string Bit_firstValue = bianryData.Substring(8);
                string Bit_lastValue = bianryData.Substring(0, 8);

                int DEC_firstValue = Convert.ToInt32(Bit_firstValue, 2);
                int DEC_lastValue = Convert.ToInt32(Bit_lastValue, 2);

                string ASCII_firstValue = "";
                if (DEC_firstValue != 0)
                {
                    ASCII_firstValue = Encoding.ASCII.GetString(new byte[] { (byte)DEC_firstValue });
                }

                string ASCII_lastValue = "";
                if (DEC_lastValue != 0)
                {
                    ASCII_lastValue = Encoding.ASCII.GetString(new byte[] { (byte)DEC_lastValue });
                }

                ConvString.Add(ASCII_firstValue + ASCII_lastValue);
            }

            return ConvString.ToArray();
        }

        private int[] ConvertIntToIntArr(string bodyNumber)
        {
            List<int> list = new List<int>();


            char[] b = bodyNumber.ToArray();

            int[] arr = new int[bodyNumber.Length / 2];

            for (int i = 0; i < b.Length; i++)
            {
                if ((i + 1) % 2 == 0)
                {
                    arr[i / 2] += b[i] * 256;
                }
                else
                {
                    arr[i / 2] += b[i];
                }
            }

            return arr;
        }

        private List<StructSensorData> GetSensorData(string bodyNumber)
        {
            string resultPath = m_Config.GetString("Directory", "Result", "E:\\Result");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // string[] files = Directory.GetFiles(resultPath).Where(x => x == new DateTime(2022, 10, 27).ToString("yyyyMMdd")).ToArray();
            string[] files = Directory.GetFiles(resultPath).Where(x => x.Contains(bodyNumber)).ToArray();

            sw.Stop();
            logManager.Trace("파일 불러오기 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

            List<StructSensorData> sensorDataList = new List<StructSensorData>();

            for (int i = 0; i < files.Length; i++)
            {
                if (sensorDataList.Count > 3)
                {
                    break;
                }

                string file = files[i];

                string model = "";
                string color = "";
                string _bodyNumber = "";

                // 해당 차량인지 확인
                if (file.EndsWith(bodyNumber))
                {
                    string allText = File.ReadAllText(file);

                    string[] headerSplit = allText.Split(new string[] { "</header>" }, StringSplitOptions.None);

                    if (headerSplit.Length > 0)
                    {
                        XmlDocument xmlFile = new XmlDocument();

                        try
                        {
                            headerSplit[0] += "</header>";
                            xmlFile.LoadXml(headerSplit[0]);

                            XmlNodeList xmlList = xmlFile.GetElementsByTagName("header");

                            for (int j = 0; j < xmlList.Count; j++)
                            {
                                XmlNode item = xmlList[j];
                                model = item["model"].InnerText;
                                color = item["color"].InnerText;
                                _bodyNumber = item["bodyNumber"].InnerText;
                            }
                        }
                        catch (Exception ex)
                        {
                            logManager.Error(ex.Message);
                        }

                        if (headerSplit.Length > 1)
                        {
                            string[] measurementSplit = headerSplit[1].Split(new string[] { "</measurement>" }, StringSplitOptions.None);

                            if (measurementSplit.Length > 0)
                            {
                                for (int j = 0; j < measurementSplit.Length; j++)
                                {
                                    if (measurementSplit[j].Contains("<measurement>"))
                                    {
                                        try
                                        {
                                            measurementSplit[j] += "</measurement>";
                                            xmlFile.LoadXml(measurementSplit[j]);

                                            string measurementText = xmlFile["measurement"].InnerXml;

                                            xmlFile.LoadXml(measurementText);

                                            XmlNodeList xmlList = xmlFile.GetElementsByTagName("checkzone");

                                            for (int k = 0; k < xmlList.Count; k++)
                                            {
                                                XmlNode item = xmlList[k];

                                                string index = item["index"].InnerText;
                                                string dE_Minus15 = item["dE-15"].InnerText;
                                                string dE_15 = item["dE15"].InnerText;
                                                string dE_25 = item["dE25"].InnerText;
                                                string dE_45 = item["dE45"].InnerText;
                                                string dE_75 = item["dE75"].InnerText;
                                                string dE_110 = item["dE110"].InnerText;
                                                string dL_Minus15 = item["dL-15"].InnerText;
                                                string dL_15 = item["dL15"].InnerText;
                                                string dL_25 = item["dL25"].InnerText;
                                                string dL_45 = item["dL45"].InnerText;
                                                string dL_75 = item["dL75"].InnerText;
                                                string dL_110 = item["dL110"].InnerText;
                                                string da_Minus15 = item["da-15"].InnerText;
                                                string da_15 = item["da15"].InnerText;
                                                string da_25 = item["da25"].InnerText;
                                                string da_45 = item["da45"].InnerText;
                                                string da_75 = item["da75"].InnerText;
                                                string da_110 = item["da110"].InnerText;
                                                string db_Minus15 = item["db-15"].InnerText;
                                                string db_15 = item["db15"].InnerText;
                                                string db_25 = item["db25"].InnerText;
                                                string db_45 = item["db45"].InnerText;
                                                string db_75 = item["db75"].InnerText;
                                                string db_110 = item["db110"].InnerText;
                                                string L_Minus15 = item["L-15"].InnerText;
                                                string a_Minus15 = item["a-15"].InnerText;
                                                string b_Minus15 = item["a-15"].InnerText;
                                                string L_15 = item["L15"].InnerText;
                                                string a_15 = item["a15"].InnerText;
                                                string b_15 = item["a15"].InnerText;
                                                string L_25 = item["L25"].InnerText;
                                                string a_25 = item["a25"].InnerText;
                                                string b_25 = item["a25"].InnerText;
                                                string L_45 = item["L45"].InnerText;
                                                string a_45 = item["a45"].InnerText;
                                                string b_45 = item["b45"].InnerText;
                                                string L_75 = item["L75"].InnerText;
                                                string a_75 = item["a75"].InnerText;
                                                string b_75 = item["b75"].InnerText;
                                                string L_110 = item["L110"].InnerText;
                                                string a_110 = item["a110"].InnerText;
                                                string b_110 = item["b110"].InnerText;

                                                StructMeasurement structMeasurement = new StructMeasurement();
                                                structMeasurement.CheckZone = index;
                                                structMeasurement.dE_Minus15 = dE_Minus15;
                                                structMeasurement.dE_15 = dE_15;
                                                structMeasurement.dE_25 = dE_25;
                                                structMeasurement.dE_45 = dE_45;
                                                structMeasurement.dE_75 = dE_75;
                                                structMeasurement.dE_110 = dE_110;
                                                structMeasurement.dL_Minus15 = dL_Minus15;
                                                structMeasurement.dL_15 = dL_15;
                                                structMeasurement.dL_25 = dL_25;
                                                structMeasurement.dL_45 = dL_45;
                                                structMeasurement.dL_75 = dL_75;
                                                structMeasurement.dL_110 = dL_110;
                                                structMeasurement.da_Minus15 = da_Minus15;
                                                structMeasurement.da_15 = da_15;
                                                structMeasurement.da_25 = da_25;
                                                structMeasurement.da_45 = da_45;
                                                structMeasurement.da_75 = da_75;
                                                structMeasurement.da_110 = da_110;
                                                structMeasurement.db_Minus15 = db_Minus15;
                                                structMeasurement.db_15 = db_15;
                                                structMeasurement.db_25 = db_25;
                                                structMeasurement.db_45 = db_45;
                                                structMeasurement.db_75 = db_75;
                                                structMeasurement.db_110 = db_110;
                                                structMeasurement.L_Minus15 = L_Minus15;
                                                structMeasurement.a_Minus15 = a_Minus15;
                                                structMeasurement.b_Minus15 = b_Minus15;
                                                structMeasurement.L_15 = L_15;
                                                structMeasurement.a_15 = a_15;
                                                structMeasurement.b_15 = b_15;
                                                structMeasurement.L_25 = L_25;
                                                structMeasurement.a_25 = a_25;
                                                structMeasurement.b_25 = b_25;
                                                structMeasurement.L_45 = L_45;
                                                structMeasurement.a_45 = a_45;
                                                structMeasurement.b_45 = b_45;
                                                structMeasurement.L_75 = L_75;
                                                structMeasurement.a_75 = a_75;
                                                structMeasurement.b_75 = b_75;
                                                structMeasurement.L_110 = L_110;
                                                structMeasurement.a_110 = a_110;
                                                structMeasurement.b_110 = b_110;

                                                StructSensorData sensorData = new StructSensorData();

                                                if (sensorData.Model == null || sensorData.Model.Trim() == "")
                                                {
                                                    sensorData.Model = model;
                                                }

                                                if (sensorData.Color == null || sensorData.Color.Trim() == "")
                                                {
                                                    sensorData.Color = color;
                                                }

                                                if (sensorData.BodyNumber == null || sensorData.BodyNumber.Trim() == "")
                                                {
                                                    sensorData.BodyNumber = _bodyNumber;
                                                }

                                                sensorData.Measurement = structMeasurement;

                                                sensorDataList.Add(sensorData);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logManager.Error(ex.Message);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return sensorDataList;
        }

        private enum Result { ALL_OK, ALL_NG, ALL_PASS, FR_OK_RR_NG, FR_OK_RR_PASS, FR_NG_RR_OK, FR_NG_RR_PASS, FR_PASS_RR_OK, FR_PASS_RR_NG, NONE }

        private Result GetResult(string bodyNumber)
        {
            try
            {
                if (bodyNumber != null && bodyNumber != "")
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    Stopwatch _sw = new Stopwatch();
                    _sw.Start();
                    List<StructSensorData> sensorDataList = GetSensorData(bodyNumber);
                    _sw.Stop();
                    logManager.Trace("GetSensorData 소요 시간 : " + _sw.ElapsedMilliseconds + "ms");
                    StructSensorData front_Fenda = sensorDataList.Find(x => x.Measurement.CheckZone == m_Config.GetString("CheckZone", "FR_FENDA", "1"));
                    StructSensorData front_Bumper = sensorDataList.Find(x => x.Measurement.CheckZone == m_Config.GetString("CheckZone", "FR_BUMPER", "2"));
                    StructSensorData rear_Qtr = sensorDataList.Find(x => x.Measurement.CheckZone == m_Config.GetString("CheckZone", "RR_QTR", "3"));
                    StructSensorData rear_Bumper = sensorDataList.Find(x => x.Measurement.CheckZone == m_Config.GetString("CheckZone", "RR_BUMPER", "4"));

                    // double front_delta = CalcDelta(front_Fenda, front_Bumper);
                    // double rear_delta = CalcDelta(rear_Qtr, rear_Bumper);

                    StructPassRange structPassRange = new StructPassRange();
                    List<StructPassRange> passRangeList = LoadPassRangeData();

                    string color = "";

                    if (sensorDataList.Count > 0)
                    {
                        color = sensorDataList[0].Color;
                    }

                    StructPassRange passRange = passRangeList.Find(x => x.Color.Trim() == color.Trim());

                    if (passRange == null)
                    {
                        string content = File.ReadAllText(passRangeFilePath);
                        content += color.Trim() + "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0," + Environment.NewLine;
                        File.WriteAllText(passRangeFilePath, content);
                        passRangeList = LoadPassRangeData();
                        passRange = passRangeList.Find(x => x.Color.Trim() == color.Trim());
                    }

                    bool fr_result = false;
                    bool rr_result = false;


                    if (FrontSymmetric)
                    {
                        if (front_Fenda != null && front_Bumper != null && (front_Fenda.Measurement.dE_Minus15 != null && front_Fenda.Measurement.dE_15 != null && front_Fenda.Measurement.dE_25 != null && front_Fenda.Measurement.dE_45 != null && front_Fenda.Measurement.dE_75 != null && front_Fenda.Measurement.dE_110 != null && front_Fenda.Measurement.dL_Minus15 != null && front_Fenda.Measurement.dL_15 != null && front_Fenda.Measurement.dL_25 != null && front_Fenda.Measurement.dL_45 != null && front_Fenda.Measurement.dL_75 != null && front_Fenda.Measurement.dL_110 != null && front_Fenda.Measurement.da_Minus15 != null && front_Fenda.Measurement.da_15 != null && front_Fenda.Measurement.da_25 != null && front_Fenda.Measurement.da_45 != null && front_Fenda.Measurement.da_75 != null && front_Fenda.Measurement.da_110 != null && front_Fenda.Measurement.db_Minus15 != null && front_Fenda.Measurement.db_15 != null && front_Fenda.Measurement.db_25 != null && front_Fenda.Measurement.db_45 != null && front_Fenda.Measurement.db_75 != null && front_Fenda.Measurement.db_110 != null && front_Fenda.Measurement.L_Minus15 != null && front_Fenda.Measurement.L_15 != null && front_Fenda.Measurement.L_25 != null && front_Fenda.Measurement.L_45 != null && front_Fenda.Measurement.L_75 != null && front_Fenda.Measurement.L_110 != null && front_Fenda.Measurement.a_Minus15 != null && front_Fenda.Measurement.a_15 != null && front_Fenda.Measurement.a_25 != null && front_Fenda.Measurement.a_45 != null && front_Fenda.Measurement.a_75 != null && front_Fenda.Measurement.a_110 != null && front_Fenda.Measurement.b_Minus15 != null && front_Fenda.Measurement.b_15 != null && front_Fenda.Measurement.b_25 != null && front_Fenda.Measurement.b_45 != null && front_Fenda.Measurement.b_75 != null && front_Fenda.Measurement.b_110 != null && front_Bumper.Measurement.dE_Minus15 != null && front_Bumper.Measurement.dE_15 != null && front_Bumper.Measurement.dE_25 != null && front_Bumper.Measurement.dE_45 != null && front_Bumper.Measurement.dE_75 != null && front_Bumper.Measurement.dE_110 != null && front_Bumper.Measurement.dL_Minus15 != null && front_Bumper.Measurement.dL_15 != null && front_Bumper.Measurement.dL_25 != null && front_Bumper.Measurement.dL_45 != null && front_Bumper.Measurement.dL_75 != null && front_Bumper.Measurement.dL_110 != null && front_Bumper.Measurement.da_Minus15 != null && front_Bumper.Measurement.da_15 != null && front_Bumper.Measurement.da_25 != null && front_Bumper.Measurement.da_45 != null && front_Bumper.Measurement.da_75 != null && front_Bumper.Measurement.da_110 != null && front_Bumper.Measurement.db_Minus15 != null && front_Bumper.Measurement.db_15 != null && front_Bumper.Measurement.db_25 != null && front_Bumper.Measurement.db_45 != null && front_Bumper.Measurement.db_75 != null && front_Bumper.Measurement.db_110 != null && front_Bumper.Measurement.L_Minus15 != null && front_Bumper.Measurement.L_15 != null && front_Bumper.Measurement.L_25 != null && front_Bumper.Measurement.L_45 != null && front_Bumper.Measurement.L_75 != null && front_Bumper.Measurement.L_110 != null && front_Bumper.Measurement.a_Minus15 != null && front_Bumper.Measurement.a_15 != null && front_Bumper.Measurement.a_25 != null && front_Bumper.Measurement.a_45 != null && front_Bumper.Measurement.a_75 != null && front_Bumper.Measurement.a_110 != null && front_Bumper.Measurement.b_Minus15 != null && front_Bumper.Measurement.b_15 != null && front_Bumper.Measurement.b_25 != null && front_Bumper.Measurement.b_45 != null && front_Bumper.Measurement.b_75 != null && front_Bumper.Measurement.b_110 != null && front_Fenda.Measurement.dE_Minus15 != "" && front_Fenda.Measurement.dE_15 != "" && front_Fenda.Measurement.dE_25 != "" && front_Fenda.Measurement.dE_45 != "" && front_Fenda.Measurement.dE_75 != "" && front_Fenda.Measurement.dE_110 != "" && front_Fenda.Measurement.dL_Minus15 != "" && front_Fenda.Measurement.dL_15 != "" && front_Fenda.Measurement.dL_25 != "" && front_Fenda.Measurement.dL_45 != "" && front_Fenda.Measurement.dL_75 != "" && front_Fenda.Measurement.dL_110 != "" && front_Fenda.Measurement.da_Minus15 != "" && front_Fenda.Measurement.da_15 != "" && front_Fenda.Measurement.da_25 != "" && front_Fenda.Measurement.da_45 != "" && front_Fenda.Measurement.da_75 != "" && front_Fenda.Measurement.da_110 != "" && front_Fenda.Measurement.db_Minus15 != "" && front_Fenda.Measurement.db_15 != "" && front_Fenda.Measurement.db_25 != "" && front_Fenda.Measurement.db_45 != "" && front_Fenda.Measurement.db_75 != "" && front_Fenda.Measurement.db_110 != "" && front_Fenda.Measurement.L_Minus15 != "" && front_Fenda.Measurement.L_15 != "" && front_Fenda.Measurement.L_25 != "" && front_Fenda.Measurement.L_45 != "" && front_Fenda.Measurement.L_75 != "" && front_Fenda.Measurement.L_110 != "" && front_Fenda.Measurement.a_Minus15 != "" && front_Fenda.Measurement.a_15 != "" && front_Fenda.Measurement.a_25 != "" && front_Fenda.Measurement.a_45 != "" && front_Fenda.Measurement.a_75 != "" && front_Fenda.Measurement.a_110 != "" && front_Fenda.Measurement.b_Minus15 != "" && front_Fenda.Measurement.b_15 != "" && front_Fenda.Measurement.b_25 != "" && front_Fenda.Measurement.b_45 != "" && front_Fenda.Measurement.b_75 != "" && front_Fenda.Measurement.b_110 != "" && front_Bumper.Measurement.dE_Minus15 != "" && front_Bumper.Measurement.dE_15 != "" && front_Bumper.Measurement.dE_25 != "" && front_Bumper.Measurement.dE_45 != "" && front_Bumper.Measurement.dE_75 != "" && front_Bumper.Measurement.dE_110 != "" && front_Bumper.Measurement.dL_Minus15 != "" && front_Bumper.Measurement.dL_15 != "" && front_Bumper.Measurement.dL_25 != "" && front_Bumper.Measurement.dL_45 != "" && front_Bumper.Measurement.dL_75 != "" && front_Bumper.Measurement.dL_110 != "" && front_Bumper.Measurement.da_Minus15 != "" && front_Bumper.Measurement.da_15 != "" && front_Bumper.Measurement.da_25 != "" && front_Bumper.Measurement.da_45 != "" && front_Bumper.Measurement.da_75 != "" && front_Bumper.Measurement.da_110 != "" && front_Bumper.Measurement.db_Minus15 != "" && front_Bumper.Measurement.db_15 != "" && front_Bumper.Measurement.db_25 != "" && front_Bumper.Measurement.db_45 != "" && front_Bumper.Measurement.db_75 != "" && front_Bumper.Measurement.db_110 != "" && front_Bumper.Measurement.L_Minus15 != "" && front_Bumper.Measurement.L_15 != "" && front_Bumper.Measurement.L_25 != "" && front_Bumper.Measurement.L_45 != "" && front_Bumper.Measurement.L_75 != "" && front_Bumper.Measurement.L_110 != "" && front_Bumper.Measurement.a_Minus15 != "" && front_Bumper.Measurement.a_15 != "" && front_Bumper.Measurement.a_25 != "" && front_Bumper.Measurement.a_45 != "" && front_Bumper.Measurement.a_75 != "" && front_Bumper.Measurement.a_110 != "" && front_Bumper.Measurement.b_Minus15 != "" && front_Bumper.Measurement.b_15 != "" && front_Bumper.Measurement.b_25 != "" && front_Bumper.Measurement.b_45 != "" && front_Bumper.Measurement.b_75 != "" && front_Bumper.Measurement.b_110 != ""))
                        {
                            double dE_Minus15 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_Minus15), Convert.ToDouble(front_Fenda.Measurement.a_Minus15), Convert.ToDouble(front_Fenda.Measurement.L_Minus15), Convert.ToDouble(front_Bumper.Measurement.L_Minus15), Convert.ToDouble(front_Bumper.Measurement.a_Minus15), Convert.ToDouble(front_Bumper.Measurement.L_Minus15));
                            double dE_15 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_15), Convert.ToDouble(front_Fenda.Measurement.a_15), Convert.ToDouble(front_Fenda.Measurement.L_15), Convert.ToDouble(front_Bumper.Measurement.L_15), Convert.ToDouble(front_Bumper.Measurement.a_15), Convert.ToDouble(front_Bumper.Measurement.L_15));
                            double dE_25 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_25), Convert.ToDouble(front_Fenda.Measurement.a_25), Convert.ToDouble(front_Fenda.Measurement.L_25), Convert.ToDouble(front_Bumper.Measurement.L_25), Convert.ToDouble(front_Bumper.Measurement.a_25), Convert.ToDouble(front_Bumper.Measurement.L_25));
                            double dE_45 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_45), Convert.ToDouble(front_Fenda.Measurement.a_45), Convert.ToDouble(front_Fenda.Measurement.L_45), Convert.ToDouble(front_Bumper.Measurement.L_45), Convert.ToDouble(front_Bumper.Measurement.a_45), Convert.ToDouble(front_Bumper.Measurement.L_45));
                            double dE_75 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_75), Convert.ToDouble(front_Fenda.Measurement.a_75), Convert.ToDouble(front_Fenda.Measurement.L_75), Convert.ToDouble(front_Bumper.Measurement.L_75), Convert.ToDouble(front_Bumper.Measurement.a_75), Convert.ToDouble(front_Bumper.Measurement.L_75));
                            double dE_110 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_110), Convert.ToDouble(front_Fenda.Measurement.a_110), Convert.ToDouble(front_Fenda.Measurement.L_110), Convert.ToDouble(front_Bumper.Measurement.L_110), Convert.ToDouble(front_Bumper.Measurement.a_110), Convert.ToDouble(front_Bumper.Measurement.L_110));

                            if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangeValue &&
                                dE_15 > (passRange.Front_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangeValue &&
                                dE_25 > (passRange.Front_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangeValue &&
                                dE_45 > (passRange.Front_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangeValue &&
                                dE_75 > (passRange.Front_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangeValue &&
                                dE_110 > (passRange.Front_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_Minus15) - Convert.ToDouble(front_Bumper.Measurement.L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_Minus15) - Convert.ToDouble(front_Bumper.Measurement.L_Minus15)) < passRange.Front_dL_Minus15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_Minus15) - Convert.ToDouble(front_Bumper.Measurement.a_Minus15)) > (passRange.Front_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_Minus15) - Convert.ToDouble(front_Bumper.Measurement.a_Minus15)) < passRange.Front_da_Minus15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_Minus15) - Convert.ToDouble(front_Bumper.Measurement.b_Minus15)) > (passRange.Front_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_Minus15) - Convert.ToDouble(front_Bumper.Measurement.b_Minus15)) < passRange.Front_db_Minus15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_15) - Convert.ToDouble(front_Bumper.Measurement.L_15)) > (passRange.Front_dL_15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_15) - Convert.ToDouble(front_Bumper.Measurement.L_15)) < passRange.Front_dL_15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_15) - Convert.ToDouble(front_Bumper.Measurement.a_15)) > (passRange.Front_da_15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_15) - Convert.ToDouble(front_Bumper.Measurement.a_15)) < passRange.Front_da_15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_15) - Convert.ToDouble(front_Bumper.Measurement.b_15)) > (passRange.Front_db_15_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_15) - Convert.ToDouble(front_Bumper.Measurement.b_15)) < passRange.Front_db_15_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_25) - Convert.ToDouble(front_Bumper.Measurement.L_25)) > (passRange.Front_dL_25_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_25) - Convert.ToDouble(front_Bumper.Measurement.L_25)) < passRange.Front_dL_25_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_25) - Convert.ToDouble(front_Bumper.Measurement.a_25)) > (passRange.Front_da_25_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_25) - Convert.ToDouble(front_Bumper.Measurement.a_25)) < passRange.Front_da_25_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_25) - Convert.ToDouble(front_Bumper.Measurement.b_25)) > (passRange.Front_db_25_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_25) - Convert.ToDouble(front_Bumper.Measurement.b_25)) < passRange.Front_db_25_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_45) - Convert.ToDouble(front_Bumper.Measurement.L_45)) > (passRange.Front_dL_45_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_45) - Convert.ToDouble(front_Bumper.Measurement.L_45)) < passRange.Front_dL_45_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_45) - Convert.ToDouble(front_Bumper.Measurement.a_45)) > (passRange.Front_da_45_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_45) - Convert.ToDouble(front_Bumper.Measurement.a_45)) < passRange.Front_da_45_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_45) - Convert.ToDouble(front_Bumper.Measurement.b_45)) > (passRange.Front_db_45_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_45) - Convert.ToDouble(front_Bumper.Measurement.b_45)) < passRange.Front_db_45_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_75) - Convert.ToDouble(front_Bumper.Measurement.L_75)) > (passRange.Front_dL_75_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_75) - Convert.ToDouble(front_Bumper.Measurement.L_75)) < passRange.Front_dL_75_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_75) - Convert.ToDouble(front_Bumper.Measurement.a_75)) > (passRange.Front_da_75_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_75) - Convert.ToDouble(front_Bumper.Measurement.a_75)) < passRange.Front_da_75_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_75) - Convert.ToDouble(front_Bumper.Measurement.b_75)) > (passRange.Front_db_75_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_75) - Convert.ToDouble(front_Bumper.Measurement.b_75)) < passRange.Front_db_75_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_110) - Convert.ToDouble(front_Bumper.Measurement.L_110)) > (passRange.Front_dL_110_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_110) - Convert.ToDouble(front_Bumper.Measurement.L_110)) < passRange.Front_dL_110_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_110) - Convert.ToDouble(front_Bumper.Measurement.a_110)) > (passRange.Front_da_110_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_110) - Convert.ToDouble(front_Bumper.Measurement.a_110)) < passRange.Front_da_110_PassRangeValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_110) - Convert.ToDouble(front_Bumper.Measurement.b_110)) > (passRange.Front_db_110_PassRangeValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_110) - Convert.ToDouble(front_Bumper.Measurement.b_110)) < passRange.Front_db_110_PassRangeValue)
                            {
                                fr_result = true;
                            }
                            else
                            {
                                fr_result = false;
                            }
                        }
                        else
                        {
                            if (iniManager.BlankIsOk)
                            {
                                fr_result = true;
                            }
                            else
                            {
                                fr_result = false;
                            }
                        }
                    }
                    else
                    {
                        if (front_Fenda != null && front_Bumper != null && (front_Fenda.Measurement.dE_Minus15 != null && front_Fenda.Measurement.dE_15 != null && front_Fenda.Measurement.dE_25 != null && front_Fenda.Measurement.dE_45 != null && front_Fenda.Measurement.dE_75 != null && front_Fenda.Measurement.dE_110 != null && front_Fenda.Measurement.dL_Minus15 != null && front_Fenda.Measurement.dL_15 != null && front_Fenda.Measurement.dL_25 != null && front_Fenda.Measurement.dL_45 != null && front_Fenda.Measurement.dL_75 != null && front_Fenda.Measurement.dL_110 != null && front_Fenda.Measurement.da_Minus15 != null && front_Fenda.Measurement.da_15 != null && front_Fenda.Measurement.da_25 != null && front_Fenda.Measurement.da_45 != null && front_Fenda.Measurement.da_75 != null && front_Fenda.Measurement.da_110 != null && front_Fenda.Measurement.db_Minus15 != null && front_Fenda.Measurement.db_15 != null && front_Fenda.Measurement.db_25 != null && front_Fenda.Measurement.db_45 != null && front_Fenda.Measurement.db_75 != null && front_Fenda.Measurement.db_110 != null && front_Fenda.Measurement.L_Minus15 != null && front_Fenda.Measurement.L_15 != null && front_Fenda.Measurement.L_25 != null && front_Fenda.Measurement.L_45 != null && front_Fenda.Measurement.L_75 != null && front_Fenda.Measurement.L_110 != null && front_Fenda.Measurement.a_Minus15 != null && front_Fenda.Measurement.a_15 != null && front_Fenda.Measurement.a_25 != null && front_Fenda.Measurement.a_45 != null && front_Fenda.Measurement.a_75 != null && front_Fenda.Measurement.a_110 != null && front_Fenda.Measurement.b_Minus15 != null && front_Fenda.Measurement.b_15 != null && front_Fenda.Measurement.b_25 != null && front_Fenda.Measurement.b_45 != null && front_Fenda.Measurement.b_75 != null && front_Fenda.Measurement.b_110 != null && front_Bumper.Measurement.dE_Minus15 != null && front_Bumper.Measurement.dE_15 != null && front_Bumper.Measurement.dE_25 != null && front_Bumper.Measurement.dE_45 != null && front_Bumper.Measurement.dE_75 != null && front_Bumper.Measurement.dE_110 != null && front_Bumper.Measurement.dL_Minus15 != null && front_Bumper.Measurement.dL_15 != null && front_Bumper.Measurement.dL_25 != null && front_Bumper.Measurement.dL_45 != null && front_Bumper.Measurement.dL_75 != null && front_Bumper.Measurement.dL_110 != null && front_Bumper.Measurement.da_Minus15 != null && front_Bumper.Measurement.da_15 != null && front_Bumper.Measurement.da_25 != null && front_Bumper.Measurement.da_45 != null && front_Bumper.Measurement.da_75 != null && front_Bumper.Measurement.da_110 != null && front_Bumper.Measurement.db_Minus15 != null && front_Bumper.Measurement.db_15 != null && front_Bumper.Measurement.db_25 != null && front_Bumper.Measurement.db_45 != null && front_Bumper.Measurement.db_75 != null && front_Bumper.Measurement.db_110 != null && front_Bumper.Measurement.L_Minus15 != null && front_Bumper.Measurement.L_15 != null && front_Bumper.Measurement.L_25 != null && front_Bumper.Measurement.L_45 != null && front_Bumper.Measurement.L_75 != null && front_Bumper.Measurement.L_110 != null && front_Bumper.Measurement.a_Minus15 != null && front_Bumper.Measurement.a_15 != null && front_Bumper.Measurement.a_25 != null && front_Bumper.Measurement.a_45 != null && front_Bumper.Measurement.a_75 != null && front_Bumper.Measurement.a_110 != null && front_Bumper.Measurement.b_Minus15 != null && front_Bumper.Measurement.b_15 != null && front_Bumper.Measurement.b_25 != null && front_Bumper.Measurement.b_45 != null && front_Bumper.Measurement.b_75 != null && front_Bumper.Measurement.b_110 != null && front_Fenda.Measurement.dE_Minus15 != "" && front_Fenda.Measurement.dE_15 != "" && front_Fenda.Measurement.dE_25 != "" && front_Fenda.Measurement.dE_45 != "" && front_Fenda.Measurement.dE_75 != "" && front_Fenda.Measurement.dE_110 != "" && front_Fenda.Measurement.dL_Minus15 != "" && front_Fenda.Measurement.dL_15 != "" && front_Fenda.Measurement.dL_25 != "" && front_Fenda.Measurement.dL_45 != "" && front_Fenda.Measurement.dL_75 != "" && front_Fenda.Measurement.dL_110 != "" && front_Fenda.Measurement.da_Minus15 != "" && front_Fenda.Measurement.da_15 != "" && front_Fenda.Measurement.da_25 != "" && front_Fenda.Measurement.da_45 != "" && front_Fenda.Measurement.da_75 != "" && front_Fenda.Measurement.da_110 != "" && front_Fenda.Measurement.db_Minus15 != "" && front_Fenda.Measurement.db_15 != "" && front_Fenda.Measurement.db_25 != "" && front_Fenda.Measurement.db_45 != "" && front_Fenda.Measurement.db_75 != "" && front_Fenda.Measurement.db_110 != "" && front_Fenda.Measurement.L_Minus15 != "" && front_Fenda.Measurement.L_15 != "" && front_Fenda.Measurement.L_25 != "" && front_Fenda.Measurement.L_45 != "" && front_Fenda.Measurement.L_75 != "" && front_Fenda.Measurement.L_110 != "" && front_Fenda.Measurement.a_Minus15 != "" && front_Fenda.Measurement.a_15 != "" && front_Fenda.Measurement.a_25 != "" && front_Fenda.Measurement.a_45 != "" && front_Fenda.Measurement.a_75 != "" && front_Fenda.Measurement.a_110 != "" && front_Fenda.Measurement.b_Minus15 != "" && front_Fenda.Measurement.b_15 != "" && front_Fenda.Measurement.b_25 != "" && front_Fenda.Measurement.b_45 != "" && front_Fenda.Measurement.b_75 != "" && front_Fenda.Measurement.b_110 != "" && front_Bumper.Measurement.dE_Minus15 != "" && front_Bumper.Measurement.dE_15 != "" && front_Bumper.Measurement.dE_25 != "" && front_Bumper.Measurement.dE_45 != "" && front_Bumper.Measurement.dE_75 != "" && front_Bumper.Measurement.dE_110 != "" && front_Bumper.Measurement.dL_Minus15 != "" && front_Bumper.Measurement.dL_15 != "" && front_Bumper.Measurement.dL_25 != "" && front_Bumper.Measurement.dL_45 != "" && front_Bumper.Measurement.dL_75 != "" && front_Bumper.Measurement.dL_110 != "" && front_Bumper.Measurement.da_Minus15 != "" && front_Bumper.Measurement.da_15 != "" && front_Bumper.Measurement.da_25 != "" && front_Bumper.Measurement.da_45 != "" && front_Bumper.Measurement.da_75 != "" && front_Bumper.Measurement.da_110 != "" && front_Bumper.Measurement.db_Minus15 != "" && front_Bumper.Measurement.db_15 != "" && front_Bumper.Measurement.db_25 != "" && front_Bumper.Measurement.db_45 != "" && front_Bumper.Measurement.db_75 != "" && front_Bumper.Measurement.db_110 != "" && front_Bumper.Measurement.L_Minus15 != "" && front_Bumper.Measurement.L_15 != "" && front_Bumper.Measurement.L_25 != "" && front_Bumper.Measurement.L_45 != "" && front_Bumper.Measurement.L_75 != "" && front_Bumper.Measurement.L_110 != "" && front_Bumper.Measurement.a_Minus15 != "" && front_Bumper.Measurement.a_15 != "" && front_Bumper.Measurement.a_25 != "" && front_Bumper.Measurement.a_45 != "" && front_Bumper.Measurement.a_75 != "" && front_Bumper.Measurement.a_110 != "" && front_Bumper.Measurement.b_Minus15 != "" && front_Bumper.Measurement.b_15 != "" && front_Bumper.Measurement.b_25 != "" && front_Bumper.Measurement.b_45 != "" && front_Bumper.Measurement.b_75 != "" && front_Bumper.Measurement.b_110 != ""))
                        {
                            double dE_Minus15 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_Minus15), Convert.ToDouble(front_Fenda.Measurement.a_Minus15), Convert.ToDouble(front_Fenda.Measurement.L_Minus15), Convert.ToDouble(front_Bumper.Measurement.L_Minus15), Convert.ToDouble(front_Bumper.Measurement.a_Minus15), Convert.ToDouble(front_Bumper.Measurement.L_Minus15));
                            double dE_15 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_15), Convert.ToDouble(front_Fenda.Measurement.a_15), Convert.ToDouble(front_Fenda.Measurement.L_15), Convert.ToDouble(front_Bumper.Measurement.L_15), Convert.ToDouble(front_Bumper.Measurement.a_15), Convert.ToDouble(front_Bumper.Measurement.L_15));
                            double dE_25 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_25), Convert.ToDouble(front_Fenda.Measurement.a_25), Convert.ToDouble(front_Fenda.Measurement.L_25), Convert.ToDouble(front_Bumper.Measurement.L_25), Convert.ToDouble(front_Bumper.Measurement.a_25), Convert.ToDouble(front_Bumper.Measurement.L_25));
                            double dE_45 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_45), Convert.ToDouble(front_Fenda.Measurement.a_45), Convert.ToDouble(front_Fenda.Measurement.L_45), Convert.ToDouble(front_Bumper.Measurement.L_45), Convert.ToDouble(front_Bumper.Measurement.a_45), Convert.ToDouble(front_Bumper.Measurement.L_45));
                            double dE_75 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_75), Convert.ToDouble(front_Fenda.Measurement.a_75), Convert.ToDouble(front_Fenda.Measurement.L_75), Convert.ToDouble(front_Bumper.Measurement.L_75), Convert.ToDouble(front_Bumper.Measurement.a_75), Convert.ToDouble(front_Bumper.Measurement.L_75));
                            double dE_110 = CalcDelta(Convert.ToDouble(front_Fenda.Measurement.L_110), Convert.ToDouble(front_Fenda.Measurement.a_110), Convert.ToDouble(front_Fenda.Measurement.L_110), Convert.ToDouble(front_Bumper.Measurement.L_110), Convert.ToDouble(front_Bumper.Measurement.a_110), Convert.ToDouble(front_Bumper.Measurement.L_110));

                            if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangePlusValue &&
                                dE_15 > (passRange.Front_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangePlusValue &&
                                dE_25 > (passRange.Front_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangePlusValue &&
                                dE_45 > (passRange.Front_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangePlusValue &&
                                dE_75 > (passRange.Front_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangePlusValue &&
                                dE_110 > (passRange.Front_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_Minus15) - Convert.ToDouble(front_Bumper.Measurement.L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_Minus15) - Convert.ToDouble(front_Bumper.Measurement.L_Minus15)) < passRange.Front_dL_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_Minus15) - Convert.ToDouble(front_Bumper.Measurement.a_Minus15)) > (passRange.Front_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_Minus15) - Convert.ToDouble(front_Bumper.Measurement.a_Minus15)) < passRange.Front_da_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_Minus15) - Convert.ToDouble(front_Bumper.Measurement.b_Minus15)) > (passRange.Front_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_Minus15) - Convert.ToDouble(front_Bumper.Measurement.b_Minus15)) < passRange.Front_db_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_15) - Convert.ToDouble(front_Bumper.Measurement.L_15)) > (passRange.Front_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_15) - Convert.ToDouble(front_Bumper.Measurement.L_15)) < passRange.Front_dL_15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_15) - Convert.ToDouble(front_Bumper.Measurement.a_15)) > (passRange.Front_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_15) - Convert.ToDouble(front_Bumper.Measurement.a_15)) < passRange.Front_da_15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_15) - Convert.ToDouble(front_Bumper.Measurement.b_15)) > (passRange.Front_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_15) - Convert.ToDouble(front_Bumper.Measurement.b_15)) < passRange.Front_db_15_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_25) - Convert.ToDouble(front_Bumper.Measurement.L_25)) > (passRange.Front_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_25) - Convert.ToDouble(front_Bumper.Measurement.L_25)) < passRange.Front_dL_25_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_25) - Convert.ToDouble(front_Bumper.Measurement.a_25)) > (passRange.Front_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_25) - Convert.ToDouble(front_Bumper.Measurement.a_25)) < passRange.Front_da_25_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_25) - Convert.ToDouble(front_Bumper.Measurement.b_25)) > (passRange.Front_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_25) - Convert.ToDouble(front_Bumper.Measurement.b_25)) < passRange.Front_db_25_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_45) - Convert.ToDouble(front_Bumper.Measurement.L_45)) > (passRange.Front_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_45) - Convert.ToDouble(front_Bumper.Measurement.L_45)) < passRange.Front_dL_45_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_45) - Convert.ToDouble(front_Bumper.Measurement.a_45)) > (passRange.Front_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_45) - Convert.ToDouble(front_Bumper.Measurement.a_45)) < passRange.Front_da_45_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_45) - Convert.ToDouble(front_Bumper.Measurement.b_45)) > (passRange.Front_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_45) - Convert.ToDouble(front_Bumper.Measurement.b_45)) < passRange.Front_db_45_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_75) - Convert.ToDouble(front_Bumper.Measurement.L_75)) > (passRange.Front_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_75) - Convert.ToDouble(front_Bumper.Measurement.L_75)) < passRange.Front_dL_75_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_75) - Convert.ToDouble(front_Bumper.Measurement.a_75)) > (passRange.Front_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_75) - Convert.ToDouble(front_Bumper.Measurement.a_75)) < passRange.Front_da_75_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_75) - Convert.ToDouble(front_Bumper.Measurement.b_75)) > (passRange.Front_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_75) - Convert.ToDouble(front_Bumper.Measurement.b_75)) < passRange.Front_db_75_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.L_110) - Convert.ToDouble(front_Bumper.Measurement.L_110)) > (passRange.Front_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.L_110) - Convert.ToDouble(front_Bumper.Measurement.L_110)) < passRange.Front_dL_110_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.a_110) - Convert.ToDouble(front_Bumper.Measurement.a_110)) > (passRange.Front_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.a_110) - Convert.ToDouble(front_Bumper.Measurement.a_110)) < passRange.Front_da_110_PassRangePlusValue &&
                                (Convert.ToDouble(front_Fenda.Measurement.b_110) - Convert.ToDouble(front_Bumper.Measurement.b_110)) > (passRange.Front_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(front_Fenda.Measurement.b_110) - Convert.ToDouble(front_Bumper.Measurement.b_110)) < passRange.Front_db_110_PassRangePlusValue)
                            {
                                fr_result = true;
                            }
                            else
                            {
                                fr_result = false;
                            }
                        }
                        else
                        {
                            if (iniManager.BlankIsOk)
                            {
                                fr_result = true;
                            }
                            else
                            {
                                fr_result = false;
                            }
                        }
                    }

                    if (RearSymmetric)
                    {
                        if (rear_Qtr != null && rear_Bumper != null && (rear_Qtr.Measurement.dE_Minus15 != null && rear_Qtr.Measurement.dE_15 != null && rear_Qtr.Measurement.dE_25 != null && rear_Qtr.Measurement.dE_45 != null && rear_Qtr.Measurement.dE_75 != null && rear_Qtr.Measurement.dE_110 != null && rear_Qtr.Measurement.dL_Minus15 != null && rear_Qtr.Measurement.dL_15 != null && rear_Qtr.Measurement.dL_25 != null && rear_Qtr.Measurement.dL_45 != null && rear_Qtr.Measurement.dL_75 != null && rear_Qtr.Measurement.dL_110 != null && rear_Qtr.Measurement.da_Minus15 != null && rear_Qtr.Measurement.da_15 != null && rear_Qtr.Measurement.da_25 != null && rear_Qtr.Measurement.da_45 != null && rear_Qtr.Measurement.da_75 != null && rear_Qtr.Measurement.da_110 != null && rear_Qtr.Measurement.db_Minus15 != null && rear_Qtr.Measurement.db_15 != null && rear_Qtr.Measurement.db_25 != null && rear_Qtr.Measurement.db_45 != null && rear_Qtr.Measurement.db_75 != null && rear_Qtr.Measurement.db_110 != null && rear_Qtr.Measurement.L_Minus15 != null && rear_Qtr.Measurement.L_15 != null && rear_Qtr.Measurement.L_25 != null && rear_Qtr.Measurement.L_45 != null && rear_Qtr.Measurement.L_75 != null && rear_Qtr.Measurement.L_110 != null && rear_Qtr.Measurement.a_Minus15 != null && rear_Qtr.Measurement.a_15 != null && rear_Qtr.Measurement.a_25 != null && rear_Qtr.Measurement.a_45 != null && rear_Qtr.Measurement.a_75 != null && rear_Qtr.Measurement.a_110 != null && rear_Qtr.Measurement.b_Minus15 != null && rear_Qtr.Measurement.b_15 != null && rear_Qtr.Measurement.b_25 != null && rear_Qtr.Measurement.b_45 != null && rear_Qtr.Measurement.b_75 != null && rear_Qtr.Measurement.b_110 != null && rear_Bumper.Measurement.dE_Minus15 != null && rear_Bumper.Measurement.dE_15 != null && rear_Bumper.Measurement.dE_25 != null && rear_Bumper.Measurement.dE_45 != null && rear_Bumper.Measurement.dE_75 != null && rear_Bumper.Measurement.dE_110 != null && rear_Bumper.Measurement.dL_Minus15 != null && rear_Bumper.Measurement.dL_15 != null && rear_Bumper.Measurement.dL_25 != null && rear_Bumper.Measurement.dL_45 != null && rear_Bumper.Measurement.dL_75 != null && rear_Bumper.Measurement.dL_110 != null && rear_Bumper.Measurement.da_Minus15 != null && rear_Bumper.Measurement.da_15 != null && rear_Bumper.Measurement.da_25 != null && rear_Bumper.Measurement.da_45 != null && rear_Bumper.Measurement.da_75 != null && rear_Bumper.Measurement.da_110 != null && rear_Bumper.Measurement.db_Minus15 != null && rear_Bumper.Measurement.db_15 != null && rear_Bumper.Measurement.db_25 != null && rear_Bumper.Measurement.db_45 != null && rear_Bumper.Measurement.db_75 != null && rear_Bumper.Measurement.db_110 != null && rear_Bumper.Measurement.L_Minus15 != null && rear_Bumper.Measurement.L_15 != null && rear_Bumper.Measurement.L_25 != null && rear_Bumper.Measurement.L_45 != null && rear_Bumper.Measurement.L_75 != null && rear_Bumper.Measurement.L_110 != null && rear_Bumper.Measurement.a_Minus15 != null && rear_Bumper.Measurement.a_15 != null && rear_Bumper.Measurement.a_25 != null && rear_Bumper.Measurement.a_45 != null && rear_Bumper.Measurement.a_75 != null && rear_Bumper.Measurement.a_110 != null && rear_Bumper.Measurement.b_Minus15 != null && rear_Bumper.Measurement.b_15 != null && rear_Bumper.Measurement.b_25 != null && rear_Bumper.Measurement.b_45 != null && rear_Bumper.Measurement.b_75 != null && rear_Bumper.Measurement.b_110 != null && rear_Qtr.Measurement.dE_Minus15 != "" && rear_Qtr.Measurement.dE_15 != "" && rear_Qtr.Measurement.dE_25 != "" && rear_Qtr.Measurement.dE_45 != "" && rear_Qtr.Measurement.dE_75 != "" && rear_Qtr.Measurement.dE_110 != "" && rear_Qtr.Measurement.dL_Minus15 != "" && rear_Qtr.Measurement.dL_15 != "" && rear_Qtr.Measurement.dL_25 != "" && rear_Qtr.Measurement.dL_45 != "" && rear_Qtr.Measurement.dL_75 != "" && rear_Qtr.Measurement.dL_110 != "" && rear_Qtr.Measurement.da_Minus15 != "" && rear_Qtr.Measurement.da_15 != "" && rear_Qtr.Measurement.da_25 != "" && rear_Qtr.Measurement.da_45 != "" && rear_Qtr.Measurement.da_75 != "" && rear_Qtr.Measurement.da_110 != "" && rear_Qtr.Measurement.db_Minus15 != "" && rear_Qtr.Measurement.db_15 != "" && rear_Qtr.Measurement.db_25 != "" && rear_Qtr.Measurement.db_45 != "" && rear_Qtr.Measurement.db_75 != "" && rear_Qtr.Measurement.db_110 != "" && rear_Qtr.Measurement.L_Minus15 != "" && rear_Qtr.Measurement.L_15 != "" && rear_Qtr.Measurement.L_25 != "" && rear_Qtr.Measurement.L_45 != "" && rear_Qtr.Measurement.L_75 != "" && rear_Qtr.Measurement.L_110 != "" && rear_Qtr.Measurement.a_Minus15 != "" && rear_Qtr.Measurement.a_15 != "" && rear_Qtr.Measurement.a_25 != "" && rear_Qtr.Measurement.a_45 != "" && rear_Qtr.Measurement.a_75 != "" && rear_Qtr.Measurement.a_110 != "" && rear_Qtr.Measurement.b_Minus15 != "" && rear_Qtr.Measurement.b_15 != "" && rear_Qtr.Measurement.b_25 != "" && rear_Qtr.Measurement.b_45 != "" && rear_Qtr.Measurement.b_75 != "" && rear_Qtr.Measurement.b_110 != "" && rear_Bumper.Measurement.dE_Minus15 != "" && rear_Bumper.Measurement.dE_15 != "" && rear_Bumper.Measurement.dE_25 != "" && rear_Bumper.Measurement.dE_45 != "" && rear_Bumper.Measurement.dE_75 != "" && rear_Bumper.Measurement.dE_110 != "" && rear_Bumper.Measurement.dL_Minus15 != "" && rear_Bumper.Measurement.dL_15 != "" && rear_Bumper.Measurement.dL_25 != "" && rear_Bumper.Measurement.dL_45 != "" && rear_Bumper.Measurement.dL_75 != "" && rear_Bumper.Measurement.dL_110 != "" && rear_Bumper.Measurement.da_Minus15 != "" && rear_Bumper.Measurement.da_15 != "" && rear_Bumper.Measurement.da_25 != "" && rear_Bumper.Measurement.da_45 != "" && rear_Bumper.Measurement.da_75 != "" && rear_Bumper.Measurement.da_110 != "" && rear_Bumper.Measurement.db_Minus15 != "" && rear_Bumper.Measurement.db_15 != "" && rear_Bumper.Measurement.db_25 != "" && rear_Bumper.Measurement.db_45 != "" && rear_Bumper.Measurement.db_75 != "" && rear_Bumper.Measurement.db_110 != "" && rear_Bumper.Measurement.L_Minus15 != "" && rear_Bumper.Measurement.L_15 != "" && rear_Bumper.Measurement.L_25 != "" && rear_Bumper.Measurement.L_45 != "" && rear_Bumper.Measurement.L_75 != "" && rear_Bumper.Measurement.L_110 != "" && rear_Bumper.Measurement.a_Minus15 != "" && rear_Bumper.Measurement.a_15 != "" && rear_Bumper.Measurement.a_25 != "" && rear_Bumper.Measurement.a_45 != "" && rear_Bumper.Measurement.a_75 != "" && rear_Bumper.Measurement.a_110 != "" && rear_Bumper.Measurement.b_Minus15 != "" && rear_Bumper.Measurement.b_15 != "" && rear_Bumper.Measurement.b_25 != "" && rear_Bumper.Measurement.b_45 != "" && rear_Bumper.Measurement.b_75 != "" && rear_Bumper.Measurement.b_110 != ""))
                        {
                            double dE_Minus15 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_Minus15), Convert.ToDouble(rear_Qtr.Measurement.a_Minus15), Convert.ToDouble(rear_Qtr.Measurement.L_Minus15), Convert.ToDouble(rear_Bumper.Measurement.L_Minus15), Convert.ToDouble(rear_Bumper.Measurement.a_Minus15), Convert.ToDouble(rear_Bumper.Measurement.L_Minus15));
                            double dE_15 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_15), Convert.ToDouble(rear_Qtr.Measurement.a_15), Convert.ToDouble(rear_Qtr.Measurement.L_15), Convert.ToDouble(rear_Bumper.Measurement.L_15), Convert.ToDouble(rear_Bumper.Measurement.a_15), Convert.ToDouble(rear_Bumper.Measurement.L_15));
                            double dE_25 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_25), Convert.ToDouble(rear_Qtr.Measurement.a_25), Convert.ToDouble(rear_Qtr.Measurement.L_25), Convert.ToDouble(rear_Bumper.Measurement.L_25), Convert.ToDouble(rear_Bumper.Measurement.a_25), Convert.ToDouble(rear_Bumper.Measurement.L_25));
                            double dE_45 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_45), Convert.ToDouble(rear_Qtr.Measurement.a_45), Convert.ToDouble(rear_Qtr.Measurement.L_45), Convert.ToDouble(rear_Bumper.Measurement.L_45), Convert.ToDouble(rear_Bumper.Measurement.a_45), Convert.ToDouble(rear_Bumper.Measurement.L_45));
                            double dE_75 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_75), Convert.ToDouble(rear_Qtr.Measurement.a_75), Convert.ToDouble(rear_Qtr.Measurement.L_75), Convert.ToDouble(rear_Bumper.Measurement.L_75), Convert.ToDouble(rear_Bumper.Measurement.a_75), Convert.ToDouble(rear_Bumper.Measurement.L_75));
                            double dE_110 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_110), Convert.ToDouble(rear_Qtr.Measurement.a_110), Convert.ToDouble(rear_Qtr.Measurement.L_110), Convert.ToDouble(rear_Bumper.Measurement.L_110), Convert.ToDouble(rear_Bumper.Measurement.a_110), Convert.ToDouble(rear_Bumper.Measurement.L_110));

                            if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangeValue &&
                                dE_15 > (passRange.Rear_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangeValue &&
                                dE_25 > (passRange.Rear_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangeValue &&
                                dE_45 > (passRange.Rear_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangeValue &&
                                dE_75 > (passRange.Rear_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangeValue &&
                                dE_110 > (passRange.Rear_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.L_Minus15)) < passRange.Rear_dL_Minus15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.a_Minus15)) < passRange.Rear_da_Minus15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.b_Minus15)) < passRange.Rear_db_Minus15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_15) - Convert.ToDouble(rear_Bumper.Measurement.L_15)) > (passRange.Rear_dL_15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_15) - Convert.ToDouble(rear_Bumper.Measurement.L_15)) < passRange.Rear_dL_15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_15) - Convert.ToDouble(rear_Bumper.Measurement.a_15)) > (passRange.Rear_da_15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_15) - Convert.ToDouble(rear_Bumper.Measurement.a_15)) < passRange.Rear_da_15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_15) - Convert.ToDouble(rear_Bumper.Measurement.b_15)) > (passRange.Rear_db_15_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_15) - Convert.ToDouble(rear_Bumper.Measurement.b_15)) < passRange.Rear_db_15_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_25) - Convert.ToDouble(rear_Bumper.Measurement.L_25)) > (passRange.Rear_dL_25_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_25) - Convert.ToDouble(rear_Bumper.Measurement.L_25)) < passRange.Rear_dL_25_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_25) - Convert.ToDouble(rear_Bumper.Measurement.a_25)) > (passRange.Rear_da_25_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_25) - Convert.ToDouble(rear_Bumper.Measurement.a_25)) < passRange.Rear_da_25_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_25) - Convert.ToDouble(rear_Bumper.Measurement.b_25)) > (passRange.Rear_db_25_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_25) - Convert.ToDouble(rear_Bumper.Measurement.b_25)) < passRange.Rear_db_25_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_45) - Convert.ToDouble(rear_Bumper.Measurement.L_45)) > (passRange.Rear_dL_45_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_45) - Convert.ToDouble(rear_Bumper.Measurement.L_45)) < passRange.Rear_dL_45_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_45) - Convert.ToDouble(rear_Bumper.Measurement.a_45)) > (passRange.Rear_da_45_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_45) - Convert.ToDouble(rear_Bumper.Measurement.a_45)) < passRange.Rear_da_45_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_45) - Convert.ToDouble(rear_Bumper.Measurement.b_45)) > (passRange.Rear_db_45_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_45) - Convert.ToDouble(rear_Bumper.Measurement.b_45)) < passRange.Rear_db_45_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_75) - Convert.ToDouble(rear_Bumper.Measurement.L_75)) > (passRange.Rear_dL_75_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_75) - Convert.ToDouble(rear_Bumper.Measurement.L_75)) < passRange.Rear_dL_75_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_75) - Convert.ToDouble(rear_Bumper.Measurement.a_75)) > (passRange.Rear_da_75_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_75) - Convert.ToDouble(rear_Bumper.Measurement.a_75)) < passRange.Rear_da_75_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_75) - Convert.ToDouble(rear_Bumper.Measurement.b_75)) > (passRange.Rear_db_75_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_75) - Convert.ToDouble(rear_Bumper.Measurement.b_75)) < passRange.Rear_db_75_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_110) - Convert.ToDouble(rear_Bumper.Measurement.L_110)) > (passRange.Rear_dL_110_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_110) - Convert.ToDouble(rear_Bumper.Measurement.L_110)) < passRange.Rear_dL_110_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_110) - Convert.ToDouble(rear_Bumper.Measurement.a_110)) > (passRange.Rear_da_110_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_110) - Convert.ToDouble(rear_Bumper.Measurement.a_110)) < passRange.Rear_da_110_PassRangeValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_110) - Convert.ToDouble(rear_Bumper.Measurement.b_110)) > (passRange.Rear_db_110_PassRangeValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_110) - Convert.ToDouble(rear_Bumper.Measurement.b_110)) < passRange.Rear_db_110_PassRangeValue)
                            {
                                rr_result = true;
                            }
                            else
                            {
                                rr_result = false;
                            }
                        }
                        else
                        {
                            if (iniManager.BlankIsOk)
                            {
                                rr_result = true;
                            }
                            else
                            {
                                rr_result = false;
                            }
                        }
                    }
                    else
                    {
                        if (rear_Qtr != null || rear_Bumper != null || (rear_Qtr.Measurement.dE_Minus15 != null && rear_Qtr.Measurement.dE_15 != null && rear_Qtr.Measurement.dE_25 != null && rear_Qtr.Measurement.dE_45 != null && rear_Qtr.Measurement.dE_75 != null && rear_Qtr.Measurement.dE_110 != null && rear_Qtr.Measurement.dL_Minus15 != null && rear_Qtr.Measurement.dL_15 != null && rear_Qtr.Measurement.dL_25 != null && rear_Qtr.Measurement.dL_45 != null && rear_Qtr.Measurement.dL_75 != null && rear_Qtr.Measurement.dL_110 != null && rear_Qtr.Measurement.da_Minus15 != null && rear_Qtr.Measurement.da_15 != null && rear_Qtr.Measurement.da_25 != null && rear_Qtr.Measurement.da_45 != null && rear_Qtr.Measurement.da_75 != null && rear_Qtr.Measurement.da_110 != null && rear_Qtr.Measurement.db_Minus15 != null && rear_Qtr.Measurement.db_15 != null && rear_Qtr.Measurement.db_25 != null && rear_Qtr.Measurement.db_45 != null && rear_Qtr.Measurement.db_75 != null && rear_Qtr.Measurement.db_110 != null && rear_Qtr.Measurement.L_Minus15 != null && rear_Qtr.Measurement.L_15 != null && rear_Qtr.Measurement.L_25 != null && rear_Qtr.Measurement.L_45 != null && rear_Qtr.Measurement.L_75 != null && rear_Qtr.Measurement.L_110 != null && rear_Qtr.Measurement.a_Minus15 != null && rear_Qtr.Measurement.a_15 != null && rear_Qtr.Measurement.a_25 != null && rear_Qtr.Measurement.a_45 != null && rear_Qtr.Measurement.a_75 != null && rear_Qtr.Measurement.a_110 != null && rear_Qtr.Measurement.b_Minus15 != null && rear_Qtr.Measurement.b_15 != null && rear_Qtr.Measurement.b_25 != null && rear_Qtr.Measurement.b_45 != null && rear_Qtr.Measurement.b_75 != null && rear_Qtr.Measurement.b_110 != null && rear_Bumper.Measurement.dE_Minus15 != null && rear_Bumper.Measurement.dE_15 != null && rear_Bumper.Measurement.dE_25 != null && rear_Bumper.Measurement.dE_45 != null && rear_Bumper.Measurement.dE_75 != null && rear_Bumper.Measurement.dE_110 != null && rear_Bumper.Measurement.dL_Minus15 != null && rear_Bumper.Measurement.dL_15 != null && rear_Bumper.Measurement.dL_25 != null && rear_Bumper.Measurement.dL_45 != null && rear_Bumper.Measurement.dL_75 != null && rear_Bumper.Measurement.dL_110 != null && rear_Bumper.Measurement.da_Minus15 != null && rear_Bumper.Measurement.da_15 != null && rear_Bumper.Measurement.da_25 != null && rear_Bumper.Measurement.da_45 != null && rear_Bumper.Measurement.da_75 != null && rear_Bumper.Measurement.da_110 != null && rear_Bumper.Measurement.db_Minus15 != null && rear_Bumper.Measurement.db_15 != null && rear_Bumper.Measurement.db_25 != null && rear_Bumper.Measurement.db_45 != null && rear_Bumper.Measurement.db_75 != null && rear_Bumper.Measurement.db_110 != null && rear_Bumper.Measurement.L_Minus15 != null && rear_Bumper.Measurement.L_15 != null && rear_Bumper.Measurement.L_25 != null && rear_Bumper.Measurement.L_45 != null && rear_Bumper.Measurement.L_75 != null && rear_Bumper.Measurement.L_110 != null && rear_Bumper.Measurement.a_Minus15 != null && rear_Bumper.Measurement.a_15 != null && rear_Bumper.Measurement.a_25 != null && rear_Bumper.Measurement.a_45 != null && rear_Bumper.Measurement.a_75 != null && rear_Bumper.Measurement.a_110 != null && rear_Bumper.Measurement.b_Minus15 != null && rear_Bumper.Measurement.b_15 != null && rear_Bumper.Measurement.b_25 != null && rear_Bumper.Measurement.b_45 != null && rear_Bumper.Measurement.b_75 != null && rear_Bumper.Measurement.b_110 != null && rear_Qtr.Measurement.dE_Minus15 != "" && rear_Qtr.Measurement.dE_15 != "" && rear_Qtr.Measurement.dE_25 != "" && rear_Qtr.Measurement.dE_45 != "" && rear_Qtr.Measurement.dE_75 != "" && rear_Qtr.Measurement.dE_110 != "" && rear_Qtr.Measurement.dL_Minus15 != "" && rear_Qtr.Measurement.dL_15 != "" && rear_Qtr.Measurement.dL_25 != "" && rear_Qtr.Measurement.dL_45 != "" && rear_Qtr.Measurement.dL_75 != "" && rear_Qtr.Measurement.dL_110 != "" && rear_Qtr.Measurement.da_Minus15 != "" && rear_Qtr.Measurement.da_15 != "" && rear_Qtr.Measurement.da_25 != "" && rear_Qtr.Measurement.da_45 != "" && rear_Qtr.Measurement.da_75 != "" && rear_Qtr.Measurement.da_110 != "" && rear_Qtr.Measurement.db_Minus15 != "" && rear_Qtr.Measurement.db_15 != "" && rear_Qtr.Measurement.db_25 != "" && rear_Qtr.Measurement.db_45 != "" && rear_Qtr.Measurement.db_75 != "" && rear_Qtr.Measurement.db_110 != "" && rear_Qtr.Measurement.L_Minus15 != "" && rear_Qtr.Measurement.L_15 != "" && rear_Qtr.Measurement.L_25 != "" && rear_Qtr.Measurement.L_45 != "" && rear_Qtr.Measurement.L_75 != "" && rear_Qtr.Measurement.L_110 != "" && rear_Qtr.Measurement.a_Minus15 != "" && rear_Qtr.Measurement.a_15 != "" && rear_Qtr.Measurement.a_25 != "" && rear_Qtr.Measurement.a_45 != "" && rear_Qtr.Measurement.a_75 != "" && rear_Qtr.Measurement.a_110 != "" && rear_Qtr.Measurement.b_Minus15 != "" && rear_Qtr.Measurement.b_15 != "" && rear_Qtr.Measurement.b_25 != "" && rear_Qtr.Measurement.b_45 != "" && rear_Qtr.Measurement.b_75 != "" && rear_Qtr.Measurement.b_110 != "" && rear_Bumper.Measurement.dE_Minus15 != "" && rear_Bumper.Measurement.dE_15 != "" && rear_Bumper.Measurement.dE_25 != "" && rear_Bumper.Measurement.dE_45 != "" && rear_Bumper.Measurement.dE_75 != "" && rear_Bumper.Measurement.dE_110 != "" && rear_Bumper.Measurement.dL_Minus15 != "" && rear_Bumper.Measurement.dL_15 != "" && rear_Bumper.Measurement.dL_25 != "" && rear_Bumper.Measurement.dL_45 != "" && rear_Bumper.Measurement.dL_75 != "" && rear_Bumper.Measurement.dL_110 != "" && rear_Bumper.Measurement.da_Minus15 != "" && rear_Bumper.Measurement.da_15 != "" && rear_Bumper.Measurement.da_25 != "" && rear_Bumper.Measurement.da_45 != "" && rear_Bumper.Measurement.da_75 != "" && rear_Bumper.Measurement.da_110 != "" && rear_Bumper.Measurement.db_Minus15 != "" && rear_Bumper.Measurement.db_15 != "" && rear_Bumper.Measurement.db_25 != "" && rear_Bumper.Measurement.db_45 != "" && rear_Bumper.Measurement.db_75 != "" && rear_Bumper.Measurement.db_110 != "" && rear_Bumper.Measurement.L_Minus15 != "" && rear_Bumper.Measurement.L_15 != "" && rear_Bumper.Measurement.L_25 != "" && rear_Bumper.Measurement.L_45 != "" && rear_Bumper.Measurement.L_75 != "" && rear_Bumper.Measurement.L_110 != "" && rear_Bumper.Measurement.a_Minus15 != "" && rear_Bumper.Measurement.a_15 != "" && rear_Bumper.Measurement.a_25 != "" && rear_Bumper.Measurement.a_45 != "" && rear_Bumper.Measurement.a_75 != "" && rear_Bumper.Measurement.a_110 != "" && rear_Bumper.Measurement.b_Minus15 != "" && rear_Bumper.Measurement.b_15 != "" && rear_Bumper.Measurement.b_25 != "" && rear_Bumper.Measurement.b_45 != "" && rear_Bumper.Measurement.b_75 != "" && rear_Bumper.Measurement.b_110 != ""))
                        {
                            double dE_Minus15 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_Minus15), Convert.ToDouble(rear_Qtr.Measurement.a_Minus15), Convert.ToDouble(rear_Qtr.Measurement.L_Minus15), Convert.ToDouble(rear_Bumper.Measurement.L_Minus15), Convert.ToDouble(rear_Bumper.Measurement.a_Minus15), Convert.ToDouble(rear_Bumper.Measurement.L_Minus15));
                            double dE_15 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_15), Convert.ToDouble(rear_Qtr.Measurement.a_15), Convert.ToDouble(rear_Qtr.Measurement.L_15), Convert.ToDouble(rear_Bumper.Measurement.L_15), Convert.ToDouble(rear_Bumper.Measurement.a_15), Convert.ToDouble(rear_Bumper.Measurement.L_15));
                            double dE_25 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_25), Convert.ToDouble(rear_Qtr.Measurement.a_25), Convert.ToDouble(rear_Qtr.Measurement.L_25), Convert.ToDouble(rear_Bumper.Measurement.L_25), Convert.ToDouble(rear_Bumper.Measurement.a_25), Convert.ToDouble(rear_Bumper.Measurement.L_25));
                            double dE_45 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_45), Convert.ToDouble(rear_Qtr.Measurement.a_45), Convert.ToDouble(rear_Qtr.Measurement.L_45), Convert.ToDouble(rear_Bumper.Measurement.L_45), Convert.ToDouble(rear_Bumper.Measurement.a_45), Convert.ToDouble(rear_Bumper.Measurement.L_45));
                            double dE_75 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_75), Convert.ToDouble(rear_Qtr.Measurement.a_75), Convert.ToDouble(rear_Qtr.Measurement.L_75), Convert.ToDouble(rear_Bumper.Measurement.L_75), Convert.ToDouble(rear_Bumper.Measurement.a_75), Convert.ToDouble(rear_Bumper.Measurement.L_75));
                            double dE_110 = CalcDelta(Convert.ToDouble(rear_Qtr.Measurement.L_110), Convert.ToDouble(rear_Qtr.Measurement.a_110), Convert.ToDouble(rear_Qtr.Measurement.L_110), Convert.ToDouble(rear_Bumper.Measurement.L_110), Convert.ToDouble(rear_Bumper.Measurement.a_110), Convert.ToDouble(rear_Bumper.Measurement.L_110));

                            if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangePlusValue &&
                                dE_15 > (passRange.Rear_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangePlusValue &&
                                dE_25 > (passRange.Rear_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangePlusValue &&
                                dE_45 > (passRange.Rear_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangePlusValue &&
                                dE_75 > (passRange.Rear_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangePlusValue &&
                                dE_110 > (passRange.Rear_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.L_Minus15)) < passRange.Rear_dL_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.a_Minus15)) < passRange.Rear_da_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_Minus15) - Convert.ToDouble(rear_Bumper.Measurement.b_Minus15)) < passRange.Rear_db_Minus15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_15) - Convert.ToDouble(rear_Bumper.Measurement.L_15)) > (passRange.Rear_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_15) - Convert.ToDouble(rear_Bumper.Measurement.L_15)) < passRange.Rear_dL_15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_15) - Convert.ToDouble(rear_Bumper.Measurement.a_15)) > (passRange.Rear_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_15) - Convert.ToDouble(rear_Bumper.Measurement.a_15)) < passRange.Rear_da_15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_15) - Convert.ToDouble(rear_Bumper.Measurement.b_15)) > (passRange.Rear_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_15) - Convert.ToDouble(rear_Bumper.Measurement.b_15)) < passRange.Rear_db_15_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_25) - Convert.ToDouble(rear_Bumper.Measurement.L_25)) > (passRange.Rear_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_25) - Convert.ToDouble(rear_Bumper.Measurement.L_25)) < passRange.Rear_dL_25_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_25) - Convert.ToDouble(rear_Bumper.Measurement.a_25)) > (passRange.Rear_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_25) - Convert.ToDouble(rear_Bumper.Measurement.a_25)) < passRange.Rear_da_25_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_25) - Convert.ToDouble(rear_Bumper.Measurement.b_25)) > (passRange.Rear_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_25) - Convert.ToDouble(rear_Bumper.Measurement.b_25)) < passRange.Rear_db_25_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_45) - Convert.ToDouble(rear_Bumper.Measurement.L_45)) > (passRange.Rear_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_45) - Convert.ToDouble(rear_Bumper.Measurement.L_45)) < passRange.Rear_dL_45_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_45) - Convert.ToDouble(rear_Bumper.Measurement.a_45)) > (passRange.Rear_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_45) - Convert.ToDouble(rear_Bumper.Measurement.a_45)) < passRange.Rear_da_45_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_45) - Convert.ToDouble(rear_Bumper.Measurement.b_45)) > (passRange.Rear_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_45) - Convert.ToDouble(rear_Bumper.Measurement.b_45)) < passRange.Rear_db_45_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_75) - Convert.ToDouble(rear_Bumper.Measurement.L_75)) > (passRange.Rear_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_75) - Convert.ToDouble(rear_Bumper.Measurement.L_75)) < passRange.Rear_dL_75_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_75) - Convert.ToDouble(rear_Bumper.Measurement.a_75)) > (passRange.Rear_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_75) - Convert.ToDouble(rear_Bumper.Measurement.a_75)) < passRange.Rear_da_75_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_75) - Convert.ToDouble(rear_Bumper.Measurement.b_75)) > (passRange.Rear_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_75) - Convert.ToDouble(rear_Bumper.Measurement.b_75)) < passRange.Rear_db_75_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.L_110) - Convert.ToDouble(rear_Bumper.Measurement.L_110)) > (passRange.Rear_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.L_110) - Convert.ToDouble(rear_Bumper.Measurement.L_110)) < passRange.Rear_dL_110_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.a_110) - Convert.ToDouble(rear_Bumper.Measurement.a_110)) > (passRange.Rear_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.a_110) - Convert.ToDouble(rear_Bumper.Measurement.a_110)) < passRange.Rear_da_110_PassRangePlusValue &&
                                (Convert.ToDouble(rear_Qtr.Measurement.b_110) - Convert.ToDouble(rear_Bumper.Measurement.b_110)) > (passRange.Rear_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(rear_Qtr.Measurement.b_110) - Convert.ToDouble(rear_Bumper.Measurement.b_110)) < passRange.Rear_db_110_PassRangePlusValue)
                            {
                                rr_result = true;
                            }
                            else
                            {
                                rr_result = false;
                            }
                        }
                        else
                        {
                            if (iniManager.BlankIsOk)
                            {
                                rr_result = true;
                            }
                            else
                            {
                                rr_result = false;
                            }
                        }
                    }

                    if (fr_result && rr_result)
                    {
                        sw.Stop();
                        logManager.Trace("판정 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

                        return Result.ALL_OK;
                    }
                    else if (fr_result && !rr_result)
                    {
                        sw.Stop();
                        logManager.Trace("판정 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

                        return Result.FR_OK_RR_NG;
                    }
                    else if (!fr_result && rr_result)
                    {
                        sw.Stop();
                        logManager.Trace("판정 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

                        return Result.FR_NG_RR_OK;
                    }
                    else
                    {
                        sw.Stop();
                        logManager.Trace("판정 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

                        return Result.ALL_NG;
                    }
                }
                else
                {
                    return Result.ALL_PASS;
                }
            }
            catch (Exception e)
            {
                return Result.ALL_PASS;
            }
        }

        private double CalcDelta(StructSensorData a, StructSensorData b)
        {
            double _L = Convert.ToDouble(a.Measurement.L_45) - Convert.ToDouble(b.Measurement.L_45);
            double _a = Convert.ToDouble(a.Measurement.a_45) - Convert.ToDouble(b.Measurement.a_45);
            double _b = Convert.ToDouble(a.Measurement.b_45) - Convert.ToDouble(b.Measurement.b_45);

            double value = Math.Sqrt(Math.Pow(_L, 2) + Math.Pow(_a, 2) + Math.Pow(_b, 2));

            return value;
        }

        private double CalcDelta(double L1, double a1, double b1, double L2, double a2, double b2)
        {
            double _L = Convert.ToDouble(L1) - Convert.ToDouble(L2);
            double _a = Convert.ToDouble(a1) - Convert.ToDouble(a2);
            double _b = Convert.ToDouble(b1) - Convert.ToDouble(b2);

            double value = Math.Sqrt(Math.Pow(_L, 2) + Math.Pow(_a, 2) + Math.Pow(_b, 2));

            return value;
        }

        public async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            CommonSaveFileDialog saveFileDialog = new CommonSaveFileDialog();
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("CSV 파일", "*.csv"));
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("모든 파일", "*.*"));
            saveFileDialog.DefaultFileName = ".csv";

            if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ProgressDialogController controller = await window.ShowProgressAsync("저장하는 중", "데이터를 적재 중 입니다...");
                controller.SetCancelable(false);
                controller.SetIndeterminate();

                await Task.Run(() =>
                {
                    string content = "측정일시,차종,SEQ,색상,BodyNumber,전체판정,";

                    if (is_dE_Minus15)
                    {
                        content += "FR FENDA dE-15,";
                    }

                    if (is_dE_15)
                    {
                        content += "FR FENDA dE15,";
                    }

                    if (is_dE_25)
                    {
                        content += "FR FENDA dE25,";
                    }

                    if (is_dE_45)
                    {
                        content += "FR FENDA dE45,";
                    }

                    if (is_dE_75)
                    {
                        content += "FR FENDA dE75,";
                    }

                    if (is_dE_110)
                    {
                        content += "FR FENDA dE110,";
                    }

                    if (is_L_Minus15)
                    {
                        content += "FR FENDA L-15,";
                    }

                    if (is_a_Minus15)
                    {
                        content += "FR FENDA a-15,";
                    }

                    if (is_b_Minus15)
                    {
                        content += "FR FENDA b-15,";
                    }

                    if (is_L_15)
                    {
                        content += "FR FENDA L15,";
                    }

                    if (is_a_15)
                    {
                        content += "FR FENDA a15,";
                    }

                    if (is_b_15)
                    {
                        content += "FR FENDA b15,";
                    }

                    if (is_L_25)
                    {
                        content += "FR FENDA L25,";
                    }

                    if (is_a_25)
                    {
                        content += "FR FENDA a25,";
                    }

                    if (is_b_25)
                    {
                        content += "FR FENDA b25,";
                    }

                    if (is_L_45)
                    {
                        content += "FR FENDA L45,";
                    }

                    if (is_a_45)
                    {
                        content += "FR FENDA a45,";
                    }

                    if (is_b_45)
                    {
                        content += "FR FENDA b45,";
                    }

                    if (is_L_75)
                    {
                        content += "FR FENDA L75,";
                    }

                    if (is_a_75)
                    {
                        content += "FR FENDA a75,";
                    }

                    if (is_b_75)
                    {
                        content += "FR FENDA b75,";
                    }

                    if (is_L_110)
                    {
                        content += "FR FENDA L110,";
                    }

                    if (is_a_110)
                    {
                        content += "FR FENDA a110,";
                    }

                    if (is_b_110)
                    {
                        content += "FR FENDA b110,";
                    }

                    if (is_dL_Minus15)
                    {
                        content += "FR FENDA dL-15,";
                    }

                    if (is_da_Minus15)
                    {
                        content += "FR FENDA da-15,";
                    }

                    if (is_db_Minus15)
                    {
                        content += "FR FENDA db-15,";
                    }

                    if (is_dL_15)
                    {
                        content += "FR FENDA dL15,";
                    }

                    if (is_da_15)
                    {
                        content += "FR FENDA da15,";
                    }

                    if (is_db_15)
                    {
                        content += "FR FENDA db15,";
                    }

                    if (is_dL_25)
                    {
                        content += "FR FENDA dL25,";
                    }

                    if (is_da_25)
                    {
                        content += "FR FENDA da25,";
                    }

                    if (is_db_25)
                    {
                        content += "FR FENDA db25,";
                    }

                    if (is_dL_45)
                    {
                        content += "FR FENDA dL45,";
                    }

                    if (is_da_45)
                    {
                        content += "FR FENDA da45,";
                    }

                    if (is_db_45)
                    {
                        content += "FR FENDA db45,";
                    }

                    if (is_dL_75)
                    {
                        content += "FR FENDA dL75,";
                    }

                    if (is_da_75)
                    {
                        content += "FR FENDA da75,";
                    }

                    if (is_db_75)
                    {
                        content += "FR FENDA db75,";
                    }

                    if (is_dL_110)
                    {
                        content += "FR FENDA dL110,";
                    }

                    if (is_da_110)
                    {
                        content += "FR FENDA da110,";
                    }

                    if (is_db_110)
                    {
                        content += "FR FENDA db110,";
                    }

                    if (is_dE_Minus15)
                    {
                        content += "FR BUMPER dE-15,";
                    }

                    if (is_dE_15)
                    {
                        content += "FR BUMPER dE15,";
                    }

                    if (is_dE_25)
                    {
                        content += "FR BUMPER dE25,";
                    }

                    if (is_dE_45)
                    {
                        content += "FR BUMPER dE45,";
                    }

                    if (is_dE_75)
                    {
                        content += "FR BUMPER dE75,";
                    }

                    if (is_dE_110)
                    {
                        content += "FR BUMPER dE110,";
                    }

                    if (is_L_Minus15)
                    {
                        content += "FR BUMPER L-15,";
                    }

                    if (is_a_Minus15)
                    {
                        content += "FR BUMPER a-15,";
                    }

                    if (is_b_Minus15)
                    {
                        content += "FR BUMPER b-15,";
                    }

                    if (is_L_15)
                    {
                        content += "FR BUMPER L15,";
                    }

                    if (is_a_15)
                    {
                        content += "FR BUMPER a15,";
                    }

                    if (is_b_15)
                    {
                        content += "FR BUMPER b15,";
                    }

                    if (is_L_25)
                    {
                        content += "FR BUMPER L25,";
                    }

                    if (is_a_25)
                    {
                        content += "FR BUMPER a25,";
                    }

                    if (is_b_25)
                    {
                        content += "FR BUMPER b25,";
                    }

                    if (is_L_45)
                    {
                        content += "FR BUMPER L45,";
                    }

                    if (is_a_45)
                    {
                        content += "FR BUMPER a45,";
                    }

                    if (is_b_45)
                    {
                        content += "FR BUMPER b45,";
                    }

                    if (is_L_75)
                    {
                        content += "FR BUMPER L75,";
                    }

                    if (is_a_75)
                    {
                        content += "FR BUMPER a75,";
                    }

                    if (is_b_75)
                    {
                        content += "FR BUMPER b75,";
                    }

                    if (is_L_110)
                    {
                        content += "FR BUMPER L110,";
                    }

                    if (is_a_110)
                    {
                        content += "FR BUMPER a110,";
                    }

                    if (is_b_110)
                    {
                        content += "FR BUMPER b110,";
                    }

                    if (is_dL_Minus15)
                    {
                        content += "FR BUMPER dL-15,";
                    }

                    if (is_da_Minus15)
                    {
                        content += "FR BUMPER da-15,";
                    }

                    if (is_db_Minus15)
                    {
                        content += "FR BUMPER db-15,";
                    }

                    if (is_dL_15)
                    {
                        content += "FR BUMPER dL15,";
                    }

                    if (is_da_15)
                    {
                        content += "FR BUMPER da15,";
                    }

                    if (is_db_15)
                    {
                        content += "FR BUMPER db15,";
                    }

                    if (is_dL_25)
                    {
                        content += "FR BUMPER dL25,";
                    }

                    if (is_da_25)
                    {
                        content += "FR BUMPER da25,";
                    }

                    if (is_db_25)
                    {
                        content += "FR BUMPER db25,";
                    }

                    if (is_dL_45)
                    {
                        content += "FR BUMPER dL45,";
                    }

                    if (is_da_45)
                    {
                        content += "FR BUMPER da45,";
                    }

                    if (is_db_45)
                    {
                        content += "FR BUMPER db45,";
                    }

                    if (is_dL_75)
                    {
                        content += "FR BUMPER dL75,";
                    }

                    if (is_da_75)
                    {
                        content += "FR BUMPER da75,";
                    }

                    if (is_db_75)
                    {
                        content += "FR BUMPER db75,";
                    }

                    if (is_dL_110)
                    {
                        content += "FR BUMPER dL110,";
                    }

                    if (is_da_110)
                    {
                        content += "FR BUMPER da110,";
                    }

                    if (is_db_110)
                    {
                        content += "FR BUMPER db110,";
                    }

                    if (is_dE_Minus15)
                    {
                        content += "RR QTR dE-15,";
                    }

                    if (is_dE_15)
                    {
                        content += "RR QTR dE15,";
                    }

                    if (is_dE_25)
                    {
                        content += "RR QTR dE25,";
                    }

                    if (is_dE_45)
                    {
                        content += "RR QTR dE45,";
                    }

                    if (is_dE_75)
                    {
                        content += "RR QTR dE75,";
                    }

                    if (is_dE_110)
                    {
                        content += "RR QTR dE110,";
                    }

                    if (is_L_Minus15)
                    {
                        content += "RR QTR L-15,";
                    }

                    if (is_a_Minus15)
                    {
                        content += "RR QTR a-15,";
                    }

                    if (is_b_Minus15)
                    {
                        content += "RR QTR b-15,";
                    }

                    if (is_L_15)
                    {
                        content += "RR QTR L15,";
                    }

                    if (is_a_15)
                    {
                        content += "RR QTR a15,";
                    }

                    if (is_b_15)
                    {
                        content += "RR QTR b15,";
                    }

                    if (is_L_25)
                    {
                        content += "RR QTR L25,";
                    }

                    if (is_a_25)
                    {
                        content += "RR QTR a25,";
                    }

                    if (is_b_25)
                    {
                        content += "RR QTR b25,";
                    }

                    if (is_L_45)
                    {
                        content += "RR QTR L45,";
                    }

                    if (is_a_45)
                    {
                        content += "RR QTR a45,";
                    }

                    if (is_b_45)
                    {
                        content += "RR QTR b45,";
                    }

                    if (is_L_75)
                    {
                        content += "RR QTR L75,";
                    }

                    if (is_a_75)
                    {
                        content += "RR QTR a75,";
                    }

                    if (is_b_75)
                    {
                        content += "RR QTR b75,";
                    }

                    if (is_L_110)
                    {
                        content += "RR QTR L110,";
                    }

                    if (is_a_110)
                    {
                        content += "RR QTR a110,";
                    }

                    if (is_b_110)
                    {
                        content += "RR QTR b110,";
                    }

                    if (is_dL_Minus15)
                    {
                        content += "RR QTR dL-15,";
                    }

                    if (is_da_Minus15)
                    {
                        content += "RR QTR da-15,";
                    }

                    if (is_db_Minus15)
                    {
                        content += "RR QTR db-15,";
                    }

                    if (is_dL_15)
                    {
                        content += "RR QTR dL15,";
                    }

                    if (is_da_15)
                    {
                        content += "RR QTR da15,";
                    }

                    if (is_db_15)
                    {
                        content += "RR QTR db15,";
                    }

                    if (is_dL_25)
                    {
                        content += "RR QTR dL25,";
                    }

                    if (is_da_25)
                    {
                        content += "RR QTR da25,";
                    }

                    if (is_db_25)
                    {
                        content += "RR QTR db25,";
                    }

                    if (is_dL_45)
                    {
                        content += "RR QTR dL45,";
                    }

                    if (is_da_45)
                    {
                        content += "RR QTR da45,";
                    }

                    if (is_db_45)
                    {
                        content += "RR QTR db45,";
                    }

                    if (is_dL_75)
                    {
                        content += "RR QTR dL75,";
                    }

                    if (is_da_75)
                    {
                        content += "RR QTR da75,";
                    }

                    if (is_db_75)
                    {
                        content += "RR QTR db75,";
                    }

                    if (is_dL_110)
                    {
                        content += "RR QTR dL110,";
                    }

                    if (is_da_110)
                    {
                        content += "RR QTR da110,";
                    }

                    if (is_db_110)
                    {
                        content += "RR QTR db110,";
                    }

                    if (is_dE_Minus15)
                    {
                        content += "RR BUMPER dE-15,";
                    }

                    if (is_dE_15)
                    {
                        content += "RR BUMPER dE15,";
                    }

                    if (is_dE_25)
                    {
                        content += "RR BUMPER dE25,";
                    }

                    if (is_dE_45)
                    {
                        content += "RR BUMPER dE45,";
                    }

                    if (is_dE_75)
                    {
                        content += "RR BUMPER dE75,";
                    }

                    if (is_dE_110)
                    {
                        content += "RR BUMPER dE110,";
                    }

                    if (is_L_Minus15)
                    {
                        content += "RR BUMPER L-15,";
                    }

                    if (is_a_Minus15)
                    {
                        content += "RR BUMPER a-15,";
                    }

                    if (is_b_Minus15)
                    {
                        content += "RR BUMPER b-15,";
                    }

                    if (is_L_15)
                    {
                        content += "RR BUMPER L15,";
                    }

                    if (is_a_15)
                    {
                        content += "RR BUMPER a15,";
                    }

                    if (is_b_15)
                    {
                        content += "RR BUMPER b15,";
                    }

                    if (is_L_25)
                    {
                        content += "RR BUMPER L25,";
                    }

                    if (is_a_25)
                    {
                        content += "RR BUMPER a25,";
                    }

                    if (is_b_25)
                    {
                        content += "RR BUMPER b25,";
                    }

                    if (is_L_45)
                    {
                        content += "RR BUMPER L45,";
                    }

                    if (is_a_45)
                    {
                        content += "RR BUMPER a45,";
                    }

                    if (is_b_45)
                    {
                        content += "RR BUMPER b45,";
                    }

                    if (is_L_75)
                    {
                        content += "RR BUMPER L75,";
                    }

                    if (is_a_75)
                    {
                        content += "RR BUMPER a75,";
                    }

                    if (is_b_75)
                    {
                        content += "RR BUMPER b75,";
                    }

                    if (is_L_110)
                    {
                        content += "RR BUMPER L110,";
                    }

                    if (is_a_110)
                    {
                        content += "RR BUMPER a110,";
                    }

                    if (is_b_110)
                    {
                        content += "RR BUMPER b110,";
                    }

                    if (is_dL_Minus15)
                    {
                        content += "RR BUMPER dL-15,";
                    }

                    if (is_da_Minus15)
                    {
                        content += "RR BUMPER da-15,";
                    }

                    if (is_db_Minus15)
                    {
                        content += "RR BUMPER db-15,";
                    }

                    if (is_dL_15)
                    {
                        content += "RR BUMPER dL15,";
                    }

                    if (is_da_15)
                    {
                        content += "RR BUMPER da15,";
                    }

                    if (is_db_15)
                    {
                        content += "RR BUMPER db15,";
                    }

                    if (is_dL_25)
                    {
                        content += "RR BUMPER dL25,";
                    }

                    if (is_da_25)
                    {
                        content += "RR BUMPER da25,";
                    }

                    if (is_db_25)
                    {
                        content += "RR BUMPER db25,";
                    }

                    if (is_dL_45)
                    {
                        content += "RR BUMPER dL45,";
                    }

                    if (is_da_45)
                    {
                        content += "RR BUMPER da45,";
                    }

                    if (is_db_45)
                    {
                        content += "RR BUMPER db45,";
                    }

                    if (is_dL_75)
                    {
                        content += "RR BUMPER dL75,";
                    }

                    if (is_da_75)
                    {
                        content += "RR BUMPER da75,";
                    }

                    if (is_db_75)
                    {
                        content += "RR BUMPER db75,";
                    }

                    if (is_dL_110)
                    {
                        content += "RR BUMPER dL110,";
                    }

                    if (is_da_110)
                    {
                        content += "RR BUMPER da110,";
                    }

                    if (is_db_110)
                    {
                        content += "RR BUMPER db110,";
                    }

                    content += "FRONT 델타값,FRONT 판정결과,REAR 델타값,REAR 판정결과";

                    content += Environment.NewLine;

                    for (int i = 0; i < reportDataList.Count; i++)
                    {
                        StructReportData structReportData = reportDataList[i];

                        content += structReportData.DateTime + "," + structReportData.Model + "," + structReportData.Comment + "," + structReportData.Color + "," + structReportData.BodyNumber + "," + structReportData.Result + ",";

                        if (is_dE_Minus15)
                        {
                            content += structReportData.FR_FENDA_dE_Minus15 + ",";
                        }

                        if (is_dE_15)
                        {
                            content += structReportData.FR_FENDA_dE_15 + ",";
                        }

                        if (is_dE_25)
                        {
                            content += structReportData.FR_FENDA_dE_25 + ",";
                        }

                        if (is_dE_45)
                        {
                            content += structReportData.FR_FENDA_dE_45 + ",";
                        }

                        if (is_dE_75)
                        {
                            content += structReportData.FR_FENDA_dE_75 + ",";
                        }

                        if (is_dE_110)
                        {
                            content += structReportData.FR_FENDA_dE_110 + ",";
                        }

                        if (is_L_Minus15)
                        {
                            content += structReportData.FR_FENDA_L_Minus15 + ",";
                        }

                        if (is_a_Minus15)
                        {
                            content += structReportData.FR_FENDA_a_Minus15 + ",";
                        }

                        if (is_b_Minus15)
                        {
                            content += structReportData.FR_FENDA_b_Minus15 + ",";
                        }

                        if (is_L_15)
                        {
                            content += structReportData.FR_FENDA_L_15 + ",";
                        }

                        if (is_a_15)
                        {
                            content += structReportData.FR_FENDA_a_15 + ",";
                        }

                        if (is_b_15)
                        {
                            content += structReportData.FR_FENDA_b_15 + ",";
                        }

                        if (is_L_25)
                        {
                            content += structReportData.FR_FENDA_L_25 + ",";
                        }

                        if (is_a_25)
                        {
                            content += structReportData.FR_FENDA_a_25 + ",";
                        }

                        if (is_b_25)
                        {
                            content += structReportData.FR_FENDA_b_25 + ",";
                        }

                        if (is_L_45)
                        {
                            content += structReportData.FR_FENDA_L_45 + ",";
                        }

                        if (is_a_45)
                        {
                            content += structReportData.FR_FENDA_a_45 + ",";
                        }

                        if (is_b_45)
                        {
                            content += structReportData.FR_FENDA_b_45 + ",";
                        }

                        if (is_L_75)
                        {
                            content += structReportData.FR_FENDA_L_75 + ",";
                        }

                        if (is_a_75)
                        {
                            content += structReportData.FR_FENDA_a_75 + ",";
                        }

                        if (is_b_75)
                        {
                            content += structReportData.FR_FENDA_b_75 + ",";
                        }

                        if (is_L_110)
                        {
                            content += structReportData.FR_FENDA_L_110 + ",";
                        }

                        if (is_a_110)
                        {
                            content += structReportData.FR_FENDA_a_110 + ",";
                        }

                        if (is_b_110)
                        {
                            content += structReportData.FR_FENDA_b_110 + ",";
                        }

                        if (is_dL_Minus15)
                        {
                            content += structReportData.FR_FENDA_dL_Minus15 + ",";
                        }

                        if (is_da_Minus15)
                        {
                            content += structReportData.FR_FENDA_da_Minus15 + ",";
                        }

                        if (is_db_Minus15)
                        {
                            content += structReportData.FR_FENDA_db_Minus15 + ",";
                        }

                        if (is_dL_15)
                        {
                            content += structReportData.FR_FENDA_dL_15 + ",";
                        }

                        if (is_da_15)
                        {
                            content += structReportData.FR_FENDA_da_15 + ",";
                        }

                        if (is_db_15)
                        {
                            content += structReportData.FR_FENDA_db_15 + ",";
                        }

                        if (is_dL_25)
                        {
                            content += structReportData.FR_FENDA_dL_25 + ",";
                        }

                        if (is_da_25)
                        {
                            content += structReportData.FR_FENDA_da_25 + ",";
                        }

                        if (is_db_25)
                        {
                            content += structReportData.FR_FENDA_db_25 + ",";
                        }

                        if (is_dL_45)
                        {
                            content += structReportData.FR_FENDA_dL_45 + ",";
                        }

                        if (is_da_45)
                        {
                            content += structReportData.FR_FENDA_da_45 + ",";
                        }

                        if (is_db_45)
                        {
                            content += structReportData.FR_FENDA_db_45 + ",";
                        }

                        if (is_dL_75)
                        {
                            content += structReportData.FR_FENDA_dL_75 + ",";
                        }

                        if (is_da_75)
                        {
                            content += structReportData.FR_FENDA_da_75 + ",";
                        }

                        if (is_db_75)
                        {
                            content += structReportData.FR_FENDA_db_75 + ",";
                        }

                        if (is_dL_110)
                        {
                            content += structReportData.FR_FENDA_dL_110 + ",";
                        }

                        if (is_da_110)
                        {
                            content += structReportData.FR_FENDA_da_110 + ",";
                        }

                        if (is_db_110)
                        {
                            content += structReportData.FR_FENDA_db_110 + ",";
                        }

                        if (is_dE_Minus15)
                        {
                            content += structReportData.FR_BUMPER_dE_Minus15 + ",";
                        }

                        if (is_dE_15)
                        {
                            content += structReportData.FR_BUMPER_dE_15 + ",";
                        }

                        if (is_dE_25)
                        {
                            content += structReportData.FR_BUMPER_dE_25 + ",";
                        }

                        if (is_dE_45)
                        {
                            content += structReportData.FR_BUMPER_dE_45 + ",";
                        }

                        if (is_dE_75)
                        {
                            content += structReportData.FR_BUMPER_dE_75 + ",";
                        }

                        if (is_dE_110)
                        {
                            content += structReportData.FR_BUMPER_dE_110 + ",";
                        }

                        if (is_L_Minus15)
                        {
                            content += structReportData.FR_BUMPER_L_Minus15 + ",";
                        }

                        if (is_a_Minus15)
                        {
                            content += structReportData.FR_BUMPER_a_Minus15 + ",";
                        }

                        if (is_b_Minus15)
                        {
                            content += structReportData.FR_BUMPER_b_Minus15 + ",";
                        }

                        if (is_L_15)
                        {
                            content += structReportData.FR_BUMPER_L_15 + ",";
                        }

                        if (is_a_15)
                        {
                            content += structReportData.FR_BUMPER_a_15 + ",";
                        }

                        if (is_b_15)
                        {
                            content += structReportData.FR_BUMPER_b_15 + ",";
                        }

                        if (is_L_25)
                        {
                            content += structReportData.FR_BUMPER_L_25 + ",";
                        }

                        if (is_a_25)
                        {
                            content += structReportData.FR_BUMPER_a_25 + ",";
                        }

                        if (is_b_25)
                        {
                            content += structReportData.FR_BUMPER_b_25 + ",";
                        }

                        if (is_L_45)
                        {
                            content += structReportData.FR_BUMPER_L_45 + ",";
                        }

                        if (is_a_45)
                        {
                            content += structReportData.FR_BUMPER_a_45 + ",";
                        }

                        if (is_b_45)
                        {
                            content += structReportData.FR_BUMPER_b_45 + ",";
                        }

                        if (is_L_75)
                        {
                            content += structReportData.FR_BUMPER_L_75 + ",";
                        }

                        if (is_a_75)
                        {
                            content += structReportData.FR_BUMPER_a_75 + ",";
                        }

                        if (is_b_75)
                        {
                            content += structReportData.FR_BUMPER_b_75 + ",";
                        }

                        if (is_L_110)
                        {
                            content += structReportData.FR_BUMPER_L_110 + ",";
                        }

                        if (is_a_110)
                        {
                            content += structReportData.FR_BUMPER_a_110 + ",";
                        }

                        if (is_b_110)
                        {
                            content += structReportData.FR_BUMPER_b_110 + ",";
                        }

                        if (is_dL_Minus15)
                        {
                            content += structReportData.FR_BUMPER_dL_Minus15 + ",";
                        }

                        if (is_da_Minus15)
                        {
                            content += structReportData.FR_BUMPER_da_Minus15 + ",";
                        }

                        if (is_db_Minus15)
                        {
                            content += structReportData.FR_BUMPER_db_Minus15 + ",";
                        }

                        if (is_dL_15)
                        {
                            content += structReportData.FR_BUMPER_dL_15 + ",";
                        }

                        if (is_da_15)
                        {
                            content += structReportData.FR_BUMPER_da_15 + ",";
                        }

                        if (is_db_15)
                        {
                            content += structReportData.FR_BUMPER_db_15 + ",";
                        }

                        if (is_dL_25)
                        {
                            content += structReportData.FR_BUMPER_dL_25 + ",";
                        }

                        if (is_da_25)
                        {
                            content += structReportData.FR_BUMPER_da_25 + ",";
                        }

                        if (is_db_25)
                        {
                            content += structReportData.FR_BUMPER_db_25 + ",";
                        }

                        if (is_dL_45)
                        {
                            content += structReportData.FR_BUMPER_dL_45 + ",";
                        }

                        if (is_da_45)
                        {
                            content += structReportData.FR_BUMPER_da_45 + ",";
                        }

                        if (is_db_45)
                        {
                            content += structReportData.FR_BUMPER_db_45 + ",";
                        }

                        if (is_dL_75)
                        {
                            content += structReportData.FR_BUMPER_dL_75 + ",";
                        }

                        if (is_da_75)
                        {
                            content += structReportData.FR_BUMPER_da_75 + ",";
                        }

                        if (is_db_75)
                        {
                            content += structReportData.FR_BUMPER_db_75 + ",";
                        }

                        if (is_dL_110)
                        {
                            content += structReportData.FR_BUMPER_dL_110 + ",";
                        }

                        if (is_da_110)
                        {
                            content += structReportData.FR_BUMPER_da_110 + ",";
                        }

                        if (is_db_110)
                        {
                            content += structReportData.FR_BUMPER_db_110 + ",";
                        }

                        if (is_dE_Minus15)
                        {
                            content += structReportData.RR_QTR_dE_Minus15 + ",";
                        }

                        if (is_dE_15)
                        {
                            content += structReportData.RR_QTR_dE_15 + ",";
                        }

                        if (is_dE_25)
                        {
                            content += structReportData.RR_QTR_dE_25 + ",";
                        }

                        if (is_dE_45)
                        {
                            content += structReportData.RR_QTR_dE_45 + ",";
                        }

                        if (is_dE_75)
                        {
                            content += structReportData.RR_QTR_dE_75 + ",";
                        }

                        if (is_dE_110)
                        {
                            content += structReportData.RR_QTR_dE_110 + ",";
                        }

                        if (is_L_Minus15)
                        {
                            content += structReportData.RR_QTR_L_Minus15 + ",";
                        }

                        if (is_a_Minus15)
                        {
                            content += structReportData.RR_QTR_a_Minus15 + ",";
                        }

                        if (is_b_Minus15)
                        {
                            content += structReportData.RR_QTR_b_Minus15 + ",";
                        }

                        if (is_L_15)
                        {
                            content += structReportData.RR_QTR_L_15 + ",";
                        }

                        if (is_a_15)
                        {
                            content += structReportData.RR_QTR_a_15 + ",";
                        }

                        if (is_b_15)
                        {
                            content += structReportData.RR_QTR_b_15 + ",";
                        }

                        if (is_L_25)
                        {
                            content += structReportData.RR_QTR_L_25 + ",";
                        }

                        if (is_a_25)
                        {
                            content += structReportData.RR_QTR_a_25 + ",";
                        }

                        if (is_b_25)
                        {
                            content += structReportData.RR_QTR_b_25 + ",";
                        }

                        if (is_L_45)
                        {
                            content += structReportData.RR_QTR_L_45 + ",";
                        }

                        if (is_a_45)
                        {
                            content += structReportData.RR_QTR_a_45 + ",";
                        }

                        if (is_b_45)
                        {
                            content += structReportData.RR_QTR_b_45 + ",";
                        }

                        if (is_L_75)
                        {
                            content += structReportData.RR_QTR_L_75 + ",";
                        }

                        if (is_a_75)
                        {
                            content += structReportData.RR_QTR_a_75 + ",";
                        }

                        if (is_b_75)
                        {
                            content += structReportData.RR_QTR_b_75 + ",";
                        }

                        if (is_L_110)
                        {
                            content += structReportData.RR_QTR_L_110 + ",";
                        }

                        if (is_a_110)
                        {
                            content += structReportData.RR_QTR_a_110 + ",";
                        }

                        if (is_b_110)
                        {
                            content += structReportData.RR_QTR_b_110 + ",";
                        }

                        if (is_dL_Minus15)
                        {
                            content += structReportData.RR_QTR_dL_Minus15 + ",";
                        }

                        if (is_da_Minus15)
                        {
                            content += structReportData.RR_QTR_da_Minus15 + ",";
                        }

                        if (is_db_Minus15)
                        {
                            content += structReportData.RR_QTR_db_Minus15 + ",";
                        }

                        if (is_dL_15)
                        {
                            content += structReportData.RR_QTR_dL_15 + ",";
                        }

                        if (is_da_15)
                        {
                            content += structReportData.RR_QTR_da_15 + ",";
                        }

                        if (is_db_15)
                        {
                            content += structReportData.RR_QTR_db_15 + ",";
                        }

                        if (is_dL_25)
                        {
                            content += structReportData.RR_QTR_dL_25 + ",";
                        }

                        if (is_da_25)
                        {
                            content += structReportData.RR_QTR_da_25 + ",";
                        }

                        if (is_db_25)
                        {
                            content += structReportData.RR_QTR_db_25 + ",";
                        }

                        if (is_dL_45)
                        {
                            content += structReportData.RR_QTR_dL_45 + ",";
                        }

                        if (is_da_45)
                        {
                            content += structReportData.RR_QTR_da_45 + ",";
                        }

                        if (is_db_45)
                        {
                            content += structReportData.RR_QTR_db_45 + ",";
                        }

                        if (is_dL_75)
                        {
                            content += structReportData.RR_QTR_dL_75 + ",";
                        }

                        if (is_da_75)
                        {
                            content += structReportData.RR_QTR_da_75 + ",";
                        }

                        if (is_db_75)
                        {
                            content += structReportData.RR_QTR_db_75 + ",";
                        }

                        if (is_dL_110)
                        {
                            content += structReportData.RR_QTR_dL_110 + ",";
                        }

                        if (is_da_110)
                        {
                            content += structReportData.RR_QTR_da_110 + ",";
                        }

                        if (is_db_110)
                        {
                            content += structReportData.RR_QTR_db_110 + ",";
                        }

                        if (is_dE_Minus15)
                        {
                            content += structReportData.RR_BUMPER_dE_Minus15 + ",";
                        }

                        if (is_dE_15)
                        {
                            content += structReportData.RR_BUMPER_dE_15 + ",";
                        }

                        if (is_dE_25)
                        {
                            content += structReportData.RR_BUMPER_dE_25 + ",";
                        }

                        if (is_dE_45)
                        {
                            content += structReportData.RR_BUMPER_dE_45 + ",";
                        }

                        if (is_dE_75)
                        {
                            content += structReportData.RR_BUMPER_dE_75 + ",";
                        }

                        if (is_dE_110)
                        {
                            content += structReportData.RR_BUMPER_dE_110 + ",";
                        }

                        if (is_L_Minus15)
                        {
                            content += structReportData.RR_BUMPER_L_Minus15 + ",";
                        }

                        if (is_a_Minus15)
                        {
                            content += structReportData.RR_BUMPER_a_Minus15 + ",";
                        }

                        if (is_b_Minus15)
                        {
                            content += structReportData.RR_BUMPER_b_Minus15 + ",";
                        }

                        if (is_L_15)
                        {
                            content += structReportData.RR_BUMPER_L_15 + ",";
                        }

                        if (is_a_15)
                        {
                            content += structReportData.RR_BUMPER_a_15 + ",";
                        }

                        if (is_b_15)
                        {
                            content += structReportData.RR_BUMPER_b_15 + ",";
                        }

                        if (is_L_25)
                        {
                            content += structReportData.RR_BUMPER_L_25 + ",";
                        }

                        if (is_a_25)
                        {
                            content += structReportData.RR_BUMPER_a_25 + ",";
                        }

                        if (is_b_25)
                        {
                            content += structReportData.RR_BUMPER_b_25 + ",";
                        }

                        if (is_L_45)
                        {
                            content += structReportData.RR_BUMPER_L_45 + ",";
                        }

                        if (is_a_45)
                        {
                            content += structReportData.RR_BUMPER_a_45 + ",";
                        }

                        if (is_b_45)
                        {
                            content += structReportData.RR_BUMPER_b_45 + ",";
                        }

                        if (is_L_75)
                        {
                            content += structReportData.RR_BUMPER_L_75 + ",";
                        }

                        if (is_a_75)
                        {
                            content += structReportData.RR_BUMPER_a_75 + ",";
                        }

                        if (is_b_75)
                        {
                            content += structReportData.RR_BUMPER_b_75 + ",";
                        }

                        if (is_L_110)
                        {
                            content += structReportData.RR_BUMPER_L_110 + ",";
                        }

                        if (is_a_110)
                        {
                            content += structReportData.RR_BUMPER_a_110 + ",";
                        }

                        if (is_b_110)
                        {
                            content += structReportData.RR_BUMPER_b_110 + ",";
                        }

                        if (is_dL_Minus15)
                        {
                            content += structReportData.RR_BUMPER_dL_Minus15 + ",";
                        }

                        if (is_da_Minus15)
                        {
                            content += structReportData.RR_BUMPER_da_Minus15 + ",";
                        }

                        if (is_db_Minus15)
                        {
                            content += structReportData.RR_BUMPER_db_Minus15 + ",";
                        }

                        if (is_dL_15)
                        {
                            content += structReportData.RR_BUMPER_dL_15 + ",";
                        }

                        if (is_da_15)
                        {
                            content += structReportData.RR_BUMPER_da_15 + ",";
                        }

                        if (is_db_15)
                        {
                            content += structReportData.RR_BUMPER_db_15 + ",";
                        }

                        if (is_dL_25)
                        {
                            content += structReportData.RR_BUMPER_dL_25 + ",";
                        }

                        if (is_da_25)
                        {
                            content += structReportData.RR_BUMPER_da_25 + ",";
                        }

                        if (is_db_25)
                        {
                            content += structReportData.RR_BUMPER_db_25 + ",";
                        }

                        if (is_dL_45)
                        {
                            content += structReportData.RR_BUMPER_dL_45 + ",";
                        }

                        if (is_da_45)
                        {
                            content += structReportData.RR_BUMPER_da_45 + ",";
                        }

                        if (is_db_45)
                        {
                            content += structReportData.RR_BUMPER_db_45 + ",";
                        }

                        if (is_dL_75)
                        {
                            content += structReportData.RR_BUMPER_dL_75 + ",";
                        }

                        if (is_da_75)
                        {
                            content += structReportData.RR_BUMPER_da_75 + ",";
                        }

                        if (is_db_75)
                        {
                            content += structReportData.RR_BUMPER_db_75 + ",";
                        }

                        if (is_dL_110)
                        {
                            content += structReportData.RR_BUMPER_dL_110 + ",";
                        }

                        if (is_da_110)
                        {
                            content += structReportData.RR_BUMPER_da_110 + ",";
                        }

                        if (is_db_110)
                        {
                            content += structReportData.RR_BUMPER_db_110 + ",";
                        }

                        content += structReportData.FR_DELTA + "," + structReportData.FR_Result + "," + structReportData.RR_DELTA + "," + structReportData.RR_Result;

                        content += Environment.NewLine;

                        controller.SetMessage("데이터를 적재 중 입니다..." + Environment.NewLine + "전체 데이터 수 : " + reportDataList.Count + Environment.NewLine + "적재 데이터 수 : " + (i + 1));
                    }

                    controller.SetMessage("데이터를 저장하는 중 입니다...");

                    try
                    {
                        if (saveFileDialog.FileName.EndsWith(".csv"))
                        {
                            File.WriteAllText(saveFileDialog.FileName, content, Encoding.Unicode);
                        }
                        else
                        {
                            File.WriteAllText(saveFileDialog.FileName + ".csv", content, Encoding.Unicode);
                        }
                    }
                    catch (Exception exc)
                    {
                        logManager.Error(exc.Message);
                    }
                });

                await controller.CloseAsync();
                controller = null;
            }

            saveFileDialog.Dispose();
            saveFileDialog = null;
        }

        public async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<StructReportData> list = new ObservableCollection<StructReportData>();

            ProgressDialogController dialogController = await window.ShowProgressAsync("불러오는 중", "이력 정보를 불러오고 있습니다.");
            dialogController.SetCancelable(false);
            dialogController.SetIndeterminate();

            await Task.Run(() =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    ReportDataList.Clear();
                });

                string dir = m_Config.GetString("Directory", "Result", "E:\\Result");

                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir).ToList().OrderBy(x => x).ToArray();

                    for (int i = 0; i < files.Length; i++)
                    {
                        string filepath = files[i];

                        string filename = Path.GetFileNameWithoutExtension(filepath);
                        int year = Convert.ToInt32(filename.Substring(0, 4));
                        int month = Convert.ToInt32(filename.Substring(4, 2));
                        int day = Convert.ToInt32(filename.Substring(6, 2));
                        int hour = Convert.ToInt32(filename.Substring(8, 2));
                        int min = Convert.ToInt32(filename.Substring(10, 2));
                        int sec = Convert.ToInt32(filename.Substring(12, 2));
                        string dateTime = year + "-" + month + "-" + day + " " + hour + ":" + min + ":" + sec;
                        DateTime startDateTime = new DateTime(year, month, day, hour, min, sec);

                        this.startDateTime = new DateTime(this.startDateTime.Year, this.startDateTime.Month, this.startDateTime.Day, startHour, startMinute, 0);
                        this.endDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endHour, endMinute, 0);


                        if (this.startDateTime <= startDateTime && this.endDateTime > startDateTime)
                        {
                            string model = "";
                            string color = "";
                            string comment = "";
                            string bodyNumber = "";

                            string allText = File.ReadAllText(filepath);

                            string[] headerSplit = allText.Split(new string[] { "</header>" }, StringSplitOptions.None);

                            if (headerSplit.Length > 0)
                            {
                                XmlDocument xmlFile = new XmlDocument();

                                try
                                {
                                    headerSplit[0] += "</header>";
                                    xmlFile.LoadXml(headerSplit[0]);

                                    XmlNodeList xmlList = xmlFile.GetElementsByTagName("header");

                                    for (int j = 0; j < xmlList.Count; j++)
                                    {
                                        XmlNode item = xmlList[j];
                                        model = item["model"].InnerText;
                                        color = item["color"].InnerText;
                                        comment = item["comment"].InnerText;
                                        bodyNumber = item["bodyNumber"].InnerText;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logManager.Error(ex.Message);
                                }

                                if (headerSplit.Length > 1)
                                {
                                    string[] measurementSplit = headerSplit[1].Split(new string[] { "</measurement>" }, StringSplitOptions.None);

                                    if (measurementSplit.Length > 0)
                                    {
                                        StructReportData structReportData = new StructReportData();
                                        structReportData.StartDateTime = startDateTime;
                                        structReportData.DateTime = startDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                        structReportData.Model = model;
                                        structReportData.Color = color;
                                        structReportData.Comment = comment;
                                        structReportData.BodyNumber = bodyNumber;
                                        bool isChecked = true;

                                        int find_index = 0;

                                        for (int l = 0; l < list.Count; l++)
                                        {
                                            if ((list[l].StartDateTime.ToString("yyyy-MM-dd") == structReportData.StartDateTime.ToString("yyyy-MM-dd") || list[l].StartDateTime.AddDays(1).ToString("yyyy-MM-dd") == structReportData.StartDateTime.ToString("yyyy-MM-dd")) && list[l].BodyNumber == structReportData.BodyNumber)
                                            {
                                                find_index = l;
                                            }
                                        }

                                        for (int j = 0; j < measurementSplit.Length; j++)
                                        {
                                            if (measurementSplit[j].Contains("<measurement>"))
                                            {
                                                try
                                                {
                                                    measurementSplit[j] += "</measurement>";
                                                    xmlFile.LoadXml(measurementSplit[j]);

                                                    string measurementText = xmlFile["measurement"].InnerXml;

                                                    xmlFile.LoadXml(measurementText);

                                                    XmlNodeList xmlList = xmlFile.GetElementsByTagName("checkzone");

                                                    for (int k = 0; k < xmlList.Count; k++)
                                                    {
                                                        XmlNode item = xmlList[k];
                                                        string index = item["index"].InnerText;
                                                        string dE_minus15 = "0";

                                                        if (item["dE-15"].InnerText != null)
                                                        {
                                                            dE_minus15 = item["dE-15"].InnerText;
                                                        }

                                                        string dE_15 = "0";

                                                        if (item["dE15"] != null)
                                                        {
                                                            dE_15 = item["dE15"].InnerText;
                                                        }

                                                        string dE_25 = "0";

                                                        if (item["dE25"] != null)
                                                        {
                                                            dE_25 = item["dE25"].InnerText;
                                                        }

                                                        string dE_45 = "0";

                                                        if (item["dE45"] != null)
                                                        {
                                                            dE_45 = item["dE45"].InnerText;
                                                        }

                                                        string dE_75 = "0";

                                                        if (item["dE75"] != null)
                                                        {
                                                            dE_75 = item["dE75"].InnerText;
                                                        }

                                                        string dE_110 = "0";

                                                        if (item["dE110"] != null)
                                                        {
                                                            dE_110 = item["dE110"].InnerText;
                                                        }

                                                        string L_minus15 = "0";

                                                        if (item["L-15"] != null)
                                                        {
                                                            L_minus15 = item["L-15"].InnerText;
                                                        }

                                                        string a_minus15 = "0";

                                                        if (item["a-15"] != null)
                                                        {
                                                            a_minus15 = item["a-15"].InnerText;
                                                        }

                                                        string b_minus15 = "0";

                                                        if (item["b-15"] != null)
                                                        {
                                                            b_minus15 = item["b-15"].InnerText;
                                                        }

                                                        string L_15 = "0";

                                                        if (item["L15"] != null)
                                                        {
                                                            L_15 = item["L15"].InnerText;
                                                        }

                                                        string a_15 = "0";

                                                        if (item["a15"] != null)
                                                        {
                                                            a_15 = item["a15"].InnerText;
                                                        }

                                                        string b_15 = "0";

                                                        if (item["b15"] != null)
                                                        {
                                                            b_15 = item["b15"].InnerText;
                                                        }

                                                        string L_25 = "0";

                                                        if (item["L25"] != null)
                                                        {
                                                            L_25 = item["L25"].InnerText;
                                                        }

                                                        string a_25 = "0";

                                                        if (item["a25"] != null)
                                                        {
                                                            a_25 = item["a25"].InnerText;
                                                        }

                                                        string b_25 = "0";

                                                        if (item["b25"] != null)
                                                        {
                                                            b_25 = item["b25"].InnerText;
                                                        }

                                                        string L_45 = "0";

                                                        if (item["L45"] != null)
                                                        {
                                                            L_45 = item["L45"].InnerText;
                                                        }

                                                        string a_45 = "0";

                                                        if (item["a45"] != null)
                                                        {
                                                            a_45 = item["a45"].InnerText;
                                                        }

                                                        string b_45 = "0";

                                                        if (item["b45"] != null)
                                                        {
                                                            b_45 = item["b45"].InnerText;
                                                        }

                                                        string L_75 = "0";

                                                        if (item["L75"] != null)
                                                        {
                                                            L_75 = item["L75"].InnerText;
                                                        }

                                                        string a_75 = "0";

                                                        if (item["a75"] != null)
                                                        {
                                                            a_75 = item["a75"].InnerText;
                                                        }

                                                        string b_75 = "0";

                                                        if (item["b75"] != null)
                                                        {
                                                            b_75 = item["b75"].InnerText;
                                                        }

                                                        string L_110 = "0";

                                                        if (item["L110"] != null)
                                                        {
                                                            L_110 = item["L110"].InnerText;
                                                        }

                                                        string a_110 = "0";

                                                        if (item["a110"] != null)
                                                        {
                                                            a_110 = item["a110"].InnerText;
                                                        }

                                                        string b_110 = "0";

                                                        if (item["b110"] != null)
                                                        {
                                                            b_110 = item["b110"].InnerText;
                                                        }

                                                        string dL_minus15 = "0";

                                                        if (item["dL-15"] != null)
                                                        {
                                                            dL_minus15 = item["dL-15"].InnerText;
                                                        }

                                                        string da_minus15 = "0";

                                                        if (item["da-15"] != null)
                                                        {
                                                            da_minus15 = item["da-15"].InnerText;
                                                        }

                                                        string db_minus15 = "0";

                                                        if (item["db-15"] != null)
                                                        {
                                                            db_minus15 = item["db-15"].InnerText;
                                                        }

                                                        string dL_15 = "0";

                                                        if (item["dL15"] != null)
                                                        {
                                                            dL_15 = item["dL15"].InnerText;
                                                        }

                                                        string da_15 = "0";

                                                        if (item["da15"] != null)
                                                        {
                                                            da_15 = item["da15"].InnerText;
                                                        }

                                                        string db_15 = "0";

                                                        if (item["db15"] != null)
                                                        {
                                                            db_15 = item["db15"].InnerText;
                                                        }

                                                        string dL_25 = "0";

                                                        if (item["dL25"] != null)
                                                        {
                                                            dL_25 = item["dL25"].InnerText;
                                                        }

                                                        string da_25 = "0";

                                                        if (item["da25"] != null)
                                                        {
                                                            da_25 = item["da25"].InnerText;
                                                        }

                                                        string db_25 = "0";

                                                        if (item["db25"] != null)
                                                        {
                                                            db_25 = item["db25"].InnerText;
                                                        }

                                                        string dL_45 = "0";

                                                        if (item["dL45"] != null)
                                                        {
                                                            dL_45 = item["dL45"].InnerText;
                                                        }

                                                        string da_45 = "0";

                                                        if (item["da45"] != null)
                                                        {
                                                            da_45 = item["da45"].InnerText;
                                                        }

                                                        string db_45 = "0";

                                                        if (item["db45"] != null)
                                                        {
                                                            db_45 = item["db45"].InnerText;
                                                        }

                                                        string dL_75 = "0";

                                                        if (item["dL75"] != null)
                                                        {
                                                            dL_75 = item["dL75"].InnerText;
                                                        }

                                                        string da_75 = "0";

                                                        if (item["da75"] != null)
                                                        {
                                                            da_75 = item["da75"].InnerText;
                                                        }

                                                        string db_75 = "0";

                                                        if (item["db75"] != null)
                                                        {
                                                            db_75 = item["db75"].InnerText;
                                                        }

                                                        string dL_110 = "0";

                                                        if (item["dL110"] != null)
                                                        {
                                                            dL_110 = item["dL110"].InnerText;
                                                        }

                                                        string da_110 = "0";

                                                        if (item["da110"] != null)
                                                        {
                                                            da_110 = item["da110"].InnerText;
                                                        }

                                                        string db_110 = "0";

                                                        if (item["db110"] != null)
                                                        {
                                                            db_110 = item["db110"].InnerText;
                                                        }

                                                        StructMeasurement structMeasurement = new StructMeasurement(index, dE_minus15, dE_15, dE_25, dE_45, dE_75, dE_110, L_minus15, a_minus15, b_minus15, L_15, a_15, b_15, L_25, a_25, b_25, L_45, a_45, b_45, L_75, a_75, b_75, L_110, a_110, b_110, dL_minus15, da_minus15, db_minus15, dL_15, da_15, db_15, dL_25, da_25, db_25, dL_45, da_45, db_45, dL_75, da_75, db_75, dL_110, da_110, db_110);
                                                        StructSensorData structSensorData = new StructSensorData(dateTime, model, color, bodyNumber, structMeasurement);

                                                        if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "FR_FENDA", "1"))
                                                        {
                                                            structReportData.FR_FENDA_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.FR_FENDA_dE_15 = structMeasurement.dE_15;
                                                            structReportData.FR_FENDA_dE_25 = structMeasurement.dE_25;
                                                            structReportData.FR_FENDA_dE_45 = structMeasurement.dE_45;
                                                            structReportData.FR_FENDA_dE_75 = structMeasurement.dE_75;
                                                            structReportData.FR_FENDA_dE_110 = structMeasurement.dE_110;
                                                            structReportData.FR_FENDA_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.FR_FENDA_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.FR_FENDA_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.FR_FENDA_L_15 = structMeasurement.L_15;
                                                            structReportData.FR_FENDA_a_15 = structMeasurement.a_15;
                                                            structReportData.FR_FENDA_b_15 = structMeasurement.b_15;
                                                            structReportData.FR_FENDA_L_25 = structMeasurement.L_25;
                                                            structReportData.FR_FENDA_a_25 = structMeasurement.a_25;
                                                            structReportData.FR_FENDA_b_25 = structMeasurement.b_25;
                                                            structReportData.FR_FENDA_L_45 = structMeasurement.L_45;
                                                            structReportData.FR_FENDA_a_45 = structMeasurement.a_45;
                                                            structReportData.FR_FENDA_b_45 = structMeasurement.b_45;
                                                            structReportData.FR_FENDA_L_75 = structMeasurement.L_75;
                                                            structReportData.FR_FENDA_a_75 = structMeasurement.a_75;
                                                            structReportData.FR_FENDA_b_75 = structMeasurement.b_75;
                                                            structReportData.FR_FENDA_L_110 = structMeasurement.L_110;
                                                            structReportData.FR_FENDA_a_110 = structMeasurement.a_110;
                                                            structReportData.FR_FENDA_b_110 = structMeasurement.b_110;
                                                            structReportData.FR_FENDA_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.FR_FENDA_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.FR_FENDA_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.FR_FENDA_dL_15 = structMeasurement.dL_15;
                                                            structReportData.FR_FENDA_da_15 = structMeasurement.da_15;
                                                            structReportData.FR_FENDA_db_15 = structMeasurement.db_15;
                                                            structReportData.FR_FENDA_dL_25 = structMeasurement.dL_25;
                                                            structReportData.FR_FENDA_da_25 = structMeasurement.da_25;
                                                            structReportData.FR_FENDA_db_25 = structMeasurement.db_25;
                                                            structReportData.FR_FENDA_dL_45 = structMeasurement.dL_45;
                                                            structReportData.FR_FENDA_da_45 = structMeasurement.da_45;
                                                            structReportData.FR_FENDA_db_45 = structMeasurement.db_45;
                                                            structReportData.FR_FENDA_dL_75 = structMeasurement.dL_75;
                                                            structReportData.FR_FENDA_da_75 = structMeasurement.da_75;
                                                            structReportData.FR_FENDA_db_75 = structMeasurement.db_75;
                                                            structReportData.FR_FENDA_dL_110 = structMeasurement.dL_110;
                                                            structReportData.FR_FENDA_da_110 = structMeasurement.da_110;
                                                            structReportData.FR_FENDA_db_110 = structMeasurement.db_110;
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "FR_BUMPER", "2"))
                                                        {
                                                            structReportData.FR_BUMPER_dE_Minus15 = structMeasurement.dE_Minus15;
                                                            structReportData.FR_BUMPER_dE_15 = structMeasurement.dE_15;
                                                            structReportData.FR_BUMPER_dE_25 = structMeasurement.dE_25;
                                                            structReportData.FR_BUMPER_dE_45 = structMeasurement.dE_45;
                                                            structReportData.FR_BUMPER_dE_75 = structMeasurement.dE_75;
                                                            structReportData.FR_BUMPER_dE_110 = structMeasurement.dE_110;
                                                            structReportData.FR_BUMPER_L_Minus15 = structMeasurement.L_Minus15;
                                                            structReportData.FR_BUMPER_a_Minus15 = structMeasurement.a_Minus15;
                                                            structReportData.FR_BUMPER_b_Minus15 = structMeasurement.b_Minus15;
                                                            structReportData.FR_BUMPER_L_15 = structMeasurement.L_15;
                                                            structReportData.FR_BUMPER_a_15 = structMeasurement.a_15;
                                                            structReportData.FR_BUMPER_b_15 = structMeasurement.b_15;
                                                            structReportData.FR_BUMPER_L_25 = structMeasurement.L_25;
                                                            structReportData.FR_BUMPER_a_25 = structMeasurement.a_25;
                                                            structReportData.FR_BUMPER_b_25 = structMeasurement.b_25;
                                                            structReportData.FR_BUMPER_L_45 = structMeasurement.L_45;
                                                            structReportData.FR_BUMPER_a_45 = structMeasurement.a_45;
                                                            structReportData.FR_BUMPER_b_45 = structMeasurement.b_45;
                                                            structReportData.FR_BUMPER_L_75 = structMeasurement.L_75;
                                                            structReportData.FR_BUMPER_a_75 = structMeasurement.a_75;
                                                            structReportData.FR_BUMPER_b_75 = structMeasurement.b_75;
                                                            structReportData.FR_BUMPER_L_110 = structMeasurement.L_110;
                                                            structReportData.FR_BUMPER_a_110 = structMeasurement.a_110;
                                                            structReportData.FR_BUMPER_b_110 = structMeasurement.b_110;
                                                            structReportData.FR_BUMPER_dL_Minus15 = structMeasurement.dL_Minus15;
                                                            structReportData.FR_BUMPER_da_Minus15 = structMeasurement.da_Minus15;
                                                            structReportData.FR_BUMPER_db_Minus15 = structMeasurement.db_Minus15;
                                                            structReportData.FR_BUMPER_dL_15 = structMeasurement.dL_15;
                                                            structReportData.FR_BUMPER_da_15 = structMeasurement.da_15;
                                                            structReportData.FR_BUMPER_db_15 = structMeasurement.db_15;
                                                            structReportData.FR_BUMPER_dL_25 = structMeasurement.dL_25;
                                                            structReportData.FR_BUMPER_da_25 = structMeasurement.da_25;
                                                            structReportData.FR_BUMPER_db_25 = structMeasurement.db_25;
                                                            structReportData.FR_BUMPER_dL_45 = structMeasurement.dL_45;
                                                            structReportData.FR_BUMPER_da_45 = structMeasurement.da_45;
                                                            structReportData.FR_BUMPER_db_45 = structMeasurement.db_45;
                                                            structReportData.FR_BUMPER_dL_75 = structMeasurement.dL_75;
                                                            structReportData.FR_BUMPER_da_75 = structMeasurement.da_75;
                                                            structReportData.FR_BUMPER_db_75 = structMeasurement.db_75;
                                                            structReportData.FR_BUMPER_dL_110 = structMeasurement.dL_110;
                                                            structReportData.FR_BUMPER_da_110 = structMeasurement.da_110;
                                                            structReportData.FR_BUMPER_db_110 = structMeasurement.db_110;
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "RR_QTR", "3"))
                                                        {
                                                            window.Dispatcher.Invoke(() =>
                                                            {
                                                                if (list.Count > 0 && list[find_index].BodyNumber == structSensorData.BodyNumber)
                                                                {
                                                                    list[find_index].RR_QTR_dE_Minus15 = structMeasurement.dE_Minus15;
                                                                    list[find_index].RR_QTR_dE_15 = structMeasurement.dE_15;
                                                                    list[find_index].RR_QTR_dE_25 = structMeasurement.dE_25;
                                                                    list[find_index].RR_QTR_dE_45 = structMeasurement.dE_45;
                                                                    list[find_index].RR_QTR_dE_75 = structMeasurement.dE_75;
                                                                    list[find_index].RR_QTR_dE_110 = structMeasurement.dE_110;
                                                                    list[find_index].RR_QTR_L_Minus15 = structMeasurement.L_Minus15;
                                                                    list[find_index].RR_QTR_a_Minus15 = structMeasurement.a_Minus15;
                                                                    list[find_index].RR_QTR_b_Minus15 = structMeasurement.b_Minus15;
                                                                    list[find_index].RR_QTR_L_15 = structMeasurement.L_15;
                                                                    list[find_index].RR_QTR_a_15 = structMeasurement.a_15;
                                                                    list[find_index].RR_QTR_b_15 = structMeasurement.b_15;
                                                                    list[find_index].RR_QTR_L_25 = structMeasurement.L_25;
                                                                    list[find_index].RR_QTR_a_25 = structMeasurement.a_25;
                                                                    list[find_index].RR_QTR_b_25 = structMeasurement.b_25;
                                                                    list[find_index].RR_QTR_L_45 = structMeasurement.L_45;
                                                                    list[find_index].RR_QTR_a_45 = structMeasurement.a_45;
                                                                    list[find_index].RR_QTR_b_45 = structMeasurement.b_45;
                                                                    list[find_index].RR_QTR_L_75 = structMeasurement.L_75;
                                                                    list[find_index].RR_QTR_a_75 = structMeasurement.a_75;
                                                                    list[find_index].RR_QTR_b_75 = structMeasurement.b_75;
                                                                    list[find_index].RR_QTR_L_110 = structMeasurement.L_110;
                                                                    list[find_index].RR_QTR_a_110 = structMeasurement.a_110;
                                                                    list[find_index].RR_QTR_b_110 = structMeasurement.b_110;
                                                                    list[find_index].RR_QTR_dL_Minus15 = structMeasurement.dL_Minus15;
                                                                    list[find_index].RR_QTR_da_Minus15 = structMeasurement.da_Minus15;
                                                                    list[find_index].RR_QTR_db_Minus15 = structMeasurement.db_Minus15;
                                                                    list[find_index].RR_QTR_dL_15 = structMeasurement.dL_15;
                                                                    list[find_index].RR_QTR_da_15 = structMeasurement.da_15;
                                                                    list[find_index].RR_QTR_db_15 = structMeasurement.db_15;
                                                                    list[find_index].RR_QTR_dL_25 = structMeasurement.dL_25;
                                                                    list[find_index].RR_QTR_da_25 = structMeasurement.da_25;
                                                                    list[find_index].RR_QTR_db_25 = structMeasurement.db_25;
                                                                    list[find_index].RR_QTR_dL_45 = structMeasurement.dL_45;
                                                                    list[find_index].RR_QTR_da_45 = structMeasurement.da_45;
                                                                    list[find_index].RR_QTR_db_45 = structMeasurement.db_45;
                                                                    list[find_index].RR_QTR_dL_75 = structMeasurement.dL_75;
                                                                    list[find_index].RR_QTR_da_75 = structMeasurement.da_75;
                                                                    list[find_index].RR_QTR_db_75 = structMeasurement.db_75;
                                                                    list[find_index].RR_QTR_dL_110 = structMeasurement.dL_110;
                                                                    list[find_index].RR_QTR_da_110 = structMeasurement.da_110;
                                                                    list[find_index].RR_QTR_db_110 = structMeasurement.db_110;

                                                                    isChecked = false;
                                                                }
                                                                else
                                                                {
                                                                    structReportData.RR_QTR_dE_Minus15 = structMeasurement.dE_Minus15;
                                                                    structReportData.RR_QTR_dE_15 = structMeasurement.dE_15;
                                                                    structReportData.RR_QTR_dE_25 = structMeasurement.dE_25;
                                                                    structReportData.RR_QTR_dE_45 = structMeasurement.dE_45;
                                                                    structReportData.RR_QTR_dE_75 = structMeasurement.dE_75;
                                                                    structReportData.RR_QTR_dE_110 = structMeasurement.dE_110;
                                                                    structReportData.RR_QTR_L_Minus15 = structMeasurement.L_Minus15;
                                                                    structReportData.RR_QTR_a_Minus15 = structMeasurement.a_Minus15;
                                                                    structReportData.RR_QTR_b_Minus15 = structMeasurement.b_Minus15;
                                                                    structReportData.RR_QTR_L_15 = structMeasurement.L_15;
                                                                    structReportData.RR_QTR_a_15 = structMeasurement.a_15;
                                                                    structReportData.RR_QTR_b_15 = structMeasurement.b_15;
                                                                    structReportData.RR_QTR_L_25 = structMeasurement.L_25;
                                                                    structReportData.RR_QTR_a_25 = structMeasurement.a_25;
                                                                    structReportData.RR_QTR_b_25 = structMeasurement.b_25;
                                                                    structReportData.RR_QTR_L_45 = structMeasurement.L_45;
                                                                    structReportData.RR_QTR_a_45 = structMeasurement.a_45;
                                                                    structReportData.RR_QTR_b_45 = structMeasurement.b_45;
                                                                    structReportData.RR_QTR_L_75 = structMeasurement.L_75;
                                                                    structReportData.RR_QTR_a_75 = structMeasurement.a_75;
                                                                    structReportData.RR_QTR_b_75 = structMeasurement.b_75;
                                                                    structReportData.RR_QTR_L_110 = structMeasurement.L_110;
                                                                    structReportData.RR_QTR_a_110 = structMeasurement.a_110;
                                                                    structReportData.RR_QTR_b_110 = structMeasurement.b_110;
                                                                    structReportData.RR_QTR_dL_Minus15 = structMeasurement.dL_Minus15;
                                                                    structReportData.RR_QTR_da_Minus15 = structMeasurement.da_Minus15;
                                                                    structReportData.RR_QTR_db_Minus15 = structMeasurement.db_Minus15;
                                                                    structReportData.RR_QTR_dL_15 = structMeasurement.dL_15;
                                                                    structReportData.RR_QTR_da_15 = structMeasurement.da_15;
                                                                    structReportData.RR_QTR_db_15 = structMeasurement.db_15;
                                                                    structReportData.RR_QTR_dL_25 = structMeasurement.dL_25;
                                                                    structReportData.RR_QTR_da_25 = structMeasurement.da_25;
                                                                    structReportData.RR_QTR_db_25 = structMeasurement.db_25;
                                                                    structReportData.RR_QTR_dL_45 = structMeasurement.dL_45;
                                                                    structReportData.RR_QTR_da_45 = structMeasurement.da_45;
                                                                    structReportData.RR_QTR_db_45 = structMeasurement.db_45;
                                                                    structReportData.RR_QTR_dL_75 = structMeasurement.dL_75;
                                                                    structReportData.RR_QTR_da_75 = structMeasurement.da_75;
                                                                    structReportData.RR_QTR_db_75 = structMeasurement.db_75;
                                                                    structReportData.RR_QTR_dL_110 = structMeasurement.dL_110;
                                                                    structReportData.RR_QTR_da_110 = structMeasurement.da_110;
                                                                    structReportData.RR_QTR_db_110 = structMeasurement.db_110;
                                                                }
                                                            });
                                                        }
                                                        else if (structMeasurement.CheckZone == m_Config.GetString("CheckZone", "RR_BUMPER", "4"))
                                                        {
                                                            window.Dispatcher.Invoke(() =>
                                                            {
                                                                if (list.Count > 0 && list[find_index].BodyNumber == structSensorData.BodyNumber)
                                                                {
                                                                    list[find_index].RR_BUMPER_dE_Minus15 = structMeasurement.dE_Minus15;
                                                                    list[find_index].RR_BUMPER_dE_15 = structMeasurement.dE_15;
                                                                    list[find_index].RR_BUMPER_dE_25 = structMeasurement.dE_25;
                                                                    list[find_index].RR_BUMPER_dE_45 = structMeasurement.dE_45;
                                                                    list[find_index].RR_BUMPER_dE_75 = structMeasurement.dE_75;
                                                                    list[find_index].RR_BUMPER_dE_110 = structMeasurement.dE_110;
                                                                    list[find_index].RR_BUMPER_L_Minus15 = structMeasurement.L_Minus15;
                                                                    list[find_index].RR_BUMPER_a_Minus15 = structMeasurement.a_Minus15;
                                                                    list[find_index].RR_BUMPER_b_Minus15 = structMeasurement.b_Minus15;
                                                                    list[find_index].RR_BUMPER_L_15 = structMeasurement.L_15;
                                                                    list[find_index].RR_BUMPER_a_15 = structMeasurement.a_15;
                                                                    list[find_index].RR_BUMPER_b_15 = structMeasurement.b_15;
                                                                    list[find_index].RR_BUMPER_L_25 = structMeasurement.L_25;
                                                                    list[find_index].RR_BUMPER_a_25 = structMeasurement.a_25;
                                                                    list[find_index].RR_BUMPER_b_25 = structMeasurement.b_25;
                                                                    list[find_index].RR_BUMPER_L_45 = structMeasurement.L_45;
                                                                    list[find_index].RR_BUMPER_a_45 = structMeasurement.a_45;
                                                                    list[find_index].RR_BUMPER_b_45 = structMeasurement.b_45;
                                                                    list[find_index].RR_BUMPER_L_75 = structMeasurement.L_75;
                                                                    list[find_index].RR_BUMPER_a_75 = structMeasurement.a_75;
                                                                    list[find_index].RR_BUMPER_b_75 = structMeasurement.b_75;
                                                                    list[find_index].RR_BUMPER_L_110 = structMeasurement.L_110;
                                                                    list[find_index].RR_BUMPER_a_110 = structMeasurement.a_110;
                                                                    list[find_index].RR_BUMPER_b_110 = structMeasurement.b_110;
                                                                    list[find_index].RR_BUMPER_dL_Minus15 = structMeasurement.dL_Minus15;
                                                                    list[find_index].RR_BUMPER_da_Minus15 = structMeasurement.da_Minus15;
                                                                    list[find_index].RR_BUMPER_db_Minus15 = structMeasurement.db_Minus15;
                                                                    list[find_index].RR_BUMPER_dL_15 = structMeasurement.dL_15;
                                                                    list[find_index].RR_BUMPER_da_15 = structMeasurement.da_15;
                                                                    list[find_index].RR_BUMPER_db_15 = structMeasurement.db_15;
                                                                    list[find_index].RR_BUMPER_dL_25 = structMeasurement.dL_25;
                                                                    list[find_index].RR_BUMPER_da_25 = structMeasurement.da_25;
                                                                    list[find_index].RR_BUMPER_db_25 = structMeasurement.db_25;
                                                                    list[find_index].RR_BUMPER_dL_45 = structMeasurement.dL_45;
                                                                    list[find_index].RR_BUMPER_da_45 = structMeasurement.da_45;
                                                                    list[find_index].RR_BUMPER_db_45 = structMeasurement.db_45;
                                                                    list[find_index].RR_BUMPER_dL_75 = structMeasurement.dL_75;
                                                                    list[find_index].RR_BUMPER_da_75 = structMeasurement.da_75;
                                                                    list[find_index].RR_BUMPER_db_75 = structMeasurement.db_75;
                                                                    list[find_index].RR_BUMPER_dL_110 = structMeasurement.dL_110;
                                                                    list[find_index].RR_BUMPER_da_110 = structMeasurement.da_110;
                                                                    list[find_index].RR_BUMPER_db_110 = structMeasurement.db_110;

                                                                    isChecked = false;
                                                                }
                                                                else
                                                                {
                                                                    structReportData.RR_BUMPER_dE_Minus15 = structMeasurement.dE_Minus15;
                                                                    structReportData.RR_BUMPER_dE_15 = structMeasurement.dE_15;
                                                                    structReportData.RR_BUMPER_dE_25 = structMeasurement.dE_25;
                                                                    structReportData.RR_BUMPER_dE_45 = structMeasurement.dE_45;
                                                                    structReportData.RR_BUMPER_dE_75 = structMeasurement.dE_75;
                                                                    structReportData.RR_BUMPER_dE_110 = structMeasurement.dE_110;
                                                                    structReportData.RR_BUMPER_L_Minus15 = structMeasurement.L_Minus15;
                                                                    structReportData.RR_BUMPER_a_Minus15 = structMeasurement.a_Minus15;
                                                                    structReportData.RR_BUMPER_b_Minus15 = structMeasurement.b_Minus15;
                                                                    structReportData.RR_BUMPER_L_15 = structMeasurement.L_15;
                                                                    structReportData.RR_BUMPER_a_15 = structMeasurement.a_15;
                                                                    structReportData.RR_BUMPER_b_15 = structMeasurement.b_15;
                                                                    structReportData.RR_BUMPER_L_25 = structMeasurement.L_25;
                                                                    structReportData.RR_BUMPER_a_25 = structMeasurement.a_25;
                                                                    structReportData.RR_BUMPER_b_25 = structMeasurement.b_25;
                                                                    structReportData.RR_BUMPER_L_45 = structMeasurement.L_45;
                                                                    structReportData.RR_BUMPER_a_45 = structMeasurement.a_45;
                                                                    structReportData.RR_BUMPER_b_45 = structMeasurement.b_45;
                                                                    structReportData.RR_BUMPER_L_75 = structMeasurement.L_75;
                                                                    structReportData.RR_BUMPER_a_75 = structMeasurement.a_75;
                                                                    structReportData.RR_BUMPER_b_75 = structMeasurement.b_75;
                                                                    structReportData.RR_BUMPER_L_110 = structMeasurement.L_110;
                                                                    structReportData.RR_BUMPER_a_110 = structMeasurement.a_110;
                                                                    structReportData.RR_BUMPER_b_110 = structMeasurement.b_110;
                                                                    structReportData.RR_BUMPER_dL_Minus15 = structMeasurement.dL_Minus15;
                                                                    structReportData.RR_BUMPER_da_Minus15 = structMeasurement.da_Minus15;
                                                                    structReportData.RR_BUMPER_db_Minus15 = structMeasurement.db_Minus15;
                                                                    structReportData.RR_BUMPER_dL_15 = structMeasurement.dL_15;
                                                                    structReportData.RR_BUMPER_da_15 = structMeasurement.da_15;
                                                                    structReportData.RR_BUMPER_db_15 = structMeasurement.db_15;
                                                                    structReportData.RR_BUMPER_dL_25 = structMeasurement.dL_25;
                                                                    structReportData.RR_BUMPER_da_25 = structMeasurement.da_25;
                                                                    structReportData.RR_BUMPER_db_25 = structMeasurement.db_25;
                                                                    structReportData.RR_BUMPER_dL_45 = structMeasurement.dL_45;
                                                                    structReportData.RR_BUMPER_da_45 = structMeasurement.da_45;
                                                                    structReportData.RR_BUMPER_db_45 = structMeasurement.db_45;
                                                                    structReportData.RR_BUMPER_dL_75 = structMeasurement.dL_75;
                                                                    structReportData.RR_BUMPER_da_75 = structMeasurement.da_75;
                                                                    structReportData.RR_BUMPER_db_75 = structMeasurement.db_75;
                                                                    structReportData.RR_BUMPER_dL_110 = structMeasurement.dL_110;
                                                                    structReportData.RR_BUMPER_da_110 = structMeasurement.da_110;
                                                                    structReportData.RR_BUMPER_db_110 = structMeasurement.db_110;
                                                                }
                                                            });
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    logManager.Error("" + ex.Message);
                                                }
                                            }
                                        }

                                        if (isChecked)
                                        {
                                            DateTime tmpStartDateTime = new DateTime(this.startDateTime.Year, this.startDateTime.Month, this.startDateTime.Day, startHour, startMinute, 0);
                                            DateTime tmpEndDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endHour, endMinute, 59);

                                            if ((selectedCarKind == "ALL" || structReportData.Model.Contains(selectedCarKind)) && (selectedColor == "ALL" || structReportData.Color.Contains(selectedColor)) && structReportData.BodyNumber.ToUpper().Contains(searchString.ToUpper()))
                                            {
                                                if (tmpStartDateTime < structReportData.StartDateTime && tmpEndDateTime > structReportData.StartDateTime)
                                                {
                                                    double front_delta = 0;
                                                    double rear_delta = 0;

                                                    try
                                                    {
                                                        front_delta = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_b_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_b_45));

                                                        rear_delta = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_b_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_b_45));

                                                        structReportData.FR_DELTA = Convert.ToString(Math.Round(front_delta, 2));
                                                        structReportData.RR_DELTA = Convert.ToString(Math.Round(rear_delta, 2));
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        structReportData.FR_DELTA = "0";
                                                        structReportData.RR_DELTA = "0";
                                                    }


                                                    StructPassRange structPassRange = new StructPassRange();
                                                    List<StructPassRange> passRangeList = LoadPassRangeData();
                                                    StructPassRange passRange = passRangeList.Find(x => x.Color.Trim() == structReportData.Color.Trim());

                                                    if (passRange == null)
                                                    {
                                                        string content = File.ReadAllText(passRangeFilePath);
                                                        content += structReportData.Color.Trim() + "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0," + Environment.NewLine;
                                                        File.WriteAllText(passRangeFilePath, content);
                                                        passRangeList = LoadPassRangeData();
                                                        passRange = passRangeList.Find(x => x.Color.Trim() == structReportData.Color.Trim());
                                                    }


                                                    if (FrontSymmetric)
                                                    {
                                                        if (structReportData.FR_FENDA_dE_Minus15 != null && structReportData.FR_FENDA_dE_15 != null && structReportData.FR_FENDA_dE_25 != null && structReportData.FR_FENDA_dE_45 != null && structReportData.FR_FENDA_dE_75 != null && structReportData.FR_FENDA_dE_110 != null && structReportData.FR_FENDA_dL_Minus15 != null && structReportData.FR_FENDA_dL_15 != null && structReportData.FR_FENDA_dL_25 != null && structReportData.FR_FENDA_dL_45 != null && structReportData.FR_FENDA_dL_75 != null && structReportData.FR_FENDA_dL_110 != null && structReportData.FR_FENDA_da_Minus15 != null && structReportData.FR_FENDA_da_15 != null && structReportData.FR_FENDA_da_25 != null && structReportData.FR_FENDA_da_45 != null && structReportData.FR_FENDA_da_75 != null && structReportData.FR_FENDA_da_110 != null && structReportData.FR_FENDA_db_Minus15 != null && structReportData.FR_FENDA_db_15 != null && structReportData.FR_FENDA_db_25 != null && structReportData.FR_FENDA_db_45 != null && structReportData.FR_FENDA_db_75 != null && structReportData.FR_FENDA_db_110 != null && structReportData.FR_FENDA_L_Minus15 != null && structReportData.FR_FENDA_L_15 != null && structReportData.FR_FENDA_L_25 != null && structReportData.FR_FENDA_L_45 != null && structReportData.FR_FENDA_L_75 != null && structReportData.FR_FENDA_L_110 != null && structReportData.FR_FENDA_a_Minus15 != null && structReportData.FR_FENDA_a_15 != null && structReportData.FR_FENDA_a_25 != null && structReportData.FR_FENDA_a_45 != null && structReportData.FR_FENDA_a_75 != null && structReportData.FR_FENDA_a_110 != null && structReportData.FR_FENDA_b_Minus15 != null && structReportData.FR_FENDA_b_15 != null && structReportData.FR_FENDA_b_25 != null && structReportData.FR_FENDA_b_45 != null && structReportData.FR_FENDA_b_75 != null && structReportData.FR_FENDA_b_110 != null && structReportData.FR_BUMPER_dE_Minus15 != null && structReportData.FR_BUMPER_dE_15 != null && structReportData.FR_BUMPER_dE_25 != null && structReportData.FR_BUMPER_dE_45 != null && structReportData.FR_BUMPER_dE_75 != null && structReportData.FR_BUMPER_dE_110 != null && structReportData.FR_BUMPER_dL_Minus15 != null && structReportData.FR_BUMPER_dL_15 != null && structReportData.FR_BUMPER_dL_25 != null && structReportData.FR_BUMPER_dL_45 != null && structReportData.FR_BUMPER_dL_75 != null && structReportData.FR_BUMPER_dL_110 != null && structReportData.FR_BUMPER_da_Minus15 != null && structReportData.FR_BUMPER_da_15 != null && structReportData.FR_BUMPER_da_25 != null && structReportData.FR_BUMPER_da_45 != null && structReportData.FR_BUMPER_da_75 != null && structReportData.FR_BUMPER_da_110 != null && structReportData.FR_BUMPER_db_Minus15 != null && structReportData.FR_BUMPER_db_15 != null && structReportData.FR_BUMPER_db_25 != null && structReportData.FR_BUMPER_db_45 != null && structReportData.FR_BUMPER_db_75 != null && structReportData.FR_BUMPER_db_110 != null && structReportData.FR_BUMPER_L_Minus15 != null && structReportData.FR_BUMPER_L_15 != null && structReportData.FR_BUMPER_L_25 != null && structReportData.FR_BUMPER_L_45 != null && structReportData.FR_BUMPER_L_75 != null && structReportData.FR_BUMPER_L_110 != null && structReportData.FR_BUMPER_a_Minus15 != null && structReportData.FR_BUMPER_a_15 != null && structReportData.FR_BUMPER_a_25 != null && structReportData.FR_BUMPER_a_45 != null && structReportData.FR_BUMPER_a_75 != null && structReportData.FR_BUMPER_a_110 != null && structReportData.FR_BUMPER_b_Minus15 != null && structReportData.FR_BUMPER_b_15 != null && structReportData.FR_BUMPER_b_25 != null && structReportData.FR_BUMPER_b_45 != null && structReportData.FR_BUMPER_b_75 != null && structReportData.FR_BUMPER_b_110 != null && structReportData.FR_FENDA_dE_Minus15 != "" && structReportData.FR_FENDA_dE_15 != "" && structReportData.FR_FENDA_dE_25 != "" && structReportData.FR_FENDA_dE_45 != "" && structReportData.FR_FENDA_dE_75 != "" && structReportData.FR_FENDA_dE_110 != "" && structReportData.FR_FENDA_dL_Minus15 != "" && structReportData.FR_FENDA_dL_15 != "" && structReportData.FR_FENDA_dL_25 != "" && structReportData.FR_FENDA_dL_45 != "" && structReportData.FR_FENDA_dL_75 != "" && structReportData.FR_FENDA_dL_110 != "" && structReportData.FR_FENDA_da_Minus15 != "" && structReportData.FR_FENDA_da_15 != "" && structReportData.FR_FENDA_da_25 != "" && structReportData.FR_FENDA_da_45 != "" && structReportData.FR_FENDA_da_75 != "" && structReportData.FR_FENDA_da_110 != "" && structReportData.FR_FENDA_db_Minus15 != "" && structReportData.FR_FENDA_db_15 != "" && structReportData.FR_FENDA_db_25 != "" && structReportData.FR_FENDA_db_45 != "" && structReportData.FR_FENDA_db_75 != "" && structReportData.FR_FENDA_db_110 != "" && structReportData.FR_FENDA_L_Minus15 != "" && structReportData.FR_FENDA_L_15 != "" && structReportData.FR_FENDA_L_25 != "" && structReportData.FR_FENDA_L_45 != "" && structReportData.FR_FENDA_L_75 != "" && structReportData.FR_FENDA_L_110 != "" && structReportData.FR_FENDA_a_Minus15 != "" && structReportData.FR_FENDA_a_15 != "" && structReportData.FR_FENDA_a_25 != "" && structReportData.FR_FENDA_a_45 != "" && structReportData.FR_FENDA_a_75 != "" && structReportData.FR_FENDA_a_110 != "" && structReportData.FR_FENDA_b_Minus15 != "" && structReportData.FR_FENDA_b_15 != "" && structReportData.FR_FENDA_b_25 != "" && structReportData.FR_FENDA_b_45 != "" && structReportData.FR_FENDA_b_75 != "" && structReportData.FR_FENDA_b_110 != "" && structReportData.FR_BUMPER_dE_Minus15 != "" && structReportData.FR_BUMPER_dE_15 != "" && structReportData.FR_BUMPER_dE_25 != "" && structReportData.FR_BUMPER_dE_45 != "" && structReportData.FR_BUMPER_dE_75 != "" && structReportData.FR_BUMPER_dE_110 != "" && structReportData.FR_BUMPER_dL_Minus15 != "" && structReportData.FR_BUMPER_dL_15 != "" && structReportData.FR_BUMPER_dL_25 != "" && structReportData.FR_BUMPER_dL_45 != "" && structReportData.FR_BUMPER_dL_75 != "" && structReportData.FR_BUMPER_dL_110 != "" && structReportData.FR_BUMPER_da_Minus15 != "" && structReportData.FR_BUMPER_da_15 != "" && structReportData.FR_BUMPER_da_25 != "" && structReportData.FR_BUMPER_da_45 != "" && structReportData.FR_BUMPER_da_75 != "" && structReportData.FR_BUMPER_da_110 != "" && structReportData.FR_BUMPER_db_Minus15 != "" && structReportData.FR_BUMPER_db_15 != "" && structReportData.FR_BUMPER_db_25 != "" && structReportData.FR_BUMPER_db_45 != "" && structReportData.FR_BUMPER_db_75 != "" && structReportData.FR_BUMPER_db_110 != "" && structReportData.FR_BUMPER_L_Minus15 != "" && structReportData.FR_BUMPER_L_15 != "" && structReportData.FR_BUMPER_L_25 != "" && structReportData.FR_BUMPER_L_45 != "" && structReportData.FR_BUMPER_L_75 != "" && structReportData.FR_BUMPER_L_110 != "" && structReportData.FR_BUMPER_a_Minus15 != "" && structReportData.FR_BUMPER_a_15 != "" && structReportData.FR_BUMPER_a_25 != "" && structReportData.FR_BUMPER_a_45 != "" && structReportData.FR_BUMPER_a_75 != "" && structReportData.FR_BUMPER_a_110 != "" && structReportData.FR_BUMPER_b_Minus15 != "" && structReportData.FR_BUMPER_b_15 != "" && structReportData.FR_BUMPER_b_25 != "" && structReportData.FR_BUMPER_b_45 != "" && structReportData.FR_BUMPER_b_75 != "" && structReportData.FR_BUMPER_b_110 != "")
                                                        {
                                                            double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_FENDA_a_Minus15), Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15));
                                                            double dE_15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_FENDA_a_15), Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15), Convert.ToDouble(structReportData.FR_BUMPER_a_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15));
                                                            double dE_25 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_FENDA_a_25), Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25), Convert.ToDouble(structReportData.FR_BUMPER_a_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25));
                                                            double dE_45 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45));
                                                            double dE_75 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_FENDA_a_75), Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75), Convert.ToDouble(structReportData.FR_BUMPER_a_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75));
                                                            double dE_110 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_FENDA_a_110), Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110), Convert.ToDouble(structReportData.FR_BUMPER_a_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110));

                                                            if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangeValue &&
                                                                dE_15 > (passRange.Front_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangeValue &&
                                                                dE_25 > (passRange.Front_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangeValue &&
                                                                dE_45 > (passRange.Front_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangeValue &&
                                                                dE_75 > (passRange.Front_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangeValue &&
                                                                dE_110 > (passRange.Front_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangeValue)
                                                            {
                                                                structReportData.FR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.FR_Result = "NG";
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (iniManager.BlankIsOk)
                                                            {
                                                                structReportData.FR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.FR_Result = "NG";
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (structReportData.FR_FENDA_dE_Minus15 != null && structReportData.FR_FENDA_dE_15 != null && structReportData.FR_FENDA_dE_25 != null && structReportData.FR_FENDA_dE_45 != null && structReportData.FR_FENDA_dE_75 != null && structReportData.FR_FENDA_dE_110 != null && structReportData.FR_FENDA_dL_Minus15 != null && structReportData.FR_FENDA_dL_15 != null && structReportData.FR_FENDA_dL_25 != null && structReportData.FR_FENDA_dL_45 != null && structReportData.FR_FENDA_dL_75 != null && structReportData.FR_FENDA_dL_110 != null && structReportData.FR_FENDA_da_Minus15 != null && structReportData.FR_FENDA_da_15 != null && structReportData.FR_FENDA_da_25 != null && structReportData.FR_FENDA_da_45 != null && structReportData.FR_FENDA_da_75 != null && structReportData.FR_FENDA_da_110 != null && structReportData.FR_FENDA_db_Minus15 != null && structReportData.FR_FENDA_db_15 != null && structReportData.FR_FENDA_db_25 != null && structReportData.FR_FENDA_db_45 != null && structReportData.FR_FENDA_db_75 != null && structReportData.FR_FENDA_db_110 != null && structReportData.FR_FENDA_L_Minus15 != null && structReportData.FR_FENDA_L_15 != null && structReportData.FR_FENDA_L_25 != null && structReportData.FR_FENDA_L_45 != null && structReportData.FR_FENDA_L_75 != null && structReportData.FR_FENDA_L_110 != null && structReportData.FR_FENDA_a_Minus15 != null && structReportData.FR_FENDA_a_15 != null && structReportData.FR_FENDA_a_25 != null && structReportData.FR_FENDA_a_45 != null && structReportData.FR_FENDA_a_75 != null && structReportData.FR_FENDA_a_110 != null && structReportData.FR_FENDA_b_Minus15 != null && structReportData.FR_FENDA_b_15 != null && structReportData.FR_FENDA_b_25 != null && structReportData.FR_FENDA_b_45 != null && structReportData.FR_FENDA_b_75 != null && structReportData.FR_FENDA_b_110 != null && structReportData.FR_BUMPER_dE_Minus15 != null && structReportData.FR_BUMPER_dE_15 != null && structReportData.FR_BUMPER_dE_25 != null && structReportData.FR_BUMPER_dE_45 != null && structReportData.FR_BUMPER_dE_75 != null && structReportData.FR_BUMPER_dE_110 != null && structReportData.FR_BUMPER_dL_Minus15 != null && structReportData.FR_BUMPER_dL_15 != null && structReportData.FR_BUMPER_dL_25 != null && structReportData.FR_BUMPER_dL_45 != null && structReportData.FR_BUMPER_dL_75 != null && structReportData.FR_BUMPER_dL_110 != null && structReportData.FR_BUMPER_da_Minus15 != null && structReportData.FR_BUMPER_da_15 != null && structReportData.FR_BUMPER_da_25 != null && structReportData.FR_BUMPER_da_45 != null && structReportData.FR_BUMPER_da_75 != null && structReportData.FR_BUMPER_da_110 != null && structReportData.FR_BUMPER_db_Minus15 != null && structReportData.FR_BUMPER_db_15 != null && structReportData.FR_BUMPER_db_25 != null && structReportData.FR_BUMPER_db_45 != null && structReportData.FR_BUMPER_db_75 != null && structReportData.FR_BUMPER_db_110 != null && structReportData.FR_BUMPER_L_Minus15 != null && structReportData.FR_BUMPER_L_15 != null && structReportData.FR_BUMPER_L_25 != null && structReportData.FR_BUMPER_L_45 != null && structReportData.FR_BUMPER_L_75 != null && structReportData.FR_BUMPER_L_110 != null && structReportData.FR_BUMPER_a_Minus15 != null && structReportData.FR_BUMPER_a_15 != null && structReportData.FR_BUMPER_a_25 != null && structReportData.FR_BUMPER_a_45 != null && structReportData.FR_BUMPER_a_75 != null && structReportData.FR_BUMPER_a_110 != null && structReportData.FR_BUMPER_b_Minus15 != null && structReportData.FR_BUMPER_b_15 != null && structReportData.FR_BUMPER_b_25 != null && structReportData.FR_BUMPER_b_45 != null && structReportData.FR_BUMPER_b_75 != null && structReportData.FR_BUMPER_b_110 != null && structReportData.FR_FENDA_dE_Minus15 != "" && structReportData.FR_FENDA_dE_15 != "" && structReportData.FR_FENDA_dE_25 != "" && structReportData.FR_FENDA_dE_45 != "" && structReportData.FR_FENDA_dE_75 != "" && structReportData.FR_FENDA_dE_110 != "" && structReportData.FR_FENDA_dL_Minus15 != "" && structReportData.FR_FENDA_dL_15 != "" && structReportData.FR_FENDA_dL_25 != "" && structReportData.FR_FENDA_dL_45 != "" && structReportData.FR_FENDA_dL_75 != "" && structReportData.FR_FENDA_dL_110 != "" && structReportData.FR_FENDA_da_Minus15 != "" && structReportData.FR_FENDA_da_15 != "" && structReportData.FR_FENDA_da_25 != "" && structReportData.FR_FENDA_da_45 != "" && structReportData.FR_FENDA_da_75 != "" && structReportData.FR_FENDA_da_110 != "" && structReportData.FR_FENDA_db_Minus15 != "" && structReportData.FR_FENDA_db_15 != "" && structReportData.FR_FENDA_db_25 != "" && structReportData.FR_FENDA_db_45 != "" && structReportData.FR_FENDA_db_75 != "" && structReportData.FR_FENDA_db_110 != "" && structReportData.FR_FENDA_L_Minus15 != "" && structReportData.FR_FENDA_L_15 != "" && structReportData.FR_FENDA_L_25 != "" && structReportData.FR_FENDA_L_45 != "" && structReportData.FR_FENDA_L_75 != "" && structReportData.FR_FENDA_L_110 != "" && structReportData.FR_FENDA_a_Minus15 != "" && structReportData.FR_FENDA_a_15 != "" && structReportData.FR_FENDA_a_25 != "" && structReportData.FR_FENDA_a_45 != "" && structReportData.FR_FENDA_a_75 != "" && structReportData.FR_FENDA_a_110 != "" && structReportData.FR_FENDA_b_Minus15 != "" && structReportData.FR_FENDA_b_15 != "" && structReportData.FR_FENDA_b_25 != "" && structReportData.FR_FENDA_b_45 != "" && structReportData.FR_FENDA_b_75 != "" && structReportData.FR_FENDA_b_110 != "" && structReportData.FR_BUMPER_dE_Minus15 != "" && structReportData.FR_BUMPER_dE_15 != "" && structReportData.FR_BUMPER_dE_25 != "" && structReportData.FR_BUMPER_dE_45 != "" && structReportData.FR_BUMPER_dE_75 != "" && structReportData.FR_BUMPER_dE_110 != "" && structReportData.FR_BUMPER_dL_Minus15 != "" && structReportData.FR_BUMPER_dL_15 != "" && structReportData.FR_BUMPER_dL_25 != "" && structReportData.FR_BUMPER_dL_45 != "" && structReportData.FR_BUMPER_dL_75 != "" && structReportData.FR_BUMPER_dL_110 != "" && structReportData.FR_BUMPER_da_Minus15 != "" && structReportData.FR_BUMPER_da_15 != "" && structReportData.FR_BUMPER_da_25 != "" && structReportData.FR_BUMPER_da_45 != "" && structReportData.FR_BUMPER_da_75 != "" && structReportData.FR_BUMPER_da_110 != "" && structReportData.FR_BUMPER_db_Minus15 != "" && structReportData.FR_BUMPER_db_15 != "" && structReportData.FR_BUMPER_db_25 != "" && structReportData.FR_BUMPER_db_45 != "" && structReportData.FR_BUMPER_db_75 != "" && structReportData.FR_BUMPER_db_110 != "" && structReportData.FR_BUMPER_L_Minus15 != "" && structReportData.FR_BUMPER_L_15 != "" && structReportData.FR_BUMPER_L_25 != "" && structReportData.FR_BUMPER_L_45 != "" && structReportData.FR_BUMPER_L_75 != "" && structReportData.FR_BUMPER_L_110 != "" && structReportData.FR_BUMPER_a_Minus15 != "" && structReportData.FR_BUMPER_a_15 != "" && structReportData.FR_BUMPER_a_25 != "" && structReportData.FR_BUMPER_a_45 != "" && structReportData.FR_BUMPER_a_75 != "" && structReportData.FR_BUMPER_a_110 != "" && structReportData.FR_BUMPER_b_Minus15 != "" && structReportData.FR_BUMPER_b_15 != "" && structReportData.FR_BUMPER_b_25 != "" && structReportData.FR_BUMPER_b_45 != "" && structReportData.FR_BUMPER_b_75 != "" && structReportData.FR_BUMPER_b_110 != "")
                                                        {
                                                            double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_FENDA_a_Minus15), Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15));
                                                            double dE_15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_FENDA_a_15), Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15), Convert.ToDouble(structReportData.FR_BUMPER_a_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15));
                                                            double dE_25 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_FENDA_a_25), Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25), Convert.ToDouble(structReportData.FR_BUMPER_a_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25));
                                                            double dE_45 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45));
                                                            double dE_75 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_FENDA_a_75), Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75), Convert.ToDouble(structReportData.FR_BUMPER_a_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75));
                                                            double dE_110 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_FENDA_a_110), Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110), Convert.ToDouble(structReportData.FR_BUMPER_a_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110));

                                                            if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangePlusValue &&
                                                                dE_15 > (passRange.Front_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangePlusValue &&
                                                                dE_25 > (passRange.Front_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangePlusValue &&
                                                                dE_45 > (passRange.Front_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangePlusValue &&
                                                                dE_75 > (passRange.Front_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangePlusValue &&
                                                                dE_110 > (passRange.Front_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangePlusValue)
                                                            {
                                                                structReportData.FR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.FR_Result = "NG";
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (iniManager.BlankIsOk)
                                                            {
                                                                structReportData.FR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.FR_Result = "NG";
                                                            }
                                                        }
                                                    }

                                                    if (RearSymmetric)
                                                    {
                                                        if (structReportData.RR_QTR_dE_Minus15 != null && structReportData.RR_QTR_dE_15 != null && structReportData.RR_QTR_dE_25 != null && structReportData.RR_QTR_dE_45 != null && structReportData.RR_QTR_dE_75 != null && structReportData.RR_QTR_dE_110 != null && structReportData.RR_QTR_dL_Minus15 != null && structReportData.RR_QTR_dL_15 != null && structReportData.RR_QTR_dL_25 != null && structReportData.RR_QTR_dL_45 != null && structReportData.RR_QTR_dL_75 != null && structReportData.RR_QTR_dL_110 != null && structReportData.RR_QTR_da_Minus15 != null && structReportData.RR_QTR_da_15 != null && structReportData.RR_QTR_da_25 != null && structReportData.RR_QTR_da_45 != null && structReportData.RR_QTR_da_75 != null && structReportData.RR_QTR_da_110 != null && structReportData.RR_QTR_db_Minus15 != null && structReportData.RR_QTR_db_15 != null && structReportData.RR_QTR_db_25 != null && structReportData.RR_QTR_db_45 != null && structReportData.RR_QTR_db_75 != null && structReportData.RR_QTR_db_110 != null && structReportData.RR_QTR_L_Minus15 != null && structReportData.RR_QTR_L_15 != null && structReportData.RR_QTR_L_25 != null && structReportData.RR_QTR_L_45 != null && structReportData.RR_QTR_L_75 != null && structReportData.RR_QTR_L_110 != null && structReportData.RR_QTR_a_Minus15 != null && structReportData.RR_QTR_a_15 != null && structReportData.RR_QTR_a_25 != null && structReportData.RR_QTR_a_45 != null && structReportData.RR_QTR_a_75 != null && structReportData.RR_QTR_a_110 != null && structReportData.RR_QTR_b_Minus15 != null && structReportData.RR_QTR_b_15 != null && structReportData.RR_QTR_b_25 != null && structReportData.RR_QTR_b_45 != null && structReportData.RR_QTR_b_75 != null && structReportData.RR_QTR_b_110 != null && structReportData.RR_BUMPER_dE_Minus15 != null && structReportData.RR_BUMPER_dE_15 != null && structReportData.RR_BUMPER_dE_25 != null && structReportData.RR_BUMPER_dE_45 != null && structReportData.RR_BUMPER_dE_75 != null && structReportData.RR_BUMPER_dE_110 != null && structReportData.RR_BUMPER_dL_Minus15 != null && structReportData.RR_BUMPER_dL_15 != null && structReportData.RR_BUMPER_dL_25 != null && structReportData.RR_BUMPER_dL_45 != null && structReportData.RR_BUMPER_dL_75 != null && structReportData.RR_BUMPER_dL_110 != null && structReportData.RR_BUMPER_da_Minus15 != null && structReportData.RR_BUMPER_da_15 != null && structReportData.RR_BUMPER_da_25 != null && structReportData.RR_BUMPER_da_45 != null && structReportData.RR_BUMPER_da_75 != null && structReportData.RR_BUMPER_da_110 != null && structReportData.RR_BUMPER_db_Minus15 != null && structReportData.RR_BUMPER_db_15 != null && structReportData.RR_BUMPER_db_25 != null && structReportData.RR_BUMPER_db_45 != null && structReportData.RR_BUMPER_db_75 != null && structReportData.RR_BUMPER_db_110 != null && structReportData.RR_BUMPER_L_Minus15 != null && structReportData.RR_BUMPER_L_15 != null && structReportData.RR_BUMPER_L_25 != null && structReportData.RR_BUMPER_L_45 != null && structReportData.RR_BUMPER_L_75 != null && structReportData.RR_BUMPER_L_110 != null && structReportData.RR_BUMPER_a_Minus15 != null && structReportData.RR_BUMPER_a_15 != null && structReportData.RR_BUMPER_a_25 != null && structReportData.RR_BUMPER_a_45 != null && structReportData.RR_BUMPER_a_75 != null && structReportData.RR_BUMPER_a_110 != null && structReportData.RR_BUMPER_b_Minus15 != null && structReportData.RR_BUMPER_b_15 != null && structReportData.RR_BUMPER_b_25 != null && structReportData.RR_BUMPER_b_45 != null && structReportData.RR_BUMPER_b_75 != null && structReportData.RR_BUMPER_b_110 != null && structReportData.RR_QTR_dE_Minus15 != "" && structReportData.RR_QTR_dE_15 != "" && structReportData.RR_QTR_dE_25 != "" && structReportData.RR_QTR_dE_45 != "" && structReportData.RR_QTR_dE_75 != "" && structReportData.RR_QTR_dE_110 != "" && structReportData.RR_QTR_dL_Minus15 != "" && structReportData.RR_QTR_dL_15 != "" && structReportData.RR_QTR_dL_25 != "" && structReportData.RR_QTR_dL_45 != "" && structReportData.RR_QTR_dL_75 != "" && structReportData.RR_QTR_dL_110 != "" && structReportData.RR_QTR_da_Minus15 != "" && structReportData.RR_QTR_da_15 != "" && structReportData.RR_QTR_da_25 != "" && structReportData.RR_QTR_da_45 != "" && structReportData.RR_QTR_da_75 != "" && structReportData.RR_QTR_da_110 != "" && structReportData.RR_QTR_db_Minus15 != "" && structReportData.RR_QTR_db_15 != "" && structReportData.RR_QTR_db_25 != "" && structReportData.RR_QTR_db_45 != "" && structReportData.RR_QTR_db_75 != "" && structReportData.RR_QTR_db_110 != "" && structReportData.RR_QTR_L_Minus15 != "" && structReportData.RR_QTR_L_15 != "" && structReportData.RR_QTR_L_25 != "" && structReportData.RR_QTR_L_45 != "" && structReportData.RR_QTR_L_75 != "" && structReportData.RR_QTR_L_110 != "" && structReportData.RR_QTR_a_Minus15 != "" && structReportData.RR_QTR_a_15 != "" && structReportData.RR_QTR_a_25 != "" && structReportData.RR_QTR_a_45 != "" && structReportData.RR_QTR_a_75 != "" && structReportData.RR_QTR_a_110 != "" && structReportData.RR_QTR_b_Minus15 != "" && structReportData.RR_QTR_b_15 != "" && structReportData.RR_QTR_b_25 != "" && structReportData.RR_QTR_b_45 != "" && structReportData.RR_QTR_b_75 != "" && structReportData.RR_QTR_b_110 != "" && structReportData.RR_BUMPER_dE_Minus15 != "" && structReportData.RR_BUMPER_dE_15 != "" && structReportData.RR_BUMPER_dE_25 != "" && structReportData.RR_BUMPER_dE_45 != "" && structReportData.RR_BUMPER_dE_75 != "" && structReportData.RR_BUMPER_dE_110 != "" && structReportData.RR_BUMPER_dL_Minus15 != "" && structReportData.RR_BUMPER_dL_15 != "" && structReportData.RR_BUMPER_dL_25 != "" && structReportData.RR_BUMPER_dL_45 != "" && structReportData.RR_BUMPER_dL_75 != "" && structReportData.RR_BUMPER_dL_110 != "" && structReportData.RR_BUMPER_da_Minus15 != "" && structReportData.RR_BUMPER_da_15 != "" && structReportData.RR_BUMPER_da_25 != "" && structReportData.RR_BUMPER_da_45 != "" && structReportData.RR_BUMPER_da_75 != "" && structReportData.RR_BUMPER_da_110 != "" && structReportData.RR_BUMPER_db_Minus15 != "" && structReportData.RR_BUMPER_db_15 != "" && structReportData.RR_BUMPER_db_25 != "" && structReportData.RR_BUMPER_db_45 != "" && structReportData.RR_BUMPER_db_75 != "" && structReportData.RR_BUMPER_db_110 != "" && structReportData.RR_BUMPER_L_Minus15 != "" && structReportData.RR_BUMPER_L_15 != "" && structReportData.RR_BUMPER_L_25 != "" && structReportData.RR_BUMPER_L_45 != "" && structReportData.RR_BUMPER_L_75 != "" && structReportData.RR_BUMPER_L_110 != "" && structReportData.RR_BUMPER_a_Minus15 != "" && structReportData.RR_BUMPER_a_15 != "" && structReportData.RR_BUMPER_a_25 != "" && structReportData.RR_BUMPER_a_45 != "" && structReportData.RR_BUMPER_a_75 != "" && structReportData.RR_BUMPER_a_110 != "" && structReportData.RR_BUMPER_b_Minus15 != "" && structReportData.RR_BUMPER_b_15 != "" && structReportData.RR_BUMPER_b_25 != "" && structReportData.RR_BUMPER_b_45 != "" && structReportData.RR_BUMPER_b_75 != "" && structReportData.RR_BUMPER_b_110 != "")
                                                        {
                                                            double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_QTR_a_Minus15), Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15));
                                                            double dE_15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_QTR_a_15), Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15), Convert.ToDouble(structReportData.RR_BUMPER_a_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15));
                                                            double dE_25 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_QTR_a_25), Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25), Convert.ToDouble(structReportData.RR_BUMPER_a_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25));
                                                            double dE_45 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45));
                                                            double dE_75 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_QTR_a_75), Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75), Convert.ToDouble(structReportData.RR_BUMPER_a_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75));
                                                            double dE_110 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_QTR_a_110), Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110), Convert.ToDouble(structReportData.RR_BUMPER_a_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110));

                                                            if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangeValue &&
                                                                dE_15 > (passRange.Rear_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangeValue &&
                                                                dE_25 > (passRange.Rear_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangeValue &&
                                                                dE_45 > (passRange.Rear_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangeValue &&
                                                                dE_75 > (passRange.Rear_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangeValue &&
                                                                dE_110 > (passRange.Rear_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangeValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangeValue)
                                                            {
                                                                structReportData.RR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.RR_Result = "NG";
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (iniManager.BlankIsOk)
                                                            {
                                                                structReportData.RR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.RR_Result = "NG";
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (structReportData.RR_QTR_dE_Minus15 != null && structReportData.RR_QTR_dE_15 != null && structReportData.RR_QTR_dE_25 != null && structReportData.RR_QTR_dE_45 != null && structReportData.RR_QTR_dE_75 != null && structReportData.RR_QTR_dE_110 != null && structReportData.RR_QTR_dL_Minus15 != null && structReportData.RR_QTR_dL_15 != null && structReportData.RR_QTR_dL_25 != null && structReportData.RR_QTR_dL_45 != null && structReportData.RR_QTR_dL_75 != null && structReportData.RR_QTR_dL_110 != null && structReportData.RR_QTR_da_Minus15 != null && structReportData.RR_QTR_da_15 != null && structReportData.RR_QTR_da_25 != null && structReportData.RR_QTR_da_45 != null && structReportData.RR_QTR_da_75 != null && structReportData.RR_QTR_da_110 != null && structReportData.RR_QTR_db_Minus15 != null && structReportData.RR_QTR_db_15 != null && structReportData.RR_QTR_db_25 != null && structReportData.RR_QTR_db_45 != null && structReportData.RR_QTR_db_75 != null && structReportData.RR_QTR_db_110 != null && structReportData.RR_QTR_L_Minus15 != null && structReportData.RR_QTR_L_15 != null && structReportData.RR_QTR_L_25 != null && structReportData.RR_QTR_L_45 != null && structReportData.RR_QTR_L_75 != null && structReportData.RR_QTR_L_110 != null && structReportData.RR_QTR_a_Minus15 != null && structReportData.RR_QTR_a_15 != null && structReportData.RR_QTR_a_25 != null && structReportData.RR_QTR_a_45 != null && structReportData.RR_QTR_a_75 != null && structReportData.RR_QTR_a_110 != null && structReportData.RR_QTR_b_Minus15 != null && structReportData.RR_QTR_b_15 != null && structReportData.RR_QTR_b_25 != null && structReportData.RR_QTR_b_45 != null && structReportData.RR_QTR_b_75 != null && structReportData.RR_QTR_b_110 != null && structReportData.RR_BUMPER_dE_Minus15 != null && structReportData.RR_BUMPER_dE_15 != null && structReportData.RR_BUMPER_dE_25 != null && structReportData.RR_BUMPER_dE_45 != null && structReportData.RR_BUMPER_dE_75 != null && structReportData.RR_BUMPER_dE_110 != null && structReportData.RR_BUMPER_dL_Minus15 != null && structReportData.RR_BUMPER_dL_15 != null && structReportData.RR_BUMPER_dL_25 != null && structReportData.RR_BUMPER_dL_45 != null && structReportData.RR_BUMPER_dL_75 != null && structReportData.RR_BUMPER_dL_110 != null && structReportData.RR_BUMPER_da_Minus15 != null && structReportData.RR_BUMPER_da_15 != null && structReportData.RR_BUMPER_da_25 != null && structReportData.RR_BUMPER_da_45 != null && structReportData.RR_BUMPER_da_75 != null && structReportData.RR_BUMPER_da_110 != null && structReportData.RR_BUMPER_db_Minus15 != null && structReportData.RR_BUMPER_db_15 != null && structReportData.RR_BUMPER_db_25 != null && structReportData.RR_BUMPER_db_45 != null && structReportData.RR_BUMPER_db_75 != null && structReportData.RR_BUMPER_db_110 != null && structReportData.RR_BUMPER_L_Minus15 != null && structReportData.RR_BUMPER_L_15 != null && structReportData.RR_BUMPER_L_25 != null && structReportData.RR_BUMPER_L_45 != null && structReportData.RR_BUMPER_L_75 != null && structReportData.RR_BUMPER_L_110 != null && structReportData.RR_BUMPER_a_Minus15 != null && structReportData.RR_BUMPER_a_15 != null && structReportData.RR_BUMPER_a_25 != null && structReportData.RR_BUMPER_a_45 != null && structReportData.RR_BUMPER_a_75 != null && structReportData.RR_BUMPER_a_110 != null && structReportData.RR_BUMPER_b_Minus15 != null && structReportData.RR_BUMPER_b_15 != null && structReportData.RR_BUMPER_b_25 != null && structReportData.RR_BUMPER_b_45 != null && structReportData.RR_BUMPER_b_75 != null && structReportData.RR_BUMPER_b_110 != null && structReportData.RR_QTR_dE_Minus15 != "" && structReportData.RR_QTR_dE_15 != "" && structReportData.RR_QTR_dE_25 != "" && structReportData.RR_QTR_dE_45 != "" && structReportData.RR_QTR_dE_75 != "" && structReportData.RR_QTR_dE_110 != "" && structReportData.RR_QTR_dL_Minus15 != "" && structReportData.RR_QTR_dL_15 != "" && structReportData.RR_QTR_dL_25 != "" && structReportData.RR_QTR_dL_45 != "" && structReportData.RR_QTR_dL_75 != "" && structReportData.RR_QTR_dL_110 != "" && structReportData.RR_QTR_da_Minus15 != "" && structReportData.RR_QTR_da_15 != "" && structReportData.RR_QTR_da_25 != "" && structReportData.RR_QTR_da_45 != "" && structReportData.RR_QTR_da_75 != "" && structReportData.RR_QTR_da_110 != "" && structReportData.RR_QTR_db_Minus15 != "" && structReportData.RR_QTR_db_15 != "" && structReportData.RR_QTR_db_25 != "" && structReportData.RR_QTR_db_45 != "" && structReportData.RR_QTR_db_75 != "" && structReportData.RR_QTR_db_110 != "" && structReportData.RR_QTR_L_Minus15 != "" && structReportData.RR_QTR_L_15 != "" && structReportData.RR_QTR_L_25 != "" && structReportData.RR_QTR_L_45 != "" && structReportData.RR_QTR_L_75 != "" && structReportData.RR_QTR_L_110 != "" && structReportData.RR_QTR_a_Minus15 != "" && structReportData.RR_QTR_a_15 != "" && structReportData.RR_QTR_a_25 != "" && structReportData.RR_QTR_a_45 != "" && structReportData.RR_QTR_a_75 != "" && structReportData.RR_QTR_a_110 != "" && structReportData.RR_QTR_b_Minus15 != "" && structReportData.RR_QTR_b_15 != "" && structReportData.RR_QTR_b_25 != "" && structReportData.RR_QTR_b_45 != "" && structReportData.RR_QTR_b_75 != "" && structReportData.RR_QTR_b_110 != "" && structReportData.RR_BUMPER_dE_Minus15 != "" && structReportData.RR_BUMPER_dE_15 != "" && structReportData.RR_BUMPER_dE_25 != "" && structReportData.RR_BUMPER_dE_45 != "" && structReportData.RR_BUMPER_dE_75 != "" && structReportData.RR_BUMPER_dE_110 != "" && structReportData.RR_BUMPER_dL_Minus15 != "" && structReportData.RR_BUMPER_dL_15 != "" && structReportData.RR_BUMPER_dL_25 != "" && structReportData.RR_BUMPER_dL_45 != "" && structReportData.RR_BUMPER_dL_75 != "" && structReportData.RR_BUMPER_dL_110 != "" && structReportData.RR_BUMPER_da_Minus15 != "" && structReportData.RR_BUMPER_da_15 != "" && structReportData.RR_BUMPER_da_25 != "" && structReportData.RR_BUMPER_da_45 != "" && structReportData.RR_BUMPER_da_75 != "" && structReportData.RR_BUMPER_da_110 != "" && structReportData.RR_BUMPER_db_Minus15 != "" && structReportData.RR_BUMPER_db_15 != "" && structReportData.RR_BUMPER_db_25 != "" && structReportData.RR_BUMPER_db_45 != "" && structReportData.RR_BUMPER_db_75 != "" && structReportData.RR_BUMPER_db_110 != "" && structReportData.RR_BUMPER_L_Minus15 != "" && structReportData.RR_BUMPER_L_15 != "" && structReportData.RR_BUMPER_L_25 != "" && structReportData.RR_BUMPER_L_45 != "" && structReportData.RR_BUMPER_L_75 != "" && structReportData.RR_BUMPER_L_110 != "" && structReportData.RR_BUMPER_a_Minus15 != "" && structReportData.RR_BUMPER_a_15 != "" && structReportData.RR_BUMPER_a_25 != "" && structReportData.RR_BUMPER_a_45 != "" && structReportData.RR_BUMPER_a_75 != "" && structReportData.RR_BUMPER_a_110 != "" && structReportData.RR_BUMPER_b_Minus15 != "" && structReportData.RR_BUMPER_b_15 != "" && structReportData.RR_BUMPER_b_25 != "" && structReportData.RR_BUMPER_b_45 != "" && structReportData.RR_BUMPER_b_75 != "" && structReportData.RR_BUMPER_b_110 != "")
                                                        {
                                                            double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_QTR_a_Minus15), Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15));
                                                            double dE_15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_QTR_a_15), Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15), Convert.ToDouble(structReportData.RR_BUMPER_a_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15));
                                                            double dE_25 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_QTR_a_25), Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25), Convert.ToDouble(structReportData.RR_BUMPER_a_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25));
                                                            double dE_45 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45));
                                                            double dE_75 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_QTR_a_75), Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75), Convert.ToDouble(structReportData.RR_BUMPER_a_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75));
                                                            double dE_110 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_QTR_a_110), Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110), Convert.ToDouble(structReportData.RR_BUMPER_a_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110));

                                                            if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangePlusValue &&
                                                                dE_15 > (passRange.Rear_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangePlusValue &&
                                                                dE_25 > (passRange.Rear_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangePlusValue &&
                                                                dE_45 > (passRange.Rear_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangePlusValue &&
                                                                dE_75 > (passRange.Rear_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangePlusValue &&
                                                                dE_110 > (passRange.Rear_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangePlusValue &&
                                                                (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangePlusValue)
                                                            {
                                                                structReportData.RR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.RR_Result = "NG";
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (iniManager.BlankIsOk)
                                                            {
                                                                structReportData.RR_Result = "OK";
                                                            }
                                                            else
                                                            {
                                                                structReportData.RR_Result = "NG";
                                                            }
                                                        }
                                                    }

                                                    if (structReportData.FR_Result == "OK" && structReportData.RR_Result == "OK")
                                                    {
                                                        structReportData.Result = "OK";
                                                    }
                                                    else if (structReportData.FR_Result == "NG" || structReportData.RR_Result == "NG")
                                                    {
                                                        structReportData.Result = "NG";
                                                    }

                                                    window.Dispatcher.Invoke(() =>
                                                    {
                                                        list.Add(structReportData);
                                                    });
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (list.Count > 0 && list[find_index].BodyNumber == structReportData.BodyNumber)
                                            {
                                                DateTime tmpStartDateTime = new DateTime(this.startDateTime.Year, this.startDateTime.Month, this.startDateTime.Day, startHour, startMinute, 0);
                                                DateTime tmpEndDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endHour, endMinute, 59);

                                                if ((selectedCarKind == "ALL" || list[find_index].Model.Contains(selectedCarKind)) && (selectedColor == "ALL" || list[find_index].Color.Contains(selectedColor)) && list[find_index].BodyNumber.ToUpper().Contains(searchString.ToUpper()))
                                                {
                                                    if (tmpStartDateTime < list[find_index].StartDateTime && tmpEndDateTime > list[find_index].StartDateTime)
                                                    {
                                                        double front_delta = 0;
                                                        double rear_delta = 0;

                                                        try
                                                        {
                                                            front_delta = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_FENDA_a_45), Convert.ToDouble(list[find_index].FR_FENDA_b_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_a_45), Convert.ToDouble(list[find_index].FR_BUMPER_b_45));

                                                            rear_delta = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_QTR_a_45), Convert.ToDouble(list[find_index].RR_QTR_b_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_a_45), Convert.ToDouble(list[find_index].RR_BUMPER_b_45));

                                                            list[find_index].FR_DELTA = Convert.ToString(Math.Round(front_delta, 2));
                                                            list[find_index].RR_DELTA = Convert.ToString(Math.Round(rear_delta, 2));
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            list[find_index].FR_DELTA = "0";
                                                            list[find_index].RR_DELTA = "0";
                                                        }


                                                        StructPassRange structPassRange = new StructPassRange();
                                                        List<StructPassRange> passRangeList = LoadPassRangeData();
                                                        StructPassRange passRange = passRangeList.Find(x => x.Color.Trim() == list[find_index].Color.Trim());

                                                        if (passRange == null)
                                                        {
                                                            string content = File.ReadAllText(passRangeFilePath);
                                                            content += list[find_index].Color.Trim() + "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0," + Environment.NewLine;
                                                            File.WriteAllText(passRangeFilePath, content);
                                                            passRangeList = LoadPassRangeData();
                                                            passRange = passRangeList.Find(x => x.Color.Trim() == list[find_index].Color.Trim());
                                                        }


                                                        if (FrontSymmetric)
                                                        {
                                                            if (list[find_index].FR_FENDA_dE_Minus15 != null && list[find_index].FR_FENDA_dE_15 != null && list[find_index].FR_FENDA_dE_25 != null && list[find_index].FR_FENDA_dE_45 != null && list[find_index].FR_FENDA_dE_75 != null && list[find_index].FR_FENDA_dE_110 != null && list[find_index].FR_FENDA_dL_Minus15 != null && list[find_index].FR_FENDA_dL_15 != null && list[find_index].FR_FENDA_dL_25 != null && list[find_index].FR_FENDA_dL_45 != null && list[find_index].FR_FENDA_dL_75 != null && list[find_index].FR_FENDA_dL_110 != null && list[find_index].FR_FENDA_da_Minus15 != null && list[find_index].FR_FENDA_da_15 != null && list[find_index].FR_FENDA_da_25 != null && list[find_index].FR_FENDA_da_45 != null && list[find_index].FR_FENDA_da_75 != null && list[find_index].FR_FENDA_da_110 != null && list[find_index].FR_FENDA_db_Minus15 != null && list[find_index].FR_FENDA_db_15 != null && list[find_index].FR_FENDA_db_25 != null && list[find_index].FR_FENDA_db_45 != null && list[find_index].FR_FENDA_db_75 != null && list[find_index].FR_FENDA_db_110 != null && list[find_index].FR_FENDA_L_Minus15 != null && list[find_index].FR_FENDA_L_15 != null && list[find_index].FR_FENDA_L_25 != null && list[find_index].FR_FENDA_L_45 != null && list[find_index].FR_FENDA_L_75 != null && list[find_index].FR_FENDA_L_110 != null && list[find_index].FR_FENDA_a_Minus15 != null && list[find_index].FR_FENDA_a_15 != null && list[find_index].FR_FENDA_a_25 != null && list[find_index].FR_FENDA_a_45 != null && list[find_index].FR_FENDA_a_75 != null && list[find_index].FR_FENDA_a_110 != null && list[find_index].FR_FENDA_b_Minus15 != null && list[find_index].FR_FENDA_b_15 != null && list[find_index].FR_FENDA_b_25 != null && list[find_index].FR_FENDA_b_45 != null && list[find_index].FR_FENDA_b_75 != null && list[find_index].FR_FENDA_b_110 != null && list[find_index].FR_BUMPER_dE_Minus15 != null && list[find_index].FR_BUMPER_dE_15 != null && list[find_index].FR_BUMPER_dE_25 != null && list[find_index].FR_BUMPER_dE_45 != null && list[find_index].FR_BUMPER_dE_75 != null && list[find_index].FR_BUMPER_dE_110 != null && list[find_index].FR_BUMPER_dL_Minus15 != null && list[find_index].FR_BUMPER_dL_15 != null && list[find_index].FR_BUMPER_dL_25 != null && list[find_index].FR_BUMPER_dL_45 != null && list[find_index].FR_BUMPER_dL_75 != null && list[find_index].FR_BUMPER_dL_110 != null && list[find_index].FR_BUMPER_da_Minus15 != null && list[find_index].FR_BUMPER_da_15 != null && list[find_index].FR_BUMPER_da_25 != null && list[find_index].FR_BUMPER_da_45 != null && list[find_index].FR_BUMPER_da_75 != null && list[find_index].FR_BUMPER_da_110 != null && list[find_index].FR_BUMPER_db_Minus15 != null && list[find_index].FR_BUMPER_db_15 != null && list[find_index].FR_BUMPER_db_25 != null && list[find_index].FR_BUMPER_db_45 != null && list[find_index].FR_BUMPER_db_75 != null && list[find_index].FR_BUMPER_db_110 != null && list[find_index].FR_BUMPER_L_Minus15 != null && list[find_index].FR_BUMPER_L_15 != null && list[find_index].FR_BUMPER_L_25 != null && list[find_index].FR_BUMPER_L_45 != null && list[find_index].FR_BUMPER_L_75 != null && list[find_index].FR_BUMPER_L_110 != null && list[find_index].FR_BUMPER_a_Minus15 != null && list[find_index].FR_BUMPER_a_15 != null && list[find_index].FR_BUMPER_a_25 != null && list[find_index].FR_BUMPER_a_45 != null && list[find_index].FR_BUMPER_a_75 != null && list[find_index].FR_BUMPER_a_110 != null && list[find_index].FR_BUMPER_b_Minus15 != null && list[find_index].FR_BUMPER_b_15 != null && list[find_index].FR_BUMPER_b_25 != null && list[find_index].FR_BUMPER_b_45 != null && list[find_index].FR_BUMPER_b_75 != null && list[find_index].FR_BUMPER_b_110 != null && list[find_index].FR_FENDA_dE_Minus15 != "" && list[find_index].FR_FENDA_dE_15 != "" && list[find_index].FR_FENDA_dE_25 != "" && list[find_index].FR_FENDA_dE_45 != "" && list[find_index].FR_FENDA_dE_75 != "" && list[find_index].FR_FENDA_dE_110 != "" && list[find_index].FR_FENDA_dL_Minus15 != "" && list[find_index].FR_FENDA_dL_15 != "" && list[find_index].FR_FENDA_dL_25 != "" && list[find_index].FR_FENDA_dL_45 != "" && list[find_index].FR_FENDA_dL_75 != "" && list[find_index].FR_FENDA_dL_110 != "" && list[find_index].FR_FENDA_da_Minus15 != "" && list[find_index].FR_FENDA_da_15 != "" && list[find_index].FR_FENDA_da_25 != "" && list[find_index].FR_FENDA_da_45 != "" && list[find_index].FR_FENDA_da_75 != "" && list[find_index].FR_FENDA_da_110 != "" && list[find_index].FR_FENDA_db_Minus15 != "" && list[find_index].FR_FENDA_db_15 != "" && list[find_index].FR_FENDA_db_25 != "" && list[find_index].FR_FENDA_db_45 != "" && list[find_index].FR_FENDA_db_75 != "" && list[find_index].FR_FENDA_db_110 != "" && list[find_index].FR_FENDA_L_Minus15 != "" && list[find_index].FR_FENDA_L_15 != "" && list[find_index].FR_FENDA_L_25 != "" && list[find_index].FR_FENDA_L_45 != "" && list[find_index].FR_FENDA_L_75 != "" && list[find_index].FR_FENDA_L_110 != "" && list[find_index].FR_FENDA_a_Minus15 != "" && list[find_index].FR_FENDA_a_15 != "" && list[find_index].FR_FENDA_a_25 != "" && list[find_index].FR_FENDA_a_45 != "" && list[find_index].FR_FENDA_a_75 != "" && list[find_index].FR_FENDA_a_110 != "" && list[find_index].FR_FENDA_b_Minus15 != "" && list[find_index].FR_FENDA_b_15 != "" && list[find_index].FR_FENDA_b_25 != "" && list[find_index].FR_FENDA_b_45 != "" && list[find_index].FR_FENDA_b_75 != "" && list[find_index].FR_FENDA_b_110 != "" && list[find_index].FR_BUMPER_dE_Minus15 != "" && list[find_index].FR_BUMPER_dE_15 != "" && list[find_index].FR_BUMPER_dE_25 != "" && list[find_index].FR_BUMPER_dE_45 != "" && list[find_index].FR_BUMPER_dE_75 != "" && list[find_index].FR_BUMPER_dE_110 != "" && list[find_index].FR_BUMPER_dL_Minus15 != "" && list[find_index].FR_BUMPER_dL_15 != "" && list[find_index].FR_BUMPER_dL_25 != "" && list[find_index].FR_BUMPER_dL_45 != "" && list[find_index].FR_BUMPER_dL_75 != "" && list[find_index].FR_BUMPER_dL_110 != "" && list[find_index].FR_BUMPER_da_Minus15 != "" && list[find_index].FR_BUMPER_da_15 != "" && list[find_index].FR_BUMPER_da_25 != "" && list[find_index].FR_BUMPER_da_45 != "" && list[find_index].FR_BUMPER_da_75 != "" && list[find_index].FR_BUMPER_da_110 != "" && list[find_index].FR_BUMPER_db_Minus15 != "" && list[find_index].FR_BUMPER_db_15 != "" && list[find_index].FR_BUMPER_db_25 != "" && list[find_index].FR_BUMPER_db_45 != "" && list[find_index].FR_BUMPER_db_75 != "" && list[find_index].FR_BUMPER_db_110 != "" && list[find_index].FR_BUMPER_L_Minus15 != "" && list[find_index].FR_BUMPER_L_15 != "" && list[find_index].FR_BUMPER_L_25 != "" && list[find_index].FR_BUMPER_L_45 != "" && list[find_index].FR_BUMPER_L_75 != "" && list[find_index].FR_BUMPER_L_110 != "" && list[find_index].FR_BUMPER_a_Minus15 != "" && list[find_index].FR_BUMPER_a_15 != "" && list[find_index].FR_BUMPER_a_25 != "" && list[find_index].FR_BUMPER_a_45 != "" && list[find_index].FR_BUMPER_a_75 != "" && list[find_index].FR_BUMPER_a_110 != "" && list[find_index].FR_BUMPER_b_Minus15 != "" && list[find_index].FR_BUMPER_b_15 != "" && list[find_index].FR_BUMPER_b_25 != "" && list[find_index].FR_BUMPER_b_45 != "" && list[find_index].FR_BUMPER_b_75 != "" && list[find_index].FR_BUMPER_b_110 != "")
                                                            {
                                                                double dE_Minus15 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15), Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15), Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15));
                                                                double dE_15 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_15), Convert.ToDouble(list[find_index].FR_FENDA_a_15), Convert.ToDouble(list[find_index].FR_FENDA_L_15), Convert.ToDouble(list[find_index].FR_BUMPER_L_15), Convert.ToDouble(list[find_index].FR_BUMPER_a_15), Convert.ToDouble(list[find_index].FR_BUMPER_L_15));
                                                                double dE_25 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_25), Convert.ToDouble(list[find_index].FR_FENDA_a_25), Convert.ToDouble(list[find_index].FR_FENDA_L_25), Convert.ToDouble(list[find_index].FR_BUMPER_L_25), Convert.ToDouble(list[find_index].FR_BUMPER_a_25), Convert.ToDouble(list[find_index].FR_BUMPER_L_25));
                                                                double dE_45 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_FENDA_a_45), Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_a_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45));
                                                                double dE_75 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_75), Convert.ToDouble(list[find_index].FR_FENDA_a_75), Convert.ToDouble(list[find_index].FR_FENDA_L_75), Convert.ToDouble(list[find_index].FR_BUMPER_L_75), Convert.ToDouble(list[find_index].FR_BUMPER_a_75), Convert.ToDouble(list[find_index].FR_BUMPER_L_75));
                                                                double dE_110 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_110), Convert.ToDouble(list[find_index].FR_FENDA_a_110), Convert.ToDouble(list[find_index].FR_FENDA_L_110), Convert.ToDouble(list[find_index].FR_BUMPER_L_110), Convert.ToDouble(list[find_index].FR_BUMPER_a_110), Convert.ToDouble(list[find_index].FR_BUMPER_L_110));
                                            
                                                                if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangeValue &&
                                                                    dE_15 > (passRange.Front_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangeValue &&
                                                                    dE_25 > (passRange.Front_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangeValue &&
                                                                    dE_45 > (passRange.Front_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangeValue &&
                                                                    dE_75 > (passRange.Front_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangeValue &&
                                                                    dE_110 > (passRange.Front_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangeValue &&
                                                                (Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_25) - Convert.ToDouble(list[find_index].FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_25) - Convert.ToDouble(list[find_index].FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_25) - Convert.ToDouble(list[find_index].FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_25) - Convert.ToDouble(list[find_index].FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_25) - Convert.ToDouble(list[find_index].FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_25) - Convert.ToDouble(list[find_index].FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_45) - Convert.ToDouble(list[find_index].FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_45) - Convert.ToDouble(list[find_index].FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_45) - Convert.ToDouble(list[find_index].FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_45) - Convert.ToDouble(list[find_index].FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_45) - Convert.ToDouble(list[find_index].FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_45) - Convert.ToDouble(list[find_index].FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_75) - Convert.ToDouble(list[find_index].FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_75) - Convert.ToDouble(list[find_index].FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_75) - Convert.ToDouble(list[find_index].FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_75) - Convert.ToDouble(list[find_index].FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_75) - Convert.ToDouble(list[find_index].FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_75) - Convert.ToDouble(list[find_index].FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_110) - Convert.ToDouble(list[find_index].FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_110) - Convert.ToDouble(list[find_index].FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_110) - Convert.ToDouble(list[find_index].FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_110) - Convert.ToDouble(list[find_index].FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_110) - Convert.ToDouble(list[find_index].FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_110) - Convert.ToDouble(list[find_index].FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangeValue)
                                                                {
                                                                    list[find_index].FR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].FR_Result = "NG";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (iniManager.BlankIsOk)
                                                                {
                                                                    list[find_index].FR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].FR_Result = "NG";
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (list[find_index].FR_FENDA_dE_Minus15 != null && list[find_index].FR_FENDA_dE_15 != null && list[find_index].FR_FENDA_dE_25 != null && list[find_index].FR_FENDA_dE_45 != null && list[find_index].FR_FENDA_dE_75 != null && list[find_index].FR_FENDA_dE_110 != null && list[find_index].FR_FENDA_dL_Minus15 != null && list[find_index].FR_FENDA_dL_15 != null && list[find_index].FR_FENDA_dL_25 != null && list[find_index].FR_FENDA_dL_45 != null && list[find_index].FR_FENDA_dL_75 != null && list[find_index].FR_FENDA_dL_110 != null && list[find_index].FR_FENDA_da_Minus15 != null && list[find_index].FR_FENDA_da_15 != null && list[find_index].FR_FENDA_da_25 != null && list[find_index].FR_FENDA_da_45 != null && list[find_index].FR_FENDA_da_75 != null && list[find_index].FR_FENDA_da_110 != null && list[find_index].FR_FENDA_db_Minus15 != null && list[find_index].FR_FENDA_db_15 != null && list[find_index].FR_FENDA_db_25 != null && list[find_index].FR_FENDA_db_45 != null && list[find_index].FR_FENDA_db_75 != null && list[find_index].FR_FENDA_db_110 != null && list[find_index].FR_FENDA_L_Minus15 != null && list[find_index].FR_FENDA_L_15 != null && list[find_index].FR_FENDA_L_25 != null && list[find_index].FR_FENDA_L_45 != null && list[find_index].FR_FENDA_L_75 != null && list[find_index].FR_FENDA_L_110 != null && list[find_index].FR_FENDA_a_Minus15 != null && list[find_index].FR_FENDA_a_15 != null && list[find_index].FR_FENDA_a_25 != null && list[find_index].FR_FENDA_a_45 != null && list[find_index].FR_FENDA_a_75 != null && list[find_index].FR_FENDA_a_110 != null && list[find_index].FR_FENDA_b_Minus15 != null && list[find_index].FR_FENDA_b_15 != null && list[find_index].FR_FENDA_b_25 != null && list[find_index].FR_FENDA_b_45 != null && list[find_index].FR_FENDA_b_75 != null && list[find_index].FR_FENDA_b_110 != null && list[find_index].FR_BUMPER_dE_Minus15 != null && list[find_index].FR_BUMPER_dE_15 != null && list[find_index].FR_BUMPER_dE_25 != null && list[find_index].FR_BUMPER_dE_45 != null && list[find_index].FR_BUMPER_dE_75 != null && list[find_index].FR_BUMPER_dE_110 != null && list[find_index].FR_BUMPER_dL_Minus15 != null && list[find_index].FR_BUMPER_dL_15 != null && list[find_index].FR_BUMPER_dL_25 != null && list[find_index].FR_BUMPER_dL_45 != null && list[find_index].FR_BUMPER_dL_75 != null && list[find_index].FR_BUMPER_dL_110 != null && list[find_index].FR_BUMPER_da_Minus15 != null && list[find_index].FR_BUMPER_da_15 != null && list[find_index].FR_BUMPER_da_25 != null && list[find_index].FR_BUMPER_da_45 != null && list[find_index].FR_BUMPER_da_75 != null && list[find_index].FR_BUMPER_da_110 != null && list[find_index].FR_BUMPER_db_Minus15 != null && list[find_index].FR_BUMPER_db_15 != null && list[find_index].FR_BUMPER_db_25 != null && list[find_index].FR_BUMPER_db_45 != null && list[find_index].FR_BUMPER_db_75 != null && list[find_index].FR_BUMPER_db_110 != null && list[find_index].FR_BUMPER_L_Minus15 != null && list[find_index].FR_BUMPER_L_15 != null && list[find_index].FR_BUMPER_L_25 != null && list[find_index].FR_BUMPER_L_45 != null && list[find_index].FR_BUMPER_L_75 != null && list[find_index].FR_BUMPER_L_110 != null && list[find_index].FR_BUMPER_a_Minus15 != null && list[find_index].FR_BUMPER_a_15 != null && list[find_index].FR_BUMPER_a_25 != null && list[find_index].FR_BUMPER_a_45 != null && list[find_index].FR_BUMPER_a_75 != null && list[find_index].FR_BUMPER_a_110 != null && list[find_index].FR_BUMPER_b_Minus15 != null && list[find_index].FR_BUMPER_b_15 != null && list[find_index].FR_BUMPER_b_25 != null && list[find_index].FR_BUMPER_b_45 != null && list[find_index].FR_BUMPER_b_75 != null && list[find_index].FR_BUMPER_b_110 != null && list[find_index].FR_FENDA_dE_Minus15 != "" && list[find_index].FR_FENDA_dE_15 != "" && list[find_index].FR_FENDA_dE_25 != "" && list[find_index].FR_FENDA_dE_45 != "" && list[find_index].FR_FENDA_dE_75 != "" && list[find_index].FR_FENDA_dE_110 != "" && list[find_index].FR_FENDA_dL_Minus15 != "" && list[find_index].FR_FENDA_dL_15 != "" && list[find_index].FR_FENDA_dL_25 != "" && list[find_index].FR_FENDA_dL_45 != "" && list[find_index].FR_FENDA_dL_75 != "" && list[find_index].FR_FENDA_dL_110 != "" && list[find_index].FR_FENDA_da_Minus15 != "" && list[find_index].FR_FENDA_da_15 != "" && list[find_index].FR_FENDA_da_25 != "" && list[find_index].FR_FENDA_da_45 != "" && list[find_index].FR_FENDA_da_75 != "" && list[find_index].FR_FENDA_da_110 != "" && list[find_index].FR_FENDA_db_Minus15 != "" && list[find_index].FR_FENDA_db_15 != "" && list[find_index].FR_FENDA_db_25 != "" && list[find_index].FR_FENDA_db_45 != "" && list[find_index].FR_FENDA_db_75 != "" && list[find_index].FR_FENDA_db_110 != "" && list[find_index].FR_FENDA_L_Minus15 != "" && list[find_index].FR_FENDA_L_15 != "" && list[find_index].FR_FENDA_L_25 != "" && list[find_index].FR_FENDA_L_45 != "" && list[find_index].FR_FENDA_L_75 != "" && list[find_index].FR_FENDA_L_110 != "" && list[find_index].FR_FENDA_a_Minus15 != "" && list[find_index].FR_FENDA_a_15 != "" && list[find_index].FR_FENDA_a_25 != "" && list[find_index].FR_FENDA_a_45 != "" && list[find_index].FR_FENDA_a_75 != "" && list[find_index].FR_FENDA_a_110 != "" && list[find_index].FR_FENDA_b_Minus15 != "" && list[find_index].FR_FENDA_b_15 != "" && list[find_index].FR_FENDA_b_25 != "" && list[find_index].FR_FENDA_b_45 != "" && list[find_index].FR_FENDA_b_75 != "" && list[find_index].FR_FENDA_b_110 != "" && list[find_index].FR_BUMPER_dE_Minus15 != "" && list[find_index].FR_BUMPER_dE_15 != "" && list[find_index].FR_BUMPER_dE_25 != "" && list[find_index].FR_BUMPER_dE_45 != "" && list[find_index].FR_BUMPER_dE_75 != "" && list[find_index].FR_BUMPER_dE_110 != "" && list[find_index].FR_BUMPER_dL_Minus15 != "" && list[find_index].FR_BUMPER_dL_15 != "" && list[find_index].FR_BUMPER_dL_25 != "" && list[find_index].FR_BUMPER_dL_45 != "" && list[find_index].FR_BUMPER_dL_75 != "" && list[find_index].FR_BUMPER_dL_110 != "" && list[find_index].FR_BUMPER_da_Minus15 != "" && list[find_index].FR_BUMPER_da_15 != "" && list[find_index].FR_BUMPER_da_25 != "" && list[find_index].FR_BUMPER_da_45 != "" && list[find_index].FR_BUMPER_da_75 != "" && list[find_index].FR_BUMPER_da_110 != "" && list[find_index].FR_BUMPER_db_Minus15 != "" && list[find_index].FR_BUMPER_db_15 != "" && list[find_index].FR_BUMPER_db_25 != "" && list[find_index].FR_BUMPER_db_45 != "" && list[find_index].FR_BUMPER_db_75 != "" && list[find_index].FR_BUMPER_db_110 != "" && list[find_index].FR_BUMPER_L_Minus15 != "" && list[find_index].FR_BUMPER_L_15 != "" && list[find_index].FR_BUMPER_L_25 != "" && list[find_index].FR_BUMPER_L_45 != "" && list[find_index].FR_BUMPER_L_75 != "" && list[find_index].FR_BUMPER_L_110 != "" && list[find_index].FR_BUMPER_a_Minus15 != "" && list[find_index].FR_BUMPER_a_15 != "" && list[find_index].FR_BUMPER_a_25 != "" && list[find_index].FR_BUMPER_a_45 != "" && list[find_index].FR_BUMPER_a_75 != "" && list[find_index].FR_BUMPER_a_110 != "" && list[find_index].FR_BUMPER_b_Minus15 != "" && list[find_index].FR_BUMPER_b_15 != "" && list[find_index].FR_BUMPER_b_25 != "" && list[find_index].FR_BUMPER_b_45 != "" && list[find_index].FR_BUMPER_b_75 != "" && list[find_index].FR_BUMPER_b_110 != "")
                                                            {
                                                                double dE_Minus15 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15), Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15), Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15), Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15));
                                                                double dE_15 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_15), Convert.ToDouble(list[find_index].FR_FENDA_a_15), Convert.ToDouble(list[find_index].FR_FENDA_L_15), Convert.ToDouble(list[find_index].FR_BUMPER_L_15), Convert.ToDouble(list[find_index].FR_BUMPER_a_15), Convert.ToDouble(list[find_index].FR_BUMPER_L_15));
                                                                double dE_25 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_25), Convert.ToDouble(list[find_index].FR_FENDA_a_25), Convert.ToDouble(list[find_index].FR_FENDA_L_25), Convert.ToDouble(list[find_index].FR_BUMPER_L_25), Convert.ToDouble(list[find_index].FR_BUMPER_a_25), Convert.ToDouble(list[find_index].FR_BUMPER_L_25));
                                                                double dE_45 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_FENDA_a_45), Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_a_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45));
                                                                double dE_75 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_75), Convert.ToDouble(list[find_index].FR_FENDA_a_75), Convert.ToDouble(list[find_index].FR_FENDA_L_75), Convert.ToDouble(list[find_index].FR_BUMPER_L_75), Convert.ToDouble(list[find_index].FR_BUMPER_a_75), Convert.ToDouble(list[find_index].FR_BUMPER_L_75));
                                                                double dE_110 = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_110), Convert.ToDouble(list[find_index].FR_FENDA_a_110), Convert.ToDouble(list[find_index].FR_FENDA_L_110), Convert.ToDouble(list[find_index].FR_BUMPER_L_110), Convert.ToDouble(list[find_index].FR_BUMPER_a_110), Convert.ToDouble(list[find_index].FR_BUMPER_L_110));

                                                                if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangePlusValue &&
                                                                    dE_15 > (passRange.Front_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangePlusValue &&
                                                                    dE_25 > (passRange.Front_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangePlusValue &&
                                                                    dE_45 > (passRange.Front_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangePlusValue &&
                                                                    dE_75 > (passRange.Front_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangePlusValue &&
                                                                    dE_110 > (passRange.Front_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_Minus15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_15) - Convert.ToDouble(list[find_index].FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_15) - Convert.ToDouble(list[find_index].FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_15) - Convert.ToDouble(list[find_index].FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_25) - Convert.ToDouble(list[find_index].FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_25) - Convert.ToDouble(list[find_index].FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_25) - Convert.ToDouble(list[find_index].FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_25) - Convert.ToDouble(list[find_index].FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_25) - Convert.ToDouble(list[find_index].FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_25) - Convert.ToDouble(list[find_index].FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_45) - Convert.ToDouble(list[find_index].FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_45) - Convert.ToDouble(list[find_index].FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_45) - Convert.ToDouble(list[find_index].FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_45) - Convert.ToDouble(list[find_index].FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_45) - Convert.ToDouble(list[find_index].FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_45) - Convert.ToDouble(list[find_index].FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_75) - Convert.ToDouble(list[find_index].FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_75) - Convert.ToDouble(list[find_index].FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_75) - Convert.ToDouble(list[find_index].FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_75) - Convert.ToDouble(list[find_index].FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_75) - Convert.ToDouble(list[find_index].FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_75) - Convert.ToDouble(list[find_index].FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_L_110) - Convert.ToDouble(list[find_index].FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_L_110) - Convert.ToDouble(list[find_index].FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_a_110) - Convert.ToDouble(list[find_index].FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_a_110) - Convert.ToDouble(list[find_index].FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].FR_FENDA_b_110) - Convert.ToDouble(list[find_index].FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].FR_FENDA_b_110) - Convert.ToDouble(list[find_index].FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangePlusValue)
                                                                {
                                                                    list[find_index].FR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].FR_Result = "NG";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (iniManager.BlankIsOk)
                                                                {
                                                                    list[find_index].FR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].FR_Result = "NG";
                                                                }
                                                            }
                                                        }

                                                        if (RearSymmetric)
                                                        {
                                                            if (list[find_index].RR_QTR_dE_Minus15 != null && list[find_index].RR_QTR_dE_15 != null && list[find_index].RR_QTR_dE_25 != null && list[find_index].RR_QTR_dE_45 != null && list[find_index].RR_QTR_dE_75 != null && list[find_index].RR_QTR_dE_110 != null && list[find_index].RR_QTR_dL_Minus15 != null && list[find_index].RR_QTR_dL_15 != null && list[find_index].RR_QTR_dL_25 != null && list[find_index].RR_QTR_dL_45 != null && list[find_index].RR_QTR_dL_75 != null && list[find_index].RR_QTR_dL_110 != null && list[find_index].RR_QTR_da_Minus15 != null && list[find_index].RR_QTR_da_15 != null && list[find_index].RR_QTR_da_25 != null && list[find_index].RR_QTR_da_45 != null && list[find_index].RR_QTR_da_75 != null && list[find_index].RR_QTR_da_110 != null && list[find_index].RR_QTR_db_Minus15 != null && list[find_index].RR_QTR_db_15 != null && list[find_index].RR_QTR_db_25 != null && list[find_index].RR_QTR_db_45 != null && list[find_index].RR_QTR_db_75 != null && list[find_index].RR_QTR_db_110 != null && list[find_index].RR_QTR_L_Minus15 != null && list[find_index].RR_QTR_L_15 != null && list[find_index].RR_QTR_L_25 != null && list[find_index].RR_QTR_L_45 != null && list[find_index].RR_QTR_L_75 != null && list[find_index].RR_QTR_L_110 != null && list[find_index].RR_QTR_a_Minus15 != null && list[find_index].RR_QTR_a_15 != null && list[find_index].RR_QTR_a_25 != null && list[find_index].RR_QTR_a_45 != null && list[find_index].RR_QTR_a_75 != null && list[find_index].RR_QTR_a_110 != null && list[find_index].RR_QTR_b_Minus15 != null && list[find_index].RR_QTR_b_15 != null && list[find_index].RR_QTR_b_25 != null && list[find_index].RR_QTR_b_45 != null && list[find_index].RR_QTR_b_75 != null && list[find_index].RR_QTR_b_110 != null && list[find_index].RR_BUMPER_dE_Minus15 != null && list[find_index].RR_BUMPER_dE_15 != null && list[find_index].RR_BUMPER_dE_25 != null && list[find_index].RR_BUMPER_dE_45 != null && list[find_index].RR_BUMPER_dE_75 != null && list[find_index].RR_BUMPER_dE_110 != null && list[find_index].RR_BUMPER_dL_Minus15 != null && list[find_index].RR_BUMPER_dL_15 != null && list[find_index].RR_BUMPER_dL_25 != null && list[find_index].RR_BUMPER_dL_45 != null && list[find_index].RR_BUMPER_dL_75 != null && list[find_index].RR_BUMPER_dL_110 != null && list[find_index].RR_BUMPER_da_Minus15 != null && list[find_index].RR_BUMPER_da_15 != null && list[find_index].RR_BUMPER_da_25 != null && list[find_index].RR_BUMPER_da_45 != null && list[find_index].RR_BUMPER_da_75 != null && list[find_index].RR_BUMPER_da_110 != null && list[find_index].RR_BUMPER_db_Minus15 != null && list[find_index].RR_BUMPER_db_15 != null && list[find_index].RR_BUMPER_db_25 != null && list[find_index].RR_BUMPER_db_45 != null && list[find_index].RR_BUMPER_db_75 != null && list[find_index].RR_BUMPER_db_110 != null && list[find_index].RR_BUMPER_L_Minus15 != null && list[find_index].RR_BUMPER_L_15 != null && list[find_index].RR_BUMPER_L_25 != null && list[find_index].RR_BUMPER_L_45 != null && list[find_index].RR_BUMPER_L_75 != null && list[find_index].RR_BUMPER_L_110 != null && list[find_index].RR_BUMPER_a_Minus15 != null && list[find_index].RR_BUMPER_a_15 != null && list[find_index].RR_BUMPER_a_25 != null && list[find_index].RR_BUMPER_a_45 != null && list[find_index].RR_BUMPER_a_75 != null && list[find_index].RR_BUMPER_a_110 != null && list[find_index].RR_BUMPER_b_Minus15 != null && list[find_index].RR_BUMPER_b_15 != null && list[find_index].RR_BUMPER_b_25 != null && list[find_index].RR_BUMPER_b_45 != null && list[find_index].RR_BUMPER_b_75 != null && list[find_index].RR_BUMPER_b_110 != null && list[find_index].RR_QTR_dE_Minus15 != "" && list[find_index].RR_QTR_dE_15 != "" && list[find_index].RR_QTR_dE_25 != "" && list[find_index].RR_QTR_dE_45 != "" && list[find_index].RR_QTR_dE_75 != "" && list[find_index].RR_QTR_dE_110 != "" && list[find_index].RR_QTR_dL_Minus15 != "" && list[find_index].RR_QTR_dL_15 != "" && list[find_index].RR_QTR_dL_25 != "" && list[find_index].RR_QTR_dL_45 != "" && list[find_index].RR_QTR_dL_75 != "" && list[find_index].RR_QTR_dL_110 != "" && list[find_index].RR_QTR_da_Minus15 != "" && list[find_index].RR_QTR_da_15 != "" && list[find_index].RR_QTR_da_25 != "" && list[find_index].RR_QTR_da_45 != "" && list[find_index].RR_QTR_da_75 != "" && list[find_index].RR_QTR_da_110 != "" && list[find_index].RR_QTR_db_Minus15 != "" && list[find_index].RR_QTR_db_15 != "" && list[find_index].RR_QTR_db_25 != "" && list[find_index].RR_QTR_db_45 != "" && list[find_index].RR_QTR_db_75 != "" && list[find_index].RR_QTR_db_110 != "" && list[find_index].RR_QTR_L_Minus15 != "" && list[find_index].RR_QTR_L_15 != "" && list[find_index].RR_QTR_L_25 != "" && list[find_index].RR_QTR_L_45 != "" && list[find_index].RR_QTR_L_75 != "" && list[find_index].RR_QTR_L_110 != "" && list[find_index].RR_QTR_a_Minus15 != "" && list[find_index].RR_QTR_a_15 != "" && list[find_index].RR_QTR_a_25 != "" && list[find_index].RR_QTR_a_45 != "" && list[find_index].RR_QTR_a_75 != "" && list[find_index].RR_QTR_a_110 != "" && list[find_index].RR_QTR_b_Minus15 != "" && list[find_index].RR_QTR_b_15 != "" && list[find_index].RR_QTR_b_25 != "" && list[find_index].RR_QTR_b_45 != "" && list[find_index].RR_QTR_b_75 != "" && list[find_index].RR_QTR_b_110 != "" && list[find_index].RR_BUMPER_dE_Minus15 != "" && list[find_index].RR_BUMPER_dE_15 != "" && list[find_index].RR_BUMPER_dE_25 != "" && list[find_index].RR_BUMPER_dE_45 != "" && list[find_index].RR_BUMPER_dE_75 != "" && list[find_index].RR_BUMPER_dE_110 != "" && list[find_index].RR_BUMPER_dL_Minus15 != "" && list[find_index].RR_BUMPER_dL_15 != "" && list[find_index].RR_BUMPER_dL_25 != "" && list[find_index].RR_BUMPER_dL_45 != "" && list[find_index].RR_BUMPER_dL_75 != "" && list[find_index].RR_BUMPER_dL_110 != "" && list[find_index].RR_BUMPER_da_Minus15 != "" && list[find_index].RR_BUMPER_da_15 != "" && list[find_index].RR_BUMPER_da_25 != "" && list[find_index].RR_BUMPER_da_45 != "" && list[find_index].RR_BUMPER_da_75 != "" && list[find_index].RR_BUMPER_da_110 != "" && list[find_index].RR_BUMPER_db_Minus15 != "" && list[find_index].RR_BUMPER_db_15 != "" && list[find_index].RR_BUMPER_db_25 != "" && list[find_index].RR_BUMPER_db_45 != "" && list[find_index].RR_BUMPER_db_75 != "" && list[find_index].RR_BUMPER_db_110 != "" && list[find_index].RR_BUMPER_L_Minus15 != "" && list[find_index].RR_BUMPER_L_15 != "" && list[find_index].RR_BUMPER_L_25 != "" && list[find_index].RR_BUMPER_L_45 != "" && list[find_index].RR_BUMPER_L_75 != "" && list[find_index].RR_BUMPER_L_110 != "" && list[find_index].RR_BUMPER_a_Minus15 != "" && list[find_index].RR_BUMPER_a_15 != "" && list[find_index].RR_BUMPER_a_25 != "" && list[find_index].RR_BUMPER_a_45 != "" && list[find_index].RR_BUMPER_a_75 != "" && list[find_index].RR_BUMPER_a_110 != "" && list[find_index].RR_BUMPER_b_Minus15 != "" && list[find_index].RR_BUMPER_b_15 != "" && list[find_index].RR_BUMPER_b_25 != "" && list[find_index].RR_BUMPER_b_45 != "" && list[find_index].RR_BUMPER_b_75 != "" && list[find_index].RR_BUMPER_b_110 != "")
                                                            {
                                                                double dE_Minus15 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_Minus15), Convert.ToDouble(list[find_index].RR_QTR_a_Minus15), Convert.ToDouble(list[find_index].RR_QTR_L_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15));
                                                                double dE_15 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_15), Convert.ToDouble(list[find_index].RR_QTR_a_15), Convert.ToDouble(list[find_index].RR_QTR_L_15), Convert.ToDouble(list[find_index].RR_BUMPER_L_15), Convert.ToDouble(list[find_index].RR_BUMPER_a_15), Convert.ToDouble(list[find_index].RR_BUMPER_L_15));
                                                                double dE_25 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_25), Convert.ToDouble(list[find_index].RR_QTR_a_25), Convert.ToDouble(list[find_index].RR_QTR_L_25), Convert.ToDouble(list[find_index].RR_BUMPER_L_25), Convert.ToDouble(list[find_index].RR_BUMPER_a_25), Convert.ToDouble(list[find_index].RR_BUMPER_L_25));
                                                                double dE_45 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_QTR_a_45), Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_a_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45));
                                                                double dE_75 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_75), Convert.ToDouble(list[find_index].RR_QTR_a_75), Convert.ToDouble(list[find_index].RR_QTR_L_75), Convert.ToDouble(list[find_index].RR_BUMPER_L_75), Convert.ToDouble(list[find_index].RR_BUMPER_a_75), Convert.ToDouble(list[find_index].RR_BUMPER_L_75));
                                                                double dE_110 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_110), Convert.ToDouble(list[find_index].RR_QTR_a_110), Convert.ToDouble(list[find_index].RR_QTR_L_110), Convert.ToDouble(list[find_index].RR_BUMPER_L_110), Convert.ToDouble(list[find_index].RR_BUMPER_a_110), Convert.ToDouble(list[find_index].RR_BUMPER_L_110));

                                                                if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangeValue &&
                                                                    dE_15 > (passRange.Rear_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangeValue &&
                                                                    dE_25 > (passRange.Rear_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangeValue &&
                                                                    dE_45 > (passRange.Rear_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangeValue &&
                                                                    dE_75 > (passRange.Rear_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangeValue &&
                                                                    dE_110 > (passRange.Rear_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_25) - Convert.ToDouble(list[find_index].RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_25) - Convert.ToDouble(list[find_index].RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_25) - Convert.ToDouble(list[find_index].RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_25) - Convert.ToDouble(list[find_index].RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_25) - Convert.ToDouble(list[find_index].RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_25) - Convert.ToDouble(list[find_index].RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_45) - Convert.ToDouble(list[find_index].RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_45) - Convert.ToDouble(list[find_index].RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_45) - Convert.ToDouble(list[find_index].RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_45) - Convert.ToDouble(list[find_index].RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_45) - Convert.ToDouble(list[find_index].RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_45) - Convert.ToDouble(list[find_index].RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_75) - Convert.ToDouble(list[find_index].RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_75) - Convert.ToDouble(list[find_index].RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_75) - Convert.ToDouble(list[find_index].RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_75) - Convert.ToDouble(list[find_index].RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_75) - Convert.ToDouble(list[find_index].RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_75) - Convert.ToDouble(list[find_index].RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_110) - Convert.ToDouble(list[find_index].RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_110) - Convert.ToDouble(list[find_index].RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_110) - Convert.ToDouble(list[find_index].RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_110) - Convert.ToDouble(list[find_index].RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangeValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_110) - Convert.ToDouble(list[find_index].RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_110) - Convert.ToDouble(list[find_index].RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangeValue)
                                                                {
                                                                    list[find_index].RR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].RR_Result = "NG";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (iniManager.BlankIsOk)
                                                                {
                                                                    list[find_index].RR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].RR_Result = "NG";
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (list[find_index].RR_QTR_dE_Minus15 != null && list[find_index].RR_QTR_dE_15 != null && list[find_index].RR_QTR_dE_25 != null && list[find_index].RR_QTR_dE_45 != null && list[find_index].RR_QTR_dE_75 != null && list[find_index].RR_QTR_dE_110 != null && list[find_index].RR_QTR_dL_Minus15 != null && list[find_index].RR_QTR_dL_15 != null && list[find_index].RR_QTR_dL_25 != null && list[find_index].RR_QTR_dL_45 != null && list[find_index].RR_QTR_dL_75 != null && list[find_index].RR_QTR_dL_110 != null && list[find_index].RR_QTR_da_Minus15 != null && list[find_index].RR_QTR_da_15 != null && list[find_index].RR_QTR_da_25 != null && list[find_index].RR_QTR_da_45 != null && list[find_index].RR_QTR_da_75 != null && list[find_index].RR_QTR_da_110 != null && list[find_index].RR_QTR_db_Minus15 != null && list[find_index].RR_QTR_db_15 != null && list[find_index].RR_QTR_db_25 != null && list[find_index].RR_QTR_db_45 != null && list[find_index].RR_QTR_db_75 != null && list[find_index].RR_QTR_db_110 != null && list[find_index].RR_QTR_L_Minus15 != null && list[find_index].RR_QTR_L_15 != null && list[find_index].RR_QTR_L_25 != null && list[find_index].RR_QTR_L_45 != null && list[find_index].RR_QTR_L_75 != null && list[find_index].RR_QTR_L_110 != null && list[find_index].RR_QTR_a_Minus15 != null && list[find_index].RR_QTR_a_15 != null && list[find_index].RR_QTR_a_25 != null && list[find_index].RR_QTR_a_45 != null && list[find_index].RR_QTR_a_75 != null && list[find_index].RR_QTR_a_110 != null && list[find_index].RR_QTR_b_Minus15 != null && list[find_index].RR_QTR_b_15 != null && list[find_index].RR_QTR_b_25 != null && list[find_index].RR_QTR_b_45 != null && list[find_index].RR_QTR_b_75 != null && list[find_index].RR_QTR_b_110 != null && list[find_index].RR_BUMPER_dE_Minus15 != null && list[find_index].RR_BUMPER_dE_15 != null && list[find_index].RR_BUMPER_dE_25 != null && list[find_index].RR_BUMPER_dE_45 != null && list[find_index].RR_BUMPER_dE_75 != null && list[find_index].RR_BUMPER_dE_110 != null && list[find_index].RR_BUMPER_dL_Minus15 != null && list[find_index].RR_BUMPER_dL_15 != null && list[find_index].RR_BUMPER_dL_25 != null && list[find_index].RR_BUMPER_dL_45 != null && list[find_index].RR_BUMPER_dL_75 != null && list[find_index].RR_BUMPER_dL_110 != null && list[find_index].RR_BUMPER_da_Minus15 != null && list[find_index].RR_BUMPER_da_15 != null && list[find_index].RR_BUMPER_da_25 != null && list[find_index].RR_BUMPER_da_45 != null && list[find_index].RR_BUMPER_da_75 != null && list[find_index].RR_BUMPER_da_110 != null && list[find_index].RR_BUMPER_db_Minus15 != null && list[find_index].RR_BUMPER_db_15 != null && list[find_index].RR_BUMPER_db_25 != null && list[find_index].RR_BUMPER_db_45 != null && list[find_index].RR_BUMPER_db_75 != null && list[find_index].RR_BUMPER_db_110 != null && list[find_index].RR_BUMPER_L_Minus15 != null && list[find_index].RR_BUMPER_L_15 != null && list[find_index].RR_BUMPER_L_25 != null && list[find_index].RR_BUMPER_L_45 != null && list[find_index].RR_BUMPER_L_75 != null && list[find_index].RR_BUMPER_L_110 != null && list[find_index].RR_BUMPER_a_Minus15 != null && list[find_index].RR_BUMPER_a_15 != null && list[find_index].RR_BUMPER_a_25 != null && list[find_index].RR_BUMPER_a_45 != null && list[find_index].RR_BUMPER_a_75 != null && list[find_index].RR_BUMPER_a_110 != null && list[find_index].RR_BUMPER_b_Minus15 != null && list[find_index].RR_BUMPER_b_15 != null && list[find_index].RR_BUMPER_b_25 != null && list[find_index].RR_BUMPER_b_45 != null && list[find_index].RR_BUMPER_b_75 != null && list[find_index].RR_BUMPER_b_110 != null && list[find_index].RR_QTR_dE_Minus15 != "" && list[find_index].RR_QTR_dE_15 != "" && list[find_index].RR_QTR_dE_25 != "" && list[find_index].RR_QTR_dE_45 != "" && list[find_index].RR_QTR_dE_75 != "" && list[find_index].RR_QTR_dE_110 != "" && list[find_index].RR_QTR_dL_Minus15 != "" && list[find_index].RR_QTR_dL_15 != "" && list[find_index].RR_QTR_dL_25 != "" && list[find_index].RR_QTR_dL_45 != "" && list[find_index].RR_QTR_dL_75 != "" && list[find_index].RR_QTR_dL_110 != "" && list[find_index].RR_QTR_da_Minus15 != "" && list[find_index].RR_QTR_da_15 != "" && list[find_index].RR_QTR_da_25 != "" && list[find_index].RR_QTR_da_45 != "" && list[find_index].RR_QTR_da_75 != "" && list[find_index].RR_QTR_da_110 != "" && list[find_index].RR_QTR_db_Minus15 != "" && list[find_index].RR_QTR_db_15 != "" && list[find_index].RR_QTR_db_25 != "" && list[find_index].RR_QTR_db_45 != "" && list[find_index].RR_QTR_db_75 != "" && list[find_index].RR_QTR_db_110 != "" && list[find_index].RR_QTR_L_Minus15 != "" && list[find_index].RR_QTR_L_15 != "" && list[find_index].RR_QTR_L_25 != "" && list[find_index].RR_QTR_L_45 != "" && list[find_index].RR_QTR_L_75 != "" && list[find_index].RR_QTR_L_110 != "" && list[find_index].RR_QTR_a_Minus15 != "" && list[find_index].RR_QTR_a_15 != "" && list[find_index].RR_QTR_a_25 != "" && list[find_index].RR_QTR_a_45 != "" && list[find_index].RR_QTR_a_75 != "" && list[find_index].RR_QTR_a_110 != "" && list[find_index].RR_QTR_b_Minus15 != "" && list[find_index].RR_QTR_b_15 != "" && list[find_index].RR_QTR_b_25 != "" && list[find_index].RR_QTR_b_45 != "" && list[find_index].RR_QTR_b_75 != "" && list[find_index].RR_QTR_b_110 != "" && list[find_index].RR_BUMPER_dE_Minus15 != "" && list[find_index].RR_BUMPER_dE_15 != "" && list[find_index].RR_BUMPER_dE_25 != "" && list[find_index].RR_BUMPER_dE_45 != "" && list[find_index].RR_BUMPER_dE_75 != "" && list[find_index].RR_BUMPER_dE_110 != "" && list[find_index].RR_BUMPER_dL_Minus15 != "" && list[find_index].RR_BUMPER_dL_15 != "" && list[find_index].RR_BUMPER_dL_25 != "" && list[find_index].RR_BUMPER_dL_45 != "" && list[find_index].RR_BUMPER_dL_75 != "" && list[find_index].RR_BUMPER_dL_110 != "" && list[find_index].RR_BUMPER_da_Minus15 != "" && list[find_index].RR_BUMPER_da_15 != "" && list[find_index].RR_BUMPER_da_25 != "" && list[find_index].RR_BUMPER_da_45 != "" && list[find_index].RR_BUMPER_da_75 != "" && list[find_index].RR_BUMPER_da_110 != "" && list[find_index].RR_BUMPER_db_Minus15 != "" && list[find_index].RR_BUMPER_db_15 != "" && list[find_index].RR_BUMPER_db_25 != "" && list[find_index].RR_BUMPER_db_45 != "" && list[find_index].RR_BUMPER_db_75 != "" && list[find_index].RR_BUMPER_db_110 != "" && list[find_index].RR_BUMPER_L_Minus15 != "" && list[find_index].RR_BUMPER_L_15 != "" && list[find_index].RR_BUMPER_L_25 != "" && list[find_index].RR_BUMPER_L_45 != "" && list[find_index].RR_BUMPER_L_75 != "" && list[find_index].RR_BUMPER_L_110 != "" && list[find_index].RR_BUMPER_a_Minus15 != "" && list[find_index].RR_BUMPER_a_15 != "" && list[find_index].RR_BUMPER_a_25 != "" && list[find_index].RR_BUMPER_a_45 != "" && list[find_index].RR_BUMPER_a_75 != "" && list[find_index].RR_BUMPER_a_110 != "" && list[find_index].RR_BUMPER_b_Minus15 != "" && list[find_index].RR_BUMPER_b_15 != "" && list[find_index].RR_BUMPER_b_25 != "" && list[find_index].RR_BUMPER_b_45 != "" && list[find_index].RR_BUMPER_b_75 != "" && list[find_index].RR_BUMPER_b_110 != "")
                                                            {
                                                                double dE_Minus15 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_Minus15), Convert.ToDouble(list[find_index].RR_QTR_a_Minus15), Convert.ToDouble(list[find_index].RR_QTR_L_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15), Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15));
                                                                double dE_15 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_15), Convert.ToDouble(list[find_index].RR_QTR_a_15), Convert.ToDouble(list[find_index].RR_QTR_L_15), Convert.ToDouble(list[find_index].RR_BUMPER_L_15), Convert.ToDouble(list[find_index].RR_BUMPER_a_15), Convert.ToDouble(list[find_index].RR_BUMPER_L_15));
                                                                double dE_25 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_25), Convert.ToDouble(list[find_index].RR_QTR_a_25), Convert.ToDouble(list[find_index].RR_QTR_L_25), Convert.ToDouble(list[find_index].RR_BUMPER_L_25), Convert.ToDouble(list[find_index].RR_BUMPER_a_25), Convert.ToDouble(list[find_index].RR_BUMPER_L_25));
                                                                double dE_45 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_QTR_a_45), Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_a_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45));
                                                                double dE_75 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_75), Convert.ToDouble(list[find_index].RR_QTR_a_75), Convert.ToDouble(list[find_index].RR_QTR_L_75), Convert.ToDouble(list[find_index].RR_BUMPER_L_75), Convert.ToDouble(list[find_index].RR_BUMPER_a_75), Convert.ToDouble(list[find_index].RR_BUMPER_L_75));
                                                                double dE_110 = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_110), Convert.ToDouble(list[find_index].RR_QTR_a_110), Convert.ToDouble(list[find_index].RR_QTR_L_110), Convert.ToDouble(list[find_index].RR_BUMPER_L_110), Convert.ToDouble(list[find_index].RR_BUMPER_a_110), Convert.ToDouble(list[find_index].RR_BUMPER_L_110));

                                                                if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangePlusValue &&
                                                                    dE_15 > (passRange.Rear_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangePlusValue &&
                                                                    dE_25 > (passRange.Rear_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangePlusValue &&
                                                                    dE_45 > (passRange.Rear_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangePlusValue &&
                                                                    dE_75 > (passRange.Rear_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangePlusValue &&
                                                                    dE_110 > (passRange.Rear_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_Minus15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_15) - Convert.ToDouble(list[find_index].RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_15) - Convert.ToDouble(list[find_index].RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_15) - Convert.ToDouble(list[find_index].RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_25) - Convert.ToDouble(list[find_index].RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_25) - Convert.ToDouble(list[find_index].RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_25) - Convert.ToDouble(list[find_index].RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_25) - Convert.ToDouble(list[find_index].RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_25) - Convert.ToDouble(list[find_index].RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_25) - Convert.ToDouble(list[find_index].RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_45) - Convert.ToDouble(list[find_index].RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_45) - Convert.ToDouble(list[find_index].RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_45) - Convert.ToDouble(list[find_index].RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_45) - Convert.ToDouble(list[find_index].RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_45) - Convert.ToDouble(list[find_index].RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_45) - Convert.ToDouble(list[find_index].RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_75) - Convert.ToDouble(list[find_index].RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_75) - Convert.ToDouble(list[find_index].RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_75) - Convert.ToDouble(list[find_index].RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_75) - Convert.ToDouble(list[find_index].RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_75) - Convert.ToDouble(list[find_index].RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_75) - Convert.ToDouble(list[find_index].RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_L_110) - Convert.ToDouble(list[find_index].RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_L_110) - Convert.ToDouble(list[find_index].RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_a_110) - Convert.ToDouble(list[find_index].RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_a_110) - Convert.ToDouble(list[find_index].RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangePlusValue &&
                                                                    (Convert.ToDouble(list[find_index].RR_QTR_b_110) - Convert.ToDouble(list[find_index].RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(list[find_index].RR_QTR_b_110) - Convert.ToDouble(list[find_index].RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangePlusValue)
                                                                {
                                                                    list[find_index].RR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].RR_Result = "NG";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (iniManager.BlankIsOk)
                                                                {
                                                                    list[find_index].RR_Result = "OK";
                                                                }
                                                                else
                                                                {
                                                                    list[find_index].RR_Result = "NG";
                                                                }
                                                            }
                                                        }

                                                        if (list[find_index].FR_Result == "OK" && list[find_index].RR_Result == "OK")
                                                        {
                                                            list[find_index].Result = "OK";
                                                        }
                                                        else if (list[find_index].FR_Result == "NG" || list[find_index].RR_Result == "NG")
                                                        {
                                                            list[find_index].Result = "NG";
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });

            for (int i = 1; i < list.Count; i++)
            {
                if (Convert.ToDateTime(list[i - 1].DateTime) > Convert.ToDateTime(list[i].DateTime) || Convert.ToInt32(list[i - 1].Comment) > Convert.ToInt32(list[i].Comment))
                {
                    if (Math.Abs(Convert.ToInt32(list[i - 1].Comment) - Convert.ToInt32(list[i].Comment)) == 1)
                    {
                        StructReportData temp = list[i - 1];
                        list[i - 1] = list[i];
                        list[i] = temp;
                    }
                }
            }

            ReportDataList = list;

            await dialogController.CloseAsync();
        }

        private void Total_Result(StructReportData structReportData)
        {
            double front_delta = 0;
            double rear_delta = 0;

            try
            {
                front_delta = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_b_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_b_45));

                rear_delta = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_b_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_b_45));

                structReportData.FR_DELTA = Convert.ToString(Math.Round(front_delta, 2));
                structReportData.RR_DELTA = Convert.ToString(Math.Round(rear_delta, 2));
            }
            catch (Exception ex)
            {
                structReportData.FR_DELTA = "0";
                structReportData.RR_DELTA = "0";
            }


            StructPassRange structPassRange = new StructPassRange();
            List<StructPassRange> passRangeList = LoadPassRangeData();
            StructPassRange passRange = passRangeList.Find(x => x.Color.Trim() == structReportData.Color.Trim());

            if (passRange == null)
            {
                string content = File.ReadAllText(passRangeFilePath);
                content += structReportData.Color.Trim() + ",0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0" + Environment.NewLine;
                File.WriteAllText(passRangeFilePath, content);
                passRangeList = LoadPassRangeData();
                passRange = passRangeList.Find(x => x.Color.Trim() == structReportData.Color.Trim());
            }

            if (FrontSymmetric)
            {
                if (structReportData.FR_FENDA_dE_Minus15 != null && structReportData.FR_FENDA_dE_15 != null && structReportData.FR_FENDA_dE_25 != null && structReportData.FR_FENDA_dE_45 != null && structReportData.FR_FENDA_dE_75 != null && structReportData.FR_FENDA_dE_110 != null && structReportData.FR_FENDA_dL_Minus15 != null && structReportData.FR_FENDA_dL_15 != null && structReportData.FR_FENDA_dL_25 != null && structReportData.FR_FENDA_dL_45 != null && structReportData.FR_FENDA_dL_75 != null && structReportData.FR_FENDA_dL_110 != null && structReportData.FR_FENDA_da_Minus15 != null && structReportData.FR_FENDA_da_15 != null && structReportData.FR_FENDA_da_25 != null && structReportData.FR_FENDA_da_45 != null && structReportData.FR_FENDA_da_75 != null && structReportData.FR_FENDA_da_110 != null && structReportData.FR_FENDA_db_Minus15 != null && structReportData.FR_FENDA_db_15 != null && structReportData.FR_FENDA_db_25 != null && structReportData.FR_FENDA_db_45 != null && structReportData.FR_FENDA_db_75 != null && structReportData.FR_FENDA_db_110 != null && structReportData.FR_FENDA_L_Minus15 != null && structReportData.FR_FENDA_L_15 != null && structReportData.FR_FENDA_L_25 != null && structReportData.FR_FENDA_L_45 != null && structReportData.FR_FENDA_L_75 != null && structReportData.FR_FENDA_L_110 != null && structReportData.FR_FENDA_a_Minus15 != null && structReportData.FR_FENDA_a_15 != null && structReportData.FR_FENDA_a_25 != null && structReportData.FR_FENDA_a_45 != null && structReportData.FR_FENDA_a_75 != null && structReportData.FR_FENDA_a_110 != null && structReportData.FR_FENDA_b_Minus15 != null && structReportData.FR_FENDA_b_15 != null && structReportData.FR_FENDA_b_25 != null && structReportData.FR_FENDA_b_45 != null && structReportData.FR_FENDA_b_75 != null && structReportData.FR_FENDA_b_110 != null && structReportData.FR_BUMPER_dE_Minus15 != null && structReportData.FR_BUMPER_dE_15 != null && structReportData.FR_BUMPER_dE_25 != null && structReportData.FR_BUMPER_dE_45 != null && structReportData.FR_BUMPER_dE_75 != null && structReportData.FR_BUMPER_dE_110 != null && structReportData.FR_BUMPER_dL_Minus15 != null && structReportData.FR_BUMPER_dL_15 != null && structReportData.FR_BUMPER_dL_25 != null && structReportData.FR_BUMPER_dL_45 != null && structReportData.FR_BUMPER_dL_75 != null && structReportData.FR_BUMPER_dL_110 != null && structReportData.FR_BUMPER_da_Minus15 != null && structReportData.FR_BUMPER_da_15 != null && structReportData.FR_BUMPER_da_25 != null && structReportData.FR_BUMPER_da_45 != null && structReportData.FR_BUMPER_da_75 != null && structReportData.FR_BUMPER_da_110 != null && structReportData.FR_BUMPER_db_Minus15 != null && structReportData.FR_BUMPER_db_15 != null && structReportData.FR_BUMPER_db_25 != null && structReportData.FR_BUMPER_db_45 != null && structReportData.FR_BUMPER_db_75 != null && structReportData.FR_BUMPER_db_110 != null && structReportData.FR_BUMPER_L_Minus15 != null && structReportData.FR_BUMPER_L_15 != null && structReportData.FR_BUMPER_L_25 != null && structReportData.FR_BUMPER_L_45 != null && structReportData.FR_BUMPER_L_75 != null && structReportData.FR_BUMPER_L_110 != null && structReportData.FR_BUMPER_a_Minus15 != null && structReportData.FR_BUMPER_a_15 != null && structReportData.FR_BUMPER_a_25 != null && structReportData.FR_BUMPER_a_45 != null && structReportData.FR_BUMPER_a_75 != null && structReportData.FR_BUMPER_a_110 != null && structReportData.FR_BUMPER_b_Minus15 != null && structReportData.FR_BUMPER_b_15 != null && structReportData.FR_BUMPER_b_25 != null && structReportData.FR_BUMPER_b_45 != null && structReportData.FR_BUMPER_b_75 != null && structReportData.FR_BUMPER_b_110 != null && structReportData.FR_FENDA_dE_Minus15 != "" && structReportData.FR_FENDA_dE_15 != "" && structReportData.FR_FENDA_dE_25 != "" && structReportData.FR_FENDA_dE_45 != "" && structReportData.FR_FENDA_dE_75 != "" && structReportData.FR_FENDA_dE_110 != "" && structReportData.FR_FENDA_dL_Minus15 != "" && structReportData.FR_FENDA_dL_15 != "" && structReportData.FR_FENDA_dL_25 != "" && structReportData.FR_FENDA_dL_45 != "" && structReportData.FR_FENDA_dL_75 != "" && structReportData.FR_FENDA_dL_110 != "" && structReportData.FR_FENDA_da_Minus15 != "" && structReportData.FR_FENDA_da_15 != "" && structReportData.FR_FENDA_da_25 != "" && structReportData.FR_FENDA_da_45 != "" && structReportData.FR_FENDA_da_75 != "" && structReportData.FR_FENDA_da_110 != "" && structReportData.FR_FENDA_db_Minus15 != "" && structReportData.FR_FENDA_db_15 != "" && structReportData.FR_FENDA_db_25 != "" && structReportData.FR_FENDA_db_45 != "" && structReportData.FR_FENDA_db_75 != "" && structReportData.FR_FENDA_db_110 != "" && structReportData.FR_FENDA_L_Minus15 != "" && structReportData.FR_FENDA_L_15 != "" && structReportData.FR_FENDA_L_25 != "" && structReportData.FR_FENDA_L_45 != "" && structReportData.FR_FENDA_L_75 != "" && structReportData.FR_FENDA_L_110 != "" && structReportData.FR_FENDA_a_Minus15 != "" && structReportData.FR_FENDA_a_15 != "" && structReportData.FR_FENDA_a_25 != "" && structReportData.FR_FENDA_a_45 != "" && structReportData.FR_FENDA_a_75 != "" && structReportData.FR_FENDA_a_110 != "" && structReportData.FR_FENDA_b_Minus15 != "" && structReportData.FR_FENDA_b_15 != "" && structReportData.FR_FENDA_b_25 != "" && structReportData.FR_FENDA_b_45 != "" && structReportData.FR_FENDA_b_75 != "" && structReportData.FR_FENDA_b_110 != "" && structReportData.FR_BUMPER_dE_Minus15 != "" && structReportData.FR_BUMPER_dE_15 != "" && structReportData.FR_BUMPER_dE_25 != "" && structReportData.FR_BUMPER_dE_45 != "" && structReportData.FR_BUMPER_dE_75 != "" && structReportData.FR_BUMPER_dE_110 != "" && structReportData.FR_BUMPER_dL_Minus15 != "" && structReportData.FR_BUMPER_dL_15 != "" && structReportData.FR_BUMPER_dL_25 != "" && structReportData.FR_BUMPER_dL_45 != "" && structReportData.FR_BUMPER_dL_75 != "" && structReportData.FR_BUMPER_dL_110 != "" && structReportData.FR_BUMPER_da_Minus15 != "" && structReportData.FR_BUMPER_da_15 != "" && structReportData.FR_BUMPER_da_25 != "" && structReportData.FR_BUMPER_da_45 != "" && structReportData.FR_BUMPER_da_75 != "" && structReportData.FR_BUMPER_da_110 != "" && structReportData.FR_BUMPER_db_Minus15 != "" && structReportData.FR_BUMPER_db_15 != "" && structReportData.FR_BUMPER_db_25 != "" && structReportData.FR_BUMPER_db_45 != "" && structReportData.FR_BUMPER_db_75 != "" && structReportData.FR_BUMPER_db_110 != "" && structReportData.FR_BUMPER_L_Minus15 != "" && structReportData.FR_BUMPER_L_15 != "" && structReportData.FR_BUMPER_L_25 != "" && structReportData.FR_BUMPER_L_45 != "" && structReportData.FR_BUMPER_L_75 != "" && structReportData.FR_BUMPER_L_110 != "" && structReportData.FR_BUMPER_a_Minus15 != "" && structReportData.FR_BUMPER_a_15 != "" && structReportData.FR_BUMPER_a_25 != "" && structReportData.FR_BUMPER_a_45 != "" && structReportData.FR_BUMPER_a_75 != "" && structReportData.FR_BUMPER_a_110 != "" && structReportData.FR_BUMPER_b_Minus15 != "" && structReportData.FR_BUMPER_b_15 != "" && structReportData.FR_BUMPER_b_25 != "" && structReportData.FR_BUMPER_b_45 != "" && structReportData.FR_BUMPER_b_75 != "" && structReportData.FR_BUMPER_b_110 != "")
                {
                    double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_FENDA_a_Minus15), Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15));
                    double dE_15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_FENDA_a_15), Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15), Convert.ToDouble(structReportData.FR_BUMPER_a_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15));
                    double dE_25 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_FENDA_a_25), Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25), Convert.ToDouble(structReportData.FR_BUMPER_a_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25));
                    double dE_45 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45));
                    double dE_75 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_FENDA_a_75), Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75), Convert.ToDouble(structReportData.FR_BUMPER_a_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75));
                    double dE_110 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_FENDA_a_110), Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110), Convert.ToDouble(structReportData.FR_BUMPER_a_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110));

                    if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangeValue &&
                        dE_15 > (passRange.Front_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangeValue &&
                        dE_25 > (passRange.Front_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangeValue &&
                        dE_45 > (passRange.Front_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangeValue &&
                        dE_75 > (passRange.Front_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangeValue &&
                        dE_110 > (passRange.Front_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangeValue)
                    {
                        structReportData.FR_Result = "OK";
                    }
                    else
                    {
                        structReportData.FR_Result = "NG";
                    }
                }
                else
                {
                    if (iniManager.BlankIsOk)
                    {
                        structReportData.FR_Result = "OK";
                    }
                    else
                    {
                        structReportData.FR_Result = "NG";
                    }
                }
            }
            else
            {
                if (structReportData.FR_FENDA_dE_Minus15 != null && structReportData.FR_FENDA_dE_15 != null && structReportData.FR_FENDA_dE_25 != null && structReportData.FR_FENDA_dE_45 != null && structReportData.FR_FENDA_dE_75 != null && structReportData.FR_FENDA_dE_110 != null && structReportData.FR_FENDA_dL_Minus15 != null && structReportData.FR_FENDA_dL_15 != null && structReportData.FR_FENDA_dL_25 != null && structReportData.FR_FENDA_dL_45 != null && structReportData.FR_FENDA_dL_75 != null && structReportData.FR_FENDA_dL_110 != null && structReportData.FR_FENDA_da_Minus15 != null && structReportData.FR_FENDA_da_15 != null && structReportData.FR_FENDA_da_25 != null && structReportData.FR_FENDA_da_45 != null && structReportData.FR_FENDA_da_75 != null && structReportData.FR_FENDA_da_110 != null && structReportData.FR_FENDA_db_Minus15 != null && structReportData.FR_FENDA_db_15 != null && structReportData.FR_FENDA_db_25 != null && structReportData.FR_FENDA_db_45 != null && structReportData.FR_FENDA_db_75 != null && structReportData.FR_FENDA_db_110 != null && structReportData.FR_FENDA_L_Minus15 != null && structReportData.FR_FENDA_L_15 != null && structReportData.FR_FENDA_L_25 != null && structReportData.FR_FENDA_L_45 != null && structReportData.FR_FENDA_L_75 != null && structReportData.FR_FENDA_L_110 != null && structReportData.FR_FENDA_a_Minus15 != null && structReportData.FR_FENDA_a_15 != null && structReportData.FR_FENDA_a_25 != null && structReportData.FR_FENDA_a_45 != null && structReportData.FR_FENDA_a_75 != null && structReportData.FR_FENDA_a_110 != null && structReportData.FR_FENDA_b_Minus15 != null && structReportData.FR_FENDA_b_15 != null && structReportData.FR_FENDA_b_25 != null && structReportData.FR_FENDA_b_45 != null && structReportData.FR_FENDA_b_75 != null && structReportData.FR_FENDA_b_110 != null && structReportData.FR_BUMPER_dE_Minus15 != null && structReportData.FR_BUMPER_dE_15 != null && structReportData.FR_BUMPER_dE_25 != null && structReportData.FR_BUMPER_dE_45 != null && structReportData.FR_BUMPER_dE_75 != null && structReportData.FR_BUMPER_dE_110 != null && structReportData.FR_BUMPER_dL_Minus15 != null && structReportData.FR_BUMPER_dL_15 != null && structReportData.FR_BUMPER_dL_25 != null && structReportData.FR_BUMPER_dL_45 != null && structReportData.FR_BUMPER_dL_75 != null && structReportData.FR_BUMPER_dL_110 != null && structReportData.FR_BUMPER_da_Minus15 != null && structReportData.FR_BUMPER_da_15 != null && structReportData.FR_BUMPER_da_25 != null && structReportData.FR_BUMPER_da_45 != null && structReportData.FR_BUMPER_da_75 != null && structReportData.FR_BUMPER_da_110 != null && structReportData.FR_BUMPER_db_Minus15 != null && structReportData.FR_BUMPER_db_15 != null && structReportData.FR_BUMPER_db_25 != null && structReportData.FR_BUMPER_db_45 != null && structReportData.FR_BUMPER_db_75 != null && structReportData.FR_BUMPER_db_110 != null && structReportData.FR_BUMPER_L_Minus15 != null && structReportData.FR_BUMPER_L_15 != null && structReportData.FR_BUMPER_L_25 != null && structReportData.FR_BUMPER_L_45 != null && structReportData.FR_BUMPER_L_75 != null && structReportData.FR_BUMPER_L_110 != null && structReportData.FR_BUMPER_a_Minus15 != null && structReportData.FR_BUMPER_a_15 != null && structReportData.FR_BUMPER_a_25 != null && structReportData.FR_BUMPER_a_45 != null && structReportData.FR_BUMPER_a_75 != null && structReportData.FR_BUMPER_a_110 != null && structReportData.FR_BUMPER_b_Minus15 != null && structReportData.FR_BUMPER_b_15 != null && structReportData.FR_BUMPER_b_25 != null && structReportData.FR_BUMPER_b_45 != null && structReportData.FR_BUMPER_b_75 != null && structReportData.FR_BUMPER_b_110 != null && structReportData.FR_FENDA_dE_Minus15 != "" && structReportData.FR_FENDA_dE_15 != "" && structReportData.FR_FENDA_dE_25 != "" && structReportData.FR_FENDA_dE_45 != "" && structReportData.FR_FENDA_dE_75 != "" && structReportData.FR_FENDA_dE_110 != "" && structReportData.FR_FENDA_dL_Minus15 != "" && structReportData.FR_FENDA_dL_15 != "" && structReportData.FR_FENDA_dL_25 != "" && structReportData.FR_FENDA_dL_45 != "" && structReportData.FR_FENDA_dL_75 != "" && structReportData.FR_FENDA_dL_110 != "" && structReportData.FR_FENDA_da_Minus15 != "" && structReportData.FR_FENDA_da_15 != "" && structReportData.FR_FENDA_da_25 != "" && structReportData.FR_FENDA_da_45 != "" && structReportData.FR_FENDA_da_75 != "" && structReportData.FR_FENDA_da_110 != "" && structReportData.FR_FENDA_db_Minus15 != "" && structReportData.FR_FENDA_db_15 != "" && structReportData.FR_FENDA_db_25 != "" && structReportData.FR_FENDA_db_45 != "" && structReportData.FR_FENDA_db_75 != "" && structReportData.FR_FENDA_db_110 != "" && structReportData.FR_FENDA_L_Minus15 != "" && structReportData.FR_FENDA_L_15 != "" && structReportData.FR_FENDA_L_25 != "" && structReportData.FR_FENDA_L_45 != "" && structReportData.FR_FENDA_L_75 != "" && structReportData.FR_FENDA_L_110 != "" && structReportData.FR_FENDA_a_Minus15 != "" && structReportData.FR_FENDA_a_15 != "" && structReportData.FR_FENDA_a_25 != "" && structReportData.FR_FENDA_a_45 != "" && structReportData.FR_FENDA_a_75 != "" && structReportData.FR_FENDA_a_110 != "" && structReportData.FR_FENDA_b_Minus15 != "" && structReportData.FR_FENDA_b_15 != "" && structReportData.FR_FENDA_b_25 != "" && structReportData.FR_FENDA_b_45 != "" && structReportData.FR_FENDA_b_75 != "" && structReportData.FR_FENDA_b_110 != "" && structReportData.FR_BUMPER_dE_Minus15 != "" && structReportData.FR_BUMPER_dE_15 != "" && structReportData.FR_BUMPER_dE_25 != "" && structReportData.FR_BUMPER_dE_45 != "" && structReportData.FR_BUMPER_dE_75 != "" && structReportData.FR_BUMPER_dE_110 != "" && structReportData.FR_BUMPER_dL_Minus15 != "" && structReportData.FR_BUMPER_dL_15 != "" && structReportData.FR_BUMPER_dL_25 != "" && structReportData.FR_BUMPER_dL_45 != "" && structReportData.FR_BUMPER_dL_75 != "" && structReportData.FR_BUMPER_dL_110 != "" && structReportData.FR_BUMPER_da_Minus15 != "" && structReportData.FR_BUMPER_da_15 != "" && structReportData.FR_BUMPER_da_25 != "" && structReportData.FR_BUMPER_da_45 != "" && structReportData.FR_BUMPER_da_75 != "" && structReportData.FR_BUMPER_da_110 != "" && structReportData.FR_BUMPER_db_Minus15 != "" && structReportData.FR_BUMPER_db_15 != "" && structReportData.FR_BUMPER_db_25 != "" && structReportData.FR_BUMPER_db_45 != "" && structReportData.FR_BUMPER_db_75 != "" && structReportData.FR_BUMPER_db_110 != "" && structReportData.FR_BUMPER_L_Minus15 != "" && structReportData.FR_BUMPER_L_15 != "" && structReportData.FR_BUMPER_L_25 != "" && structReportData.FR_BUMPER_L_45 != "" && structReportData.FR_BUMPER_L_75 != "" && structReportData.FR_BUMPER_L_110 != "" && structReportData.FR_BUMPER_a_Minus15 != "" && structReportData.FR_BUMPER_a_15 != "" && structReportData.FR_BUMPER_a_25 != "" && structReportData.FR_BUMPER_a_45 != "" && structReportData.FR_BUMPER_a_75 != "" && structReportData.FR_BUMPER_a_110 != "" && structReportData.FR_BUMPER_b_Minus15 != "" && structReportData.FR_BUMPER_b_15 != "" && structReportData.FR_BUMPER_b_25 != "" && structReportData.FR_BUMPER_b_45 != "" && structReportData.FR_BUMPER_b_75 != "" && structReportData.FR_BUMPER_b_110 != "")
                {
                    double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_FENDA_a_Minus15), Convert.ToDouble(structReportData.FR_FENDA_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15));
                    double dE_15 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_FENDA_a_15), Convert.ToDouble(structReportData.FR_FENDA_L_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15), Convert.ToDouble(structReportData.FR_BUMPER_a_15), Convert.ToDouble(structReportData.FR_BUMPER_L_15));
                    double dE_25 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_FENDA_a_25), Convert.ToDouble(structReportData.FR_FENDA_L_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25), Convert.ToDouble(structReportData.FR_BUMPER_a_25), Convert.ToDouble(structReportData.FR_BUMPER_L_25));
                    double dE_45 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_FENDA_a_45), Convert.ToDouble(structReportData.FR_FENDA_L_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45), Convert.ToDouble(structReportData.FR_BUMPER_a_45), Convert.ToDouble(structReportData.FR_BUMPER_L_45));
                    double dE_75 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_FENDA_a_75), Convert.ToDouble(structReportData.FR_FENDA_L_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75), Convert.ToDouble(structReportData.FR_BUMPER_a_75), Convert.ToDouble(structReportData.FR_BUMPER_L_75));
                    double dE_110 = CalcDelta(Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_FENDA_a_110), Convert.ToDouble(structReportData.FR_FENDA_L_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110), Convert.ToDouble(structReportData.FR_BUMPER_a_110), Convert.ToDouble(structReportData.FR_BUMPER_L_110));

                    if (dE_Minus15 > (passRange.Front_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_Minus15_PassRangePlusValue &&
                        dE_15 > (passRange.Front_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_15_PassRangePlusValue &&
                        dE_25 > (passRange.Front_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_25_PassRangePlusValue &&
                        dE_45 > (passRange.Front_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_45_PassRangePlusValue &&
                        dE_75 > (passRange.Front_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_75_PassRangePlusValue &&
                        dE_110 > (passRange.Front_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Front_dE_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) > (passRange.Front_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_L_Minus15)) < passRange.Front_dL_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) > (passRange.Front_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_a_Minus15)) < passRange.Front_da_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) > (passRange.Front_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_Minus15) - Convert.ToDouble(structReportData.FR_BUMPER_b_Minus15)) < passRange.Front_db_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) > (passRange.Front_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_15) - Convert.ToDouble(structReportData.FR_BUMPER_L_15)) < passRange.Front_dL_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) > (passRange.Front_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_15) - Convert.ToDouble(structReportData.FR_BUMPER_a_15)) < passRange.Front_da_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) > (passRange.Front_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_15) - Convert.ToDouble(structReportData.FR_BUMPER_b_15)) < passRange.Front_db_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) > (passRange.Front_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_25) - Convert.ToDouble(structReportData.FR_BUMPER_L_25)) < passRange.Front_dL_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) > (passRange.Front_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_25) - Convert.ToDouble(structReportData.FR_BUMPER_a_25)) < passRange.Front_da_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) > (passRange.Front_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_25) - Convert.ToDouble(structReportData.FR_BUMPER_b_25)) < passRange.Front_db_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) > (passRange.Front_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_45) - Convert.ToDouble(structReportData.FR_BUMPER_L_45)) < passRange.Front_dL_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) > (passRange.Front_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_45) - Convert.ToDouble(structReportData.FR_BUMPER_a_45)) < passRange.Front_da_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) > (passRange.Front_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_45) - Convert.ToDouble(structReportData.FR_BUMPER_b_45)) < passRange.Front_db_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) > (passRange.Front_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_75) - Convert.ToDouble(structReportData.FR_BUMPER_L_75)) < passRange.Front_dL_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) > (passRange.Front_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_75) - Convert.ToDouble(structReportData.FR_BUMPER_a_75)) < passRange.Front_da_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) > (passRange.Front_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_75) - Convert.ToDouble(structReportData.FR_BUMPER_b_75)) < passRange.Front_db_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) > (passRange.Front_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_L_110) - Convert.ToDouble(structReportData.FR_BUMPER_L_110)) < passRange.Front_dL_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) > (passRange.Front_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_a_110) - Convert.ToDouble(structReportData.FR_BUMPER_a_110)) < passRange.Front_da_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) > (passRange.Front_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.FR_FENDA_b_110) - Convert.ToDouble(structReportData.FR_BUMPER_b_110)) < passRange.Front_db_110_PassRangePlusValue)
                    {                                                                                                                                                                                                                                                                                                         
                        structReportData.FR_Result = "OK";
                    }
                    else
                    {
                        structReportData.FR_Result = "NG";
                    }
                }
                else
                {
                    if (iniManager.BlankIsOk)
                    {
                        structReportData.FR_Result = "OK";
                    }
                    else
                    {
                        structReportData.FR_Result = "NG";
                    }
                }
            }

            if (RearSymmetric)
            {
                if (structReportData.RR_QTR_dE_Minus15 != null && structReportData.RR_QTR_dE_15 != null && structReportData.RR_QTR_dE_25 != null && structReportData.RR_QTR_dE_45 != null && structReportData.RR_QTR_dE_75 != null && structReportData.RR_QTR_dE_110 != null && structReportData.RR_QTR_dL_Minus15 != null && structReportData.RR_QTR_dL_15 != null && structReportData.RR_QTR_dL_25 != null && structReportData.RR_QTR_dL_45 != null && structReportData.RR_QTR_dL_75 != null && structReportData.RR_QTR_dL_110 != null && structReportData.RR_QTR_da_Minus15 != null && structReportData.RR_QTR_da_15 != null && structReportData.RR_QTR_da_25 != null && structReportData.RR_QTR_da_45 != null && structReportData.RR_QTR_da_75 != null && structReportData.RR_QTR_da_110 != null && structReportData.RR_QTR_db_Minus15 != null && structReportData.RR_QTR_db_15 != null && structReportData.RR_QTR_db_25 != null && structReportData.RR_QTR_db_45 != null && structReportData.RR_QTR_db_75 != null && structReportData.RR_QTR_db_110 != null && structReportData.RR_QTR_L_Minus15 != null && structReportData.RR_QTR_L_15 != null && structReportData.RR_QTR_L_25 != null && structReportData.RR_QTR_L_45 != null && structReportData.RR_QTR_L_75 != null && structReportData.RR_QTR_L_110 != null && structReportData.RR_QTR_a_Minus15 != null && structReportData.RR_QTR_a_15 != null && structReportData.RR_QTR_a_25 != null && structReportData.RR_QTR_a_45 != null && structReportData.RR_QTR_a_75 != null && structReportData.RR_QTR_a_110 != null && structReportData.RR_QTR_b_Minus15 != null && structReportData.RR_QTR_b_15 != null && structReportData.RR_QTR_b_25 != null && structReportData.RR_QTR_b_45 != null && structReportData.RR_QTR_b_75 != null && structReportData.RR_QTR_b_110 != null && structReportData.RR_BUMPER_dE_Minus15 != null && structReportData.RR_BUMPER_dE_15 != null && structReportData.RR_BUMPER_dE_25 != null && structReportData.RR_BUMPER_dE_45 != null && structReportData.RR_BUMPER_dE_75 != null && structReportData.RR_BUMPER_dE_110 != null && structReportData.RR_BUMPER_dL_Minus15 != null && structReportData.RR_BUMPER_dL_15 != null && structReportData.RR_BUMPER_dL_25 != null && structReportData.RR_BUMPER_dL_45 != null && structReportData.RR_BUMPER_dL_75 != null && structReportData.RR_BUMPER_dL_110 != null && structReportData.RR_BUMPER_da_Minus15 != null && structReportData.RR_BUMPER_da_15 != null && structReportData.RR_BUMPER_da_25 != null && structReportData.RR_BUMPER_da_45 != null && structReportData.RR_BUMPER_da_75 != null && structReportData.RR_BUMPER_da_110 != null && structReportData.RR_BUMPER_db_Minus15 != null && structReportData.RR_BUMPER_db_15 != null && structReportData.RR_BUMPER_db_25 != null && structReportData.RR_BUMPER_db_45 != null && structReportData.RR_BUMPER_db_75 != null && structReportData.RR_BUMPER_db_110 != null && structReportData.RR_BUMPER_L_Minus15 != null && structReportData.RR_BUMPER_L_15 != null && structReportData.RR_BUMPER_L_25 != null && structReportData.RR_BUMPER_L_45 != null && structReportData.RR_BUMPER_L_75 != null && structReportData.RR_BUMPER_L_110 != null && structReportData.RR_BUMPER_a_Minus15 != null && structReportData.RR_BUMPER_a_15 != null && structReportData.RR_BUMPER_a_25 != null && structReportData.RR_BUMPER_a_45 != null && structReportData.RR_BUMPER_a_75 != null && structReportData.RR_BUMPER_a_110 != null && structReportData.RR_BUMPER_b_Minus15 != null && structReportData.RR_BUMPER_b_15 != null && structReportData.RR_BUMPER_b_25 != null && structReportData.RR_BUMPER_b_45 != null && structReportData.RR_BUMPER_b_75 != null && structReportData.RR_BUMPER_b_110 != null && structReportData.RR_QTR_dE_Minus15 != "" && structReportData.RR_QTR_dE_15 != "" && structReportData.RR_QTR_dE_25 != "" && structReportData.RR_QTR_dE_45 != "" && structReportData.RR_QTR_dE_75 != "" && structReportData.RR_QTR_dE_110 != "" && structReportData.RR_QTR_dL_Minus15 != "" && structReportData.RR_QTR_dL_15 != "" && structReportData.RR_QTR_dL_25 != "" && structReportData.RR_QTR_dL_45 != "" && structReportData.RR_QTR_dL_75 != "" && structReportData.RR_QTR_dL_110 != "" && structReportData.RR_QTR_da_Minus15 != "" && structReportData.RR_QTR_da_15 != "" && structReportData.RR_QTR_da_25 != "" && structReportData.RR_QTR_da_45 != "" && structReportData.RR_QTR_da_75 != "" && structReportData.RR_QTR_da_110 != "" && structReportData.RR_QTR_db_Minus15 != "" && structReportData.RR_QTR_db_15 != "" && structReportData.RR_QTR_db_25 != "" && structReportData.RR_QTR_db_45 != "" && structReportData.RR_QTR_db_75 != "" && structReportData.RR_QTR_db_110 != "" && structReportData.RR_QTR_L_Minus15 != "" && structReportData.RR_QTR_L_15 != "" && structReportData.RR_QTR_L_25 != "" && structReportData.RR_QTR_L_45 != "" && structReportData.RR_QTR_L_75 != "" && structReportData.RR_QTR_L_110 != "" && structReportData.RR_QTR_a_Minus15 != "" && structReportData.RR_QTR_a_15 != "" && structReportData.RR_QTR_a_25 != "" && structReportData.RR_QTR_a_45 != "" && structReportData.RR_QTR_a_75 != "" && structReportData.RR_QTR_a_110 != "" && structReportData.RR_QTR_b_Minus15 != "" && structReportData.RR_QTR_b_15 != "" && structReportData.RR_QTR_b_25 != "" && structReportData.RR_QTR_b_45 != "" && structReportData.RR_QTR_b_75 != "" && structReportData.RR_QTR_b_110 != "" && structReportData.RR_BUMPER_dE_Minus15 != "" && structReportData.RR_BUMPER_dE_15 != "" && structReportData.RR_BUMPER_dE_25 != "" && structReportData.RR_BUMPER_dE_45 != "" && structReportData.RR_BUMPER_dE_75 != "" && structReportData.RR_BUMPER_dE_110 != "" && structReportData.RR_BUMPER_dL_Minus15 != "" && structReportData.RR_BUMPER_dL_15 != "" && structReportData.RR_BUMPER_dL_25 != "" && structReportData.RR_BUMPER_dL_45 != "" && structReportData.RR_BUMPER_dL_75 != "" && structReportData.RR_BUMPER_dL_110 != "" && structReportData.RR_BUMPER_da_Minus15 != "" && structReportData.RR_BUMPER_da_15 != "" && structReportData.RR_BUMPER_da_25 != "" && structReportData.RR_BUMPER_da_45 != "" && structReportData.RR_BUMPER_da_75 != "" && structReportData.RR_BUMPER_da_110 != "" && structReportData.RR_BUMPER_db_Minus15 != "" && structReportData.RR_BUMPER_db_15 != "" && structReportData.RR_BUMPER_db_25 != "" && structReportData.RR_BUMPER_db_45 != "" && structReportData.RR_BUMPER_db_75 != "" && structReportData.RR_BUMPER_db_110 != "" && structReportData.RR_BUMPER_L_Minus15 != "" && structReportData.RR_BUMPER_L_15 != "" && structReportData.RR_BUMPER_L_25 != "" && structReportData.RR_BUMPER_L_45 != "" && structReportData.RR_BUMPER_L_75 != "" && structReportData.RR_BUMPER_L_110 != "" && structReportData.RR_BUMPER_a_Minus15 != "" && structReportData.RR_BUMPER_a_15 != "" && structReportData.RR_BUMPER_a_25 != "" && structReportData.RR_BUMPER_a_45 != "" && structReportData.RR_BUMPER_a_75 != "" && structReportData.RR_BUMPER_a_110 != "" && structReportData.RR_BUMPER_b_Minus15 != "" && structReportData.RR_BUMPER_b_15 != "" && structReportData.RR_BUMPER_b_25 != "" && structReportData.RR_BUMPER_b_45 != "" && structReportData.RR_BUMPER_b_75 != "" && structReportData.RR_BUMPER_b_110 != "")
                {
                    double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_QTR_a_Minus15), Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15));
                    double dE_15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_QTR_a_15), Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15), Convert.ToDouble(structReportData.RR_BUMPER_a_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15));
                    double dE_25 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_QTR_a_25), Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25), Convert.ToDouble(structReportData.RR_BUMPER_a_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25));
                    double dE_45 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45));
                    double dE_75 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_QTR_a_75), Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75), Convert.ToDouble(structReportData.RR_BUMPER_a_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75));
                    double dE_110 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_QTR_a_110), Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110), Convert.ToDouble(structReportData.RR_BUMPER_a_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110));

                    if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangeValue &&
                        dE_15 > (passRange.Rear_dE_15_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangeValue &&
                        dE_25 > (passRange.Rear_dE_25_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangeValue &&
                        dE_45 > (passRange.Rear_dE_45_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangeValue &&
                        dE_75 > (passRange.Rear_dE_75_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangeValue &&
                        dE_110 > (passRange.Rear_dE_110_PassRangeValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangeValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangeValue)
                    {
                        structReportData.RR_Result = "OK";
                    }
                    else
                    {
                        structReportData.RR_Result = "NG";
                    }
                }
                else
                {
                    if (iniManager.BlankIsOk)
                    {
                        structReportData.RR_Result = "OK";
                    }
                    else
                    {
                        structReportData.RR_Result = "NG";
                    }
                }
            }
            else
            {
                if (structReportData.RR_QTR_dE_Minus15 != null && structReportData.RR_QTR_dE_15 != null && structReportData.RR_QTR_dE_25 != null && structReportData.RR_QTR_dE_45 != null && structReportData.RR_QTR_dE_75 != null && structReportData.RR_QTR_dE_110 != null && structReportData.RR_QTR_dL_Minus15 != null && structReportData.RR_QTR_dL_15 != null && structReportData.RR_QTR_dL_25 != null && structReportData.RR_QTR_dL_45 != null && structReportData.RR_QTR_dL_75 != null && structReportData.RR_QTR_dL_110 != null && structReportData.RR_QTR_da_Minus15 != null && structReportData.RR_QTR_da_15 != null && structReportData.RR_QTR_da_25 != null && structReportData.RR_QTR_da_45 != null && structReportData.RR_QTR_da_75 != null && structReportData.RR_QTR_da_110 != null && structReportData.RR_QTR_db_Minus15 != null && structReportData.RR_QTR_db_15 != null && structReportData.RR_QTR_db_25 != null && structReportData.RR_QTR_db_45 != null && structReportData.RR_QTR_db_75 != null && structReportData.RR_QTR_db_110 != null && structReportData.RR_QTR_L_Minus15 != null && structReportData.RR_QTR_L_15 != null && structReportData.RR_QTR_L_25 != null && structReportData.RR_QTR_L_45 != null && structReportData.RR_QTR_L_75 != null && structReportData.RR_QTR_L_110 != null && structReportData.RR_QTR_a_Minus15 != null && structReportData.RR_QTR_a_15 != null && structReportData.RR_QTR_a_25 != null && structReportData.RR_QTR_a_45 != null && structReportData.RR_QTR_a_75 != null && structReportData.RR_QTR_a_110 != null && structReportData.RR_QTR_b_Minus15 != null && structReportData.RR_QTR_b_15 != null && structReportData.RR_QTR_b_25 != null && structReportData.RR_QTR_b_45 != null && structReportData.RR_QTR_b_75 != null && structReportData.RR_QTR_b_110 != null && structReportData.RR_BUMPER_dE_Minus15 != null && structReportData.RR_BUMPER_dE_15 != null && structReportData.RR_BUMPER_dE_25 != null && structReportData.RR_BUMPER_dE_45 != null && structReportData.RR_BUMPER_dE_75 != null && structReportData.RR_BUMPER_dE_110 != null && structReportData.RR_BUMPER_dL_Minus15 != null && structReportData.RR_BUMPER_dL_15 != null && structReportData.RR_BUMPER_dL_25 != null && structReportData.RR_BUMPER_dL_45 != null && structReportData.RR_BUMPER_dL_75 != null && structReportData.RR_BUMPER_dL_110 != null && structReportData.RR_BUMPER_da_Minus15 != null && structReportData.RR_BUMPER_da_15 != null && structReportData.RR_BUMPER_da_25 != null && structReportData.RR_BUMPER_da_45 != null && structReportData.RR_BUMPER_da_75 != null && structReportData.RR_BUMPER_da_110 != null && structReportData.RR_BUMPER_db_Minus15 != null && structReportData.RR_BUMPER_db_15 != null && structReportData.RR_BUMPER_db_25 != null && structReportData.RR_BUMPER_db_45 != null && structReportData.RR_BUMPER_db_75 != null && structReportData.RR_BUMPER_db_110 != null && structReportData.RR_BUMPER_L_Minus15 != null && structReportData.RR_BUMPER_L_15 != null && structReportData.RR_BUMPER_L_25 != null && structReportData.RR_BUMPER_L_45 != null && structReportData.RR_BUMPER_L_75 != null && structReportData.RR_BUMPER_L_110 != null && structReportData.RR_BUMPER_a_Minus15 != null && structReportData.RR_BUMPER_a_15 != null && structReportData.RR_BUMPER_a_25 != null && structReportData.RR_BUMPER_a_45 != null && structReportData.RR_BUMPER_a_75 != null && structReportData.RR_BUMPER_a_110 != null && structReportData.RR_BUMPER_b_Minus15 != null && structReportData.RR_BUMPER_b_15 != null && structReportData.RR_BUMPER_b_25 != null && structReportData.RR_BUMPER_b_45 != null && structReportData.RR_BUMPER_b_75 != null && structReportData.RR_BUMPER_b_110 != null && structReportData.RR_QTR_dE_Minus15 != "" && structReportData.RR_QTR_dE_15 != "" && structReportData.RR_QTR_dE_25 != "" && structReportData.RR_QTR_dE_45 != "" && structReportData.RR_QTR_dE_75 != "" && structReportData.RR_QTR_dE_110 != "" && structReportData.RR_QTR_dL_Minus15 != "" && structReportData.RR_QTR_dL_15 != "" && structReportData.RR_QTR_dL_25 != "" && structReportData.RR_QTR_dL_45 != "" && structReportData.RR_QTR_dL_75 != "" && structReportData.RR_QTR_dL_110 != "" && structReportData.RR_QTR_da_Minus15 != "" && structReportData.RR_QTR_da_15 != "" && structReportData.RR_QTR_da_25 != "" && structReportData.RR_QTR_da_45 != "" && structReportData.RR_QTR_da_75 != "" && structReportData.RR_QTR_da_110 != "" && structReportData.RR_QTR_db_Minus15 != "" && structReportData.RR_QTR_db_15 != "" && structReportData.RR_QTR_db_25 != "" && structReportData.RR_QTR_db_45 != "" && structReportData.RR_QTR_db_75 != "" && structReportData.RR_QTR_db_110 != "" && structReportData.RR_QTR_L_Minus15 != "" && structReportData.RR_QTR_L_15 != "" && structReportData.RR_QTR_L_25 != "" && structReportData.RR_QTR_L_45 != "" && structReportData.RR_QTR_L_75 != "" && structReportData.RR_QTR_L_110 != "" && structReportData.RR_QTR_a_Minus15 != "" && structReportData.RR_QTR_a_15 != "" && structReportData.RR_QTR_a_25 != "" && structReportData.RR_QTR_a_45 != "" && structReportData.RR_QTR_a_75 != "" && structReportData.RR_QTR_a_110 != "" && structReportData.RR_QTR_b_Minus15 != "" && structReportData.RR_QTR_b_15 != "" && structReportData.RR_QTR_b_25 != "" && structReportData.RR_QTR_b_45 != "" && structReportData.RR_QTR_b_75 != "" && structReportData.RR_QTR_b_110 != "" && structReportData.RR_BUMPER_dE_Minus15 != "" && structReportData.RR_BUMPER_dE_15 != "" && structReportData.RR_BUMPER_dE_25 != "" && structReportData.RR_BUMPER_dE_45 != "" && structReportData.RR_BUMPER_dE_75 != "" && structReportData.RR_BUMPER_dE_110 != "" && structReportData.RR_BUMPER_dL_Minus15 != "" && structReportData.RR_BUMPER_dL_15 != "" && structReportData.RR_BUMPER_dL_25 != "" && structReportData.RR_BUMPER_dL_45 != "" && structReportData.RR_BUMPER_dL_75 != "" && structReportData.RR_BUMPER_dL_110 != "" && structReportData.RR_BUMPER_da_Minus15 != "" && structReportData.RR_BUMPER_da_15 != "" && structReportData.RR_BUMPER_da_25 != "" && structReportData.RR_BUMPER_da_45 != "" && structReportData.RR_BUMPER_da_75 != "" && structReportData.RR_BUMPER_da_110 != "" && structReportData.RR_BUMPER_db_Minus15 != "" && structReportData.RR_BUMPER_db_15 != "" && structReportData.RR_BUMPER_db_25 != "" && structReportData.RR_BUMPER_db_45 != "" && structReportData.RR_BUMPER_db_75 != "" && structReportData.RR_BUMPER_db_110 != "" && structReportData.RR_BUMPER_L_Minus15 != "" && structReportData.RR_BUMPER_L_15 != "" && structReportData.RR_BUMPER_L_25 != "" && structReportData.RR_BUMPER_L_45 != "" && structReportData.RR_BUMPER_L_75 != "" && structReportData.RR_BUMPER_L_110 != "" && structReportData.RR_BUMPER_a_Minus15 != "" && structReportData.RR_BUMPER_a_15 != "" && structReportData.RR_BUMPER_a_25 != "" && structReportData.RR_BUMPER_a_45 != "" && structReportData.RR_BUMPER_a_75 != "" && structReportData.RR_BUMPER_a_110 != "" && structReportData.RR_BUMPER_b_Minus15 != "" && structReportData.RR_BUMPER_b_15 != "" && structReportData.RR_BUMPER_b_25 != "" && structReportData.RR_BUMPER_b_45 != "" && structReportData.RR_BUMPER_b_75 != "" && structReportData.RR_BUMPER_b_110 != "")
                {
                    double dE_Minus15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_QTR_a_Minus15), Convert.ToDouble(structReportData.RR_QTR_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15), Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15));
                    double dE_15 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_QTR_a_15), Convert.ToDouble(structReportData.RR_QTR_L_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15), Convert.ToDouble(structReportData.RR_BUMPER_a_15), Convert.ToDouble(structReportData.RR_BUMPER_L_15));
                    double dE_25 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_QTR_a_25), Convert.ToDouble(structReportData.RR_QTR_L_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25), Convert.ToDouble(structReportData.RR_BUMPER_a_25), Convert.ToDouble(structReportData.RR_BUMPER_L_25));
                    double dE_45 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_QTR_a_45), Convert.ToDouble(structReportData.RR_QTR_L_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45), Convert.ToDouble(structReportData.RR_BUMPER_a_45), Convert.ToDouble(structReportData.RR_BUMPER_L_45));
                    double dE_75 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_QTR_a_75), Convert.ToDouble(structReportData.RR_QTR_L_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75), Convert.ToDouble(structReportData.RR_BUMPER_a_75), Convert.ToDouble(structReportData.RR_BUMPER_L_75));
                    double dE_110 = CalcDelta(Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_QTR_a_110), Convert.ToDouble(structReportData.RR_QTR_L_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110), Convert.ToDouble(structReportData.RR_BUMPER_a_110), Convert.ToDouble(structReportData.RR_BUMPER_L_110));

                    if (dE_Minus15 > (passRange.Rear_dE_Minus15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_Minus15_PassRangePlusValue &&
                        dE_15 > (passRange.Rear_dE_15_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_15_PassRangePlusValue &&
                        dE_25 > (passRange.Rear_dE_25_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_25_PassRangePlusValue &&
                        dE_45 > (passRange.Rear_dE_45_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_45_PassRangePlusValue &&
                        dE_75 > (passRange.Rear_dE_75_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_75_PassRangePlusValue &&
                        dE_110 > (passRange.Rear_dE_110_PassRangeMinusValue * -1) && dE_Minus15 < passRange.Rear_dE_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) > (passRange.Rear_dL_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_L_Minus15)) < passRange.Rear_dL_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) > (passRange.Rear_da_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_a_Minus15)) < passRange.Rear_da_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) > (passRange.Rear_db_Minus15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_Minus15) - Convert.ToDouble(structReportData.RR_BUMPER_b_Minus15)) < passRange.Rear_db_Minus15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) > (passRange.Rear_dL_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_15) - Convert.ToDouble(structReportData.RR_BUMPER_L_15)) < passRange.Rear_dL_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) > (passRange.Rear_da_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_15) - Convert.ToDouble(structReportData.RR_BUMPER_a_15)) < passRange.Rear_da_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) > (passRange.Rear_db_15_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_15) - Convert.ToDouble(structReportData.RR_BUMPER_b_15)) < passRange.Rear_db_15_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) > (passRange.Rear_dL_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_25) - Convert.ToDouble(structReportData.RR_BUMPER_L_25)) < passRange.Rear_dL_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) > (passRange.Rear_da_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_25) - Convert.ToDouble(structReportData.RR_BUMPER_a_25)) < passRange.Rear_da_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) > (passRange.Rear_db_25_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_25) - Convert.ToDouble(structReportData.RR_BUMPER_b_25)) < passRange.Rear_db_25_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) > (passRange.Rear_dL_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_45) - Convert.ToDouble(structReportData.RR_BUMPER_L_45)) < passRange.Rear_dL_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) > (passRange.Rear_da_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_45) - Convert.ToDouble(structReportData.RR_BUMPER_a_45)) < passRange.Rear_da_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) > (passRange.Rear_db_45_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_45) - Convert.ToDouble(structReportData.RR_BUMPER_b_45)) < passRange.Rear_db_45_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) > (passRange.Rear_dL_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_75) - Convert.ToDouble(structReportData.RR_BUMPER_L_75)) < passRange.Rear_dL_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) > (passRange.Rear_da_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_75) - Convert.ToDouble(structReportData.RR_BUMPER_a_75)) < passRange.Rear_da_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) > (passRange.Rear_db_75_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_75) - Convert.ToDouble(structReportData.RR_BUMPER_b_75)) < passRange.Rear_db_75_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) > (passRange.Rear_dL_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_L_110) - Convert.ToDouble(structReportData.RR_BUMPER_L_110)) < passRange.Rear_dL_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) > (passRange.Rear_da_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_a_110) - Convert.ToDouble(structReportData.RR_BUMPER_a_110)) < passRange.Rear_da_110_PassRangePlusValue &&
                        (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) > (passRange.Rear_db_110_PassRangeMinusValue * -1) && (Convert.ToDouble(structReportData.RR_QTR_b_110) - Convert.ToDouble(structReportData.RR_BUMPER_b_110)) < passRange.Rear_db_110_PassRangePlusValue)
                    {
                        structReportData.RR_Result = "OK";
                    }
                    else
                    {
                        structReportData.RR_Result = "NG";
                    }
                }
                else
                {
                    if (iniManager.BlankIsOk)
                    {
                        structReportData.RR_Result = "OK";
                    }
                    else
                    {
                        structReportData.RR_Result = "NG";
                    }
                }
            }

            if (structReportData.FR_Result == "OK" && structReportData.RR_Result == "OK")
            {
                structReportData.Result = "OK";
            }
            else if (structReportData.FR_Result == "NG" || structReportData.RR_Result == "NG")
            {
                structReportData.Result = "NG";
            }

            bool isChecked = false;

            if (isChecked)
            {
                DateTime tmpStartDateTime = new DateTime(this.startDateTime.Year, this.startDateTime.Month, this.startDateTime.Day, startHour, startMinute, 0);
                DateTime tmpEndDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endHour, endMinute, 59);

                if ((selectedCarKind == "ALL" || structReportData.Model.Contains(selectedCarKind)) && (selectedColor == "ALL" || structReportData.Color.Contains(selectedColor)) && structReportData.BodyNumber.ToUpper().Contains(searchString.ToUpper()))
                {
                    if (tmpStartDateTime < structReportData.StartDateTime && tmpEndDateTime > structReportData.StartDateTime)
                    {
                        

                        /*
                        window.Dispatcher.Invoke(() =>
                        {
                            list.Add(structReportData);
                        });
                        */
                    }
                }
            }
            else
            {
                /*
                if (list.Count > 0 && list[find_index].BodyNumber == structReportData.BodyNumber)
                {
                    DateTime tmpStartDateTime = new DateTime(this.startDateTime.Year, this.startDateTime.Month, this.startDateTime.Day, startHour, startMinute, 0);
                    DateTime tmpEndDateTime = new DateTime(endDateTime.Year, endDateTime.Month, endDateTime.Day, endHour, endMinute, 59);

                    if ((selectedCarKind == "ALL" || list[find_index].Model.Contains(selectedCarKind)) && (selectedColor == "ALL" || list[find_index].Color.Contains(selectedColor)) && list[find_index].BodyNumber.ToUpper().Contains(searchString.ToUpper()))
                    {
                        if (tmpStartDateTime < list[find_index].StartDateTime && tmpEndDateTime > list[find_index].StartDateTime)
                        {
                            double front_delta = 0;
                            double rear_delta = 0;

                            try
                            {
                                front_delta = CalcDelta(Convert.ToDouble(list[find_index].FR_FENDA_L_45), Convert.ToDouble(list[find_index].FR_FENDA_a_45), Convert.ToDouble(list[find_index].FR_FENDA_b_45), Convert.ToDouble(list[find_index].FR_BUMPER_L_45), Convert.ToDouble(list[find_index].FR_BUMPER_a_45), Convert.ToDouble(list[find_index].FR_BUMPER_b_45));

                                rear_delta = CalcDelta(Convert.ToDouble(list[find_index].RR_QTR_L_45), Convert.ToDouble(list[find_index].RR_QTR_a_45), Convert.ToDouble(list[find_index].RR_QTR_b_45), Convert.ToDouble(list[find_index].RR_BUMPER_L_45), Convert.ToDouble(list[find_index].RR_BUMPER_a_45), Convert.ToDouble(list[find_index].RR_BUMPER_b_45));

                                list[find_index].FR_DELTA = Convert.ToString(Math.Round(front_delta, 2));
                                list[find_index].RR_DELTA = Convert.ToString(Math.Round(rear_delta, 2));
                            }
                            catch (Exception ex)
                            {
                                list[find_index].FR_DELTA = "0";
                                list[find_index].RR_DELTA = "0";
                            }


                            StructPassRange structPassRange = new StructPassRange();
                            List<StructPassRange> passRangeList = LoadPassRangeData();
                            StructPassRange passRange = passRangeList.Find(x => x.Color.Trim() == list[find_index].Color.Trim());

                            if (passRange == null)
                            {
                                string content = File.ReadAllText(passRangeFilePath);
                                content += list[find_index].Color.Trim() + ",0,0,0,0" + Environment.NewLine;
                                File.WriteAllText(passRangeFilePath, content);
                                passRangeList = LoadPassRangeData();
                                passRange = passRangeList.Find(x => x.Color.Trim() == list[find_index].Color.Trim());
                            }

                            double frontMin = passRange.FrontBaseValue - passRange.FrontPassRangeValue;
                            double frontMax = passRange.FrontBaseValue + passRange.FrontPassRangeValue;
                            double rearMin = passRange.RearBaseValue - passRange.RearPassRangeValue;
                            double rearMax = passRange.RearBaseValue + passRange.RearPassRangeValue;

                            if (list[find_index].FR_FENDA_L_45 != null && list[find_index].FR_FENDA_a_45 != null && list[find_index].FR_FENDA_b_45 != null && list[find_index].FR_BUMPER_L_45 != null && list[find_index].FR_BUMPER_a_45 != null && list[find_index].FR_BUMPER_b_45 != null && list[find_index].FR_FENDA_L_45 != "" && list[find_index].FR_FENDA_a_45 != "" && list[find_index].FR_FENDA_b_45 != "" && list[find_index].FR_BUMPER_L_45 != "" && list[find_index].FR_BUMPER_a_45 != "" && list[find_index].FR_BUMPER_b_45 != "")
                            {
                                if (frontMin <= front_delta && frontMax >= front_delta)
                                {
                                    list[find_index].FR_Result = "OK";
                                }
                                else
                                {
                                    list[find_index].FR_Result = "NG";
                                }
                            }
                            else
                            {
                                list[find_index].FR_Result = "NG";
                            }

                            if (list[find_index].RR_QTR_L_45 != null && list[find_index].RR_QTR_a_45 != null && list[find_index].RR_QTR_b_45 != null && list[find_index].RR_BUMPER_L_45 != null && list[find_index].RR_BUMPER_a_45 != null && list[find_index].RR_BUMPER_b_45 != null && list[find_index].RR_QTR_L_45 != "" && list[find_index].RR_QTR_a_45 != "" && list[find_index].RR_QTR_b_45 != "" && list[find_index].RR_BUMPER_L_45 != "" && list[find_index].RR_BUMPER_a_45 != "" && list[find_index].RR_BUMPER_b_45 != "")
                            {
                                if (rearMin <= rear_delta && rearMax >= rear_delta)
                                {
                                    list[find_index].RR_Result = "OK";
                                }
                                else
                                {
                                    list[find_index].RR_Result = "NG";
                                }
                            }
                            else
                            {
                                list[find_index].RR_Result = "NG";
                            }

                            if (list[find_index].FR_Result == "OK" && list[find_index].RR_Result == "OK")
                            {
                                list[find_index].Result = "OK";
                            }
                            else if (list[find_index].FR_Result == "NG" || list[find_index].RR_Result == "NG")
                            {
                                list[find_index].Result = "NG";
                            }
                        }
                    }
                }
                */
            }
        }

        public async Task ShowMessageDialogAsync(string title, string message)
        {
            await dialogCoordinator.ShowMessageAsync(this, title, message);
        }

        public async Task<MessageDialogResult> ShowMessageDialogAsync(string title, string message, MessageDialogStyle style, MetroDialogSettings settings)
        {
            return await dialogCoordinator.ShowMessageAsync(this, title, message, style: style, settings: settings);
        }

        public void Window_Closing(object sender, CancelEventArgs e)
        {
            MetroDialogSettings settings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "확인",
                NegativeButtonText = "취소",
                AnimateShow = true,
                AnimateHide = false,
                DialogMessageFontSize = 14
            };

            MessageDialogResult result = window.ShowModalMessageExternal("확인", "프로그램 종료 시 OK 라인 연계 데이터 송신과 E-FOREST DB 데이터 저장을 중지합니다. 그래도 종료하시겠습니까?", MessageDialogStyle.AffirmativeAndNegative, settings);

            if (result == MessageDialogResult.Affirmative)
            {
                isRunning = false;
                // dbManager.Close();
            }
            else
            {
                e.Cancel = true;
            }
        }

        private string passRangeFilePath = Environment.CurrentDirectory + "\\PassRange.hsr";
        private string carKindFilePath = Environment.CurrentDirectory + "\\CarKindList.hsr";

        private List<StructPassRange> LoadPassRangeData()
        {
            List<StructPassRange> list = new List<StructPassRange>();

            string[] lines = File.ReadAllLines(passRangeFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] split = line.Split(',');

                StructPassRange passRange = new StructPassRange();

                if (split.Length > 4)
                {
                    if (split[0] != null)
                    {
                        passRange.Color = split[0];
                    }

                    if (split[1] != null)
                    {
                        passRange.Front_dE_Minus15_PassRangeValue = Convert.ToDouble(split[1]);
                    }

                    if (split[2] != null)
                    {
                        passRange.Front_dE_Minus15_PassRangeMinusValue = Convert.ToDouble(split[2]);
                    }

                    if (split[3] != null)
                    {
                        passRange.Front_dE_Minus15_PassRangePlusValue = Convert.ToDouble(split[3]);
                    }

                    if (split[4] != null)
                    {
                        passRange.Front_dL_Minus15_PassRangeValue = Convert.ToDouble(split[4]);
                    }

                    if (split[5] != null)
                    {
                        passRange.Front_dL_Minus15_PassRangeMinusValue = Convert.ToDouble(split[5]);
                    }

                    if (split[6] != null)
                    {
                        passRange.Front_dL_Minus15_PassRangePlusValue = Convert.ToDouble(split[6]);
                    }

                    if (split[7] != null)
                    {
                        passRange.Front_da_Minus15_PassRangeValue = Convert.ToDouble(split[7]);
                    }

                    if (split[8] != null)
                    {
                        passRange.Front_da_Minus15_PassRangeMinusValue = Convert.ToDouble(split[8]);
                    }

                    if (split[9] != null)
                    {
                        passRange.Front_da_Minus15_PassRangePlusValue = Convert.ToDouble(split[9]);
                    }

                    if (split[10] != null)
                    {
                        passRange.Front_db_Minus15_PassRangeValue = Convert.ToDouble(split[10]);
                    }

                    if (split[11] != null)
                    {
                        passRange.Front_db_Minus15_PassRangeMinusValue = Convert.ToDouble(split[11]);
                    }

                    if (split[12] != null)
                    {
                        passRange.Front_db_Minus15_PassRangePlusValue = Convert.ToDouble(split[12]);
                    }

                    if (split[13] != null)
                    {
                        passRange.Front_dE_15_PassRangeValue = Convert.ToDouble(split[13]);
                    }

                    if (split[14] != null)
                    {
                        passRange.Front_dE_15_PassRangeMinusValue = Convert.ToDouble(split[14]);
                    }

                    if (split[15] != null)
                    {
                        passRange.Front_dE_15_PassRangePlusValue = Convert.ToDouble(split[15]);
                    }

                    if (split[16] != null)
                    {
                        passRange.Front_dL_15_PassRangeValue = Convert.ToDouble(split[16]);
                    }

                    if (split[17] != null)
                    {
                        passRange.Front_dL_15_PassRangeMinusValue = Convert.ToDouble(split[17]);
                    }

                    if (split[18] != null)
                    {
                        passRange.Front_dL_15_PassRangePlusValue = Convert.ToDouble(split[18]);
                    }

                    if (split[19] != null)
                    {
                        passRange.Front_da_15_PassRangeValue = Convert.ToDouble(split[19]);
                    }

                    if (split[20] != null)
                    {
                        passRange.Front_da_15_PassRangeMinusValue = Convert.ToDouble(split[20]);
                    }

                    if (split[21] != null)
                    {
                        passRange.Front_da_15_PassRangePlusValue = Convert.ToDouble(split[21]);
                    }

                    if (split[22] != null)
                    {
                        passRange.Front_db_15_PassRangeValue = Convert.ToDouble(split[22]);
                    }

                    if (split[23] != null)
                    {
                        passRange.Front_db_15_PassRangeMinusValue = Convert.ToDouble(split[23]);
                    }

                    if (split[24] != null)
                    {
                        passRange.Front_db_15_PassRangePlusValue = Convert.ToDouble(split[24]);
                    }

                    if (split[25] != null)
                    {
                        passRange.Front_dE_25_PassRangeValue = Convert.ToDouble(split[25]);
                    }

                    if (split[26] != null)
                    {
                        passRange.Front_dE_25_PassRangeMinusValue = Convert.ToDouble(split[26]);
                    }

                    if (split[27] != null)
                    {
                        passRange.Front_dE_25_PassRangePlusValue = Convert.ToDouble(split[27]);
                    }

                    if (split[28] != null)
                    {
                        passRange.Front_dL_25_PassRangeValue = Convert.ToDouble(split[28]);
                    }

                    if (split[29] != null)
                    {
                        passRange.Front_dL_25_PassRangeMinusValue = Convert.ToDouble(split[29]);
                    }

                    if (split[30] != null)
                    {
                        passRange.Front_dL_25_PassRangePlusValue = Convert.ToDouble(split[30]);
                    }

                    if (split[31] != null)
                    {
                        passRange.Front_da_25_PassRangeValue = Convert.ToDouble(split[31]);
                    }

                    if (split[32] != null)
                    {
                        passRange.Front_da_25_PassRangeMinusValue = Convert.ToDouble(split[32]);
                    }

                    if (split[33] != null)
                    {
                        passRange.Front_da_25_PassRangePlusValue = Convert.ToDouble(split[33]);
                    }

                    if (split[34] != null)
                    {
                        passRange.Front_db_25_PassRangeValue = Convert.ToDouble(split[34]);
                    }

                    if (split[35] != null)
                    {
                        passRange.Front_db_25_PassRangeMinusValue = Convert.ToDouble(split[35]);
                    }

                    if (split[36] != null)
                    {
                        passRange.Front_db_25_PassRangePlusValue = Convert.ToDouble(split[36]);
                    }

                    if (split[37] != null)
                    {
                        passRange.Front_dE_45_PassRangeValue = Convert.ToDouble(split[37]);
                    }

                    if (split[38] != null)
                    {
                        passRange.Front_dE_45_PassRangeMinusValue = Convert.ToDouble(split[38]);
                    }

                    if (split[39] != null)
                    {
                        passRange.Front_dE_45_PassRangePlusValue = Convert.ToDouble(split[39]);
                    }

                    if (split[40] != null)
                    {
                        passRange.Front_dL_45_PassRangeValue = Convert.ToDouble(split[40]);
                    }

                    if (split[41] != null)
                    {
                        passRange.Front_dL_45_PassRangeMinusValue = Convert.ToDouble(split[41]);
                    }

                    if (split[42] != null)
                    {
                        passRange.Front_dL_45_PassRangePlusValue = Convert.ToDouble(split[42]);
                    }

                    if (split[43] != null)
                    {
                        passRange.Front_da_45_PassRangeValue = Convert.ToDouble(split[43]);
                    }

                    if (split[44] != null)
                    {
                        passRange.Front_da_45_PassRangeMinusValue = Convert.ToDouble(split[44]);
                    }

                    if (split[45] != null)
                    {
                        passRange.Front_da_45_PassRangePlusValue = Convert.ToDouble(split[45]);
                    }

                    if (split[46] != null)
                    {
                        passRange.Front_db_45_PassRangeValue = Convert.ToDouble(split[46]);
                    }

                    if (split[47] != null)
                    {
                        passRange.Front_db_45_PassRangeMinusValue = Convert.ToDouble(split[47]);
                    }

                    if (split[48] != null)
                    {
                        passRange.Front_db_45_PassRangePlusValue = Convert.ToDouble(split[48]);
                    }

                    if (split[49] != null)
                    {
                        passRange.Front_dE_75_PassRangeValue = Convert.ToDouble(split[49]);
                    }

                    if (split[50] != null)
                    {
                        passRange.Front_dE_75_PassRangeMinusValue = Convert.ToDouble(split[50]);
                    }

                    if (split[51] != null)
                    {
                        passRange.Front_dE_75_PassRangePlusValue = Convert.ToDouble(split[51]);
                    }

                    if (split[52] != null)
                    {
                        passRange.Front_dL_75_PassRangeValue = Convert.ToDouble(split[52]);
                    }

                    if (split[53] != null)
                    {
                        passRange.Front_dL_75_PassRangeMinusValue = Convert.ToDouble(split[53]);
                    }

                    if (split[54] != null)
                    {
                        passRange.Front_dL_75_PassRangePlusValue = Convert.ToDouble(split[54]);
                    }

                    if (split[55] != null)
                    {
                        passRange.Front_da_75_PassRangeValue = Convert.ToDouble(split[55]);
                    }

                    if (split[56] != null)
                    {
                        passRange.Front_da_75_PassRangeMinusValue = Convert.ToDouble(split[56]);
                    }

                    if (split[57] != null)
                    {
                        passRange.Front_da_75_PassRangePlusValue = Convert.ToDouble(split[57]);
                    }

                    if (split[58] != null)
                    {
                        passRange.Front_db_75_PassRangeValue = Convert.ToDouble(split[58]);
                    }

                    if (split[59] != null)
                    {
                        passRange.Front_db_75_PassRangeMinusValue = Convert.ToDouble(split[59]);
                    }

                    if (split[60] != null)
                    {
                        passRange.Front_db_75_PassRangePlusValue = Convert.ToDouble(split[60]);
                    }

                    if (split[61] != null)
                    {
                        passRange.Front_dE_110_PassRangeValue = Convert.ToDouble(split[61]);
                    }

                    if (split[62] != null)
                    {
                        passRange.Front_dE_110_PassRangeMinusValue = Convert.ToDouble(split[62]);
                    }

                    if (split[63] != null)
                    {
                        passRange.Front_dE_110_PassRangePlusValue = Convert.ToDouble(split[63]);
                    }

                    if (split[64] != null)
                    {
                        passRange.Front_dL_110_PassRangeValue = Convert.ToDouble(split[64]);
                    }

                    if (split[65] != null)
                    {
                        passRange.Front_dL_110_PassRangeMinusValue = Convert.ToDouble(split[65]);
                    }

                    if (split[66] != null)
                    {
                        passRange.Front_dL_110_PassRangePlusValue = Convert.ToDouble(split[66]);
                    }

                    if (split[67] != null)
                    {
                        passRange.Front_da_110_PassRangeValue = Convert.ToDouble(split[67]);
                    }

                    if (split[68] != null)
                    {
                        passRange.Front_da_110_PassRangeMinusValue = Convert.ToDouble(split[68]);
                    }

                    if (split[69] != null)
                    {
                        passRange.Front_da_110_PassRangePlusValue = Convert.ToDouble(split[69]);
                    }

                    if (split[70] != null)
                    {
                        passRange.Front_db_110_PassRangeValue = Convert.ToDouble(split[70]);
                    }

                    if (split[71] != null)
                    {
                        passRange.Front_db_110_PassRangeMinusValue = Convert.ToDouble(split[71]);
                    }

                    if (split[72] != null)
                    {
                        passRange.Front_db_110_PassRangePlusValue = Convert.ToDouble(split[72]);
                    }

                    if (split[73] != null)
                    {
                        passRange.Rear_dE_Minus15_PassRangeValue = Convert.ToDouble(split[73]);
                    }

                    if (split[74] != null)
                    {
                        passRange.Rear_dE_Minus15_PassRangeMinusValue = Convert.ToDouble(split[74]);
                    }

                    if (split[75] != null)
                    {
                        passRange.Rear_dE_Minus15_PassRangePlusValue = Convert.ToDouble(split[75]);
                    }

                    if (split[76] != null)
                    {
                        passRange.Rear_dL_Minus15_PassRangeValue = Convert.ToDouble(split[76]);
                    }

                    if (split[77] != null)
                    {
                        passRange.Rear_dL_Minus15_PassRangeMinusValue = Convert.ToDouble(split[77]);
                    }

                    if (split[78] != null)
                    {
                        passRange.Rear_dL_Minus15_PassRangePlusValue = Convert.ToDouble(split[78]);
                    }

                    if (split[79] != null)
                    {
                        passRange.Rear_da_Minus15_PassRangeValue = Convert.ToDouble(split[79]);
                    }

                    if (split[80] != null)
                    {
                        passRange.Rear_da_Minus15_PassRangeMinusValue = Convert.ToDouble(split[80]);
                    }

                    if (split[81] != null)
                    {
                        passRange.Rear_da_Minus15_PassRangePlusValue = Convert.ToDouble(split[81]);
                    }

                    if (split[82] != null)
                    {
                        passRange.Rear_db_Minus15_PassRangeValue = Convert.ToDouble(split[82]);
                    }

                    if (split[83] != null)
                    {
                        passRange.Rear_db_Minus15_PassRangeMinusValue = Convert.ToDouble(split[83]);
                    }

                    if (split[84] != null)
                    {
                        passRange.Rear_db_Minus15_PassRangePlusValue = Convert.ToDouble(split[84]);
                    }

                    if (split[85] != null)
                    {
                        passRange.Rear_dE_15_PassRangeValue = Convert.ToDouble(split[85]);
                    }

                    if (split[86] != null)
                    {
                        passRange.Rear_dE_15_PassRangeMinusValue = Convert.ToDouble(split[86]);
                    }

                    if (split[87] != null)
                    {
                        passRange.Rear_dE_15_PassRangePlusValue = Convert.ToDouble(split[87]);
                    }

                    if (split[88] != null)
                    {
                        passRange.Rear_dL_15_PassRangeValue = Convert.ToDouble(split[88]);
                    }

                    if (split[89] != null)
                    {
                        passRange.Rear_dL_15_PassRangeMinusValue = Convert.ToDouble(split[89]);
                    }

                    if (split[90] != null)
                    {
                        passRange.Rear_dL_15_PassRangePlusValue = Convert.ToDouble(split[90]);
                    }

                    if (split[91] != null)
                    {
                        passRange.Rear_da_15_PassRangeValue = Convert.ToDouble(split[91]);
                    }

                    if (split[92] != null)
                    {
                        passRange.Rear_da_15_PassRangeMinusValue = Convert.ToDouble(split[92]);
                    }

                    if (split[93] != null)
                    {
                        passRange.Rear_da_15_PassRangePlusValue = Convert.ToDouble(split[93]);
                    }

                    if (split[94] != null)
                    {
                        passRange.Rear_db_15_PassRangeValue = Convert.ToDouble(split[94]);
                    }

                    if (split[95] != null)
                    {
                        passRange.Rear_db_15_PassRangeMinusValue = Convert.ToDouble(split[95]);
                    }

                    if (split[96] != null)
                    {
                        passRange.Rear_db_15_PassRangePlusValue = Convert.ToDouble(split[96]);
                    }

                    if (split[97] != null)
                    {
                        passRange.Rear_dE_25_PassRangeValue = Convert.ToDouble(split[97]);
                    }

                    if (split[98] != null)
                    {
                        passRange.Rear_dE_25_PassRangeMinusValue = Convert.ToDouble(split[98]);
                    }

                    if (split[99] != null)
                    {
                        passRange.Rear_dE_25_PassRangePlusValue = Convert.ToDouble(split[99]);
                    }

                    if (split[100] != null)
                    {
                        passRange.Rear_dL_25_PassRangeValue = Convert.ToDouble(split[100]);
                    }

                    if (split[101] != null)
                    {
                        passRange.Rear_dL_25_PassRangeMinusValue = Convert.ToDouble(split[101]);
                    }

                    if (split[102] != null)
                    {
                        passRange.Rear_dL_25_PassRangePlusValue = Convert.ToDouble(split[102]);
                    }

                    if (split[103] != null)
                    {
                        passRange.Rear_da_25_PassRangeValue = Convert.ToDouble(split[103]);
                    }

                    if (split[104] != null)
                    {
                        passRange.Rear_da_25_PassRangeMinusValue = Convert.ToDouble(split[104]);
                    }

                    if (split[105] != null)
                    {
                        passRange.Rear_da_25_PassRangePlusValue = Convert.ToDouble(split[105]);
                    }

                    if (split[106] != null)
                    {
                        passRange.Rear_db_25_PassRangeValue = Convert.ToDouble(split[106]);
                    }

                    if (split[107] != null)
                    {
                        passRange.Rear_db_25_PassRangeMinusValue = Convert.ToDouble(split[107]);
                    }

                    if (split[108] != null)
                    {
                        passRange.Rear_db_25_PassRangePlusValue = Convert.ToDouble(split[108]);
                    }

                    if (split[109] != null)
                    {
                        passRange.Rear_dE_45_PassRangeValue = Convert.ToDouble(split[109]);
                    }

                    if (split[110] != null)
                    {
                        passRange.Rear_dE_45_PassRangeMinusValue = Convert.ToDouble(split[110]);
                    }

                    if (split[111] != null)
                    {
                        passRange.Rear_dE_45_PassRangePlusValue = Convert.ToDouble(split[111]);
                    }

                    if (split[112] != null)
                    {
                        passRange.Rear_dL_45_PassRangeValue = Convert.ToDouble(split[112]);
                    }

                    if (split[113] != null)
                    {
                        passRange.Rear_dL_45_PassRangeMinusValue = Convert.ToDouble(split[113]);
                    }

                    if (split[114] != null)
                    {
                        passRange.Rear_dL_45_PassRangePlusValue = Convert.ToDouble(split[114]);
                    }

                    if (split[115] != null)
                    {
                        passRange.Rear_da_45_PassRangeValue = Convert.ToDouble(split[115]);
                    }

                    if (split[116] != null)
                    {
                        passRange.Rear_da_45_PassRangeMinusValue = Convert.ToDouble(split[116]);
                    }

                    if (split[117] != null)
                    {
                        passRange.Rear_da_45_PassRangePlusValue = Convert.ToDouble(split[117]);
                    }

                    if (split[118] != null)
                    {
                        passRange.Rear_db_45_PassRangeValue = Convert.ToDouble(split[118]);
                    }

                    if (split[119] != null)
                    {
                        passRange.Rear_db_45_PassRangeMinusValue = Convert.ToDouble(split[119]);
                    }

                    if (split[120] != null)
                    {
                        passRange.Rear_db_45_PassRangePlusValue = Convert.ToDouble(split[120]);
                    }

                    if (split[121] != null)
                    {
                        passRange.Rear_dE_75_PassRangeValue = Convert.ToDouble(split[121]);
                    }

                    if (split[122] != null)
                    {
                        passRange.Rear_dE_75_PassRangeMinusValue = Convert.ToDouble(split[122]);
                    }

                    if (split[123] != null)
                    {
                        passRange.Rear_dE_75_PassRangePlusValue = Convert.ToDouble(split[123]);
                    }

                    if (split[124] != null)
                    {
                        passRange.Rear_dL_75_PassRangeValue = Convert.ToDouble(split[124]);
                    }

                    if (split[125] != null)
                    {
                        passRange.Rear_dL_75_PassRangeMinusValue = Convert.ToDouble(split[125]);
                    }

                    if (split[126] != null)
                    {
                        passRange.Rear_dL_75_PassRangePlusValue = Convert.ToDouble(split[126]);
                    }

                    if (split[127] != null)
                    {
                        passRange.Rear_da_75_PassRangeValue = Convert.ToDouble(split[127]);
                    }

                    if (split[128] != null)
                    {
                        passRange.Rear_da_75_PassRangeMinusValue = Convert.ToDouble(split[128]);
                    }

                    if (split[129] != null)
                    {
                        passRange.Rear_da_75_PassRangePlusValue = Convert.ToDouble(split[129]);
                    }

                    if (split[130] != null)
                    {
                        passRange.Rear_db_75_PassRangeValue = Convert.ToDouble(split[130]);
                    }

                    if (split[131] != null)
                    {
                        passRange.Rear_db_75_PassRangeMinusValue = Convert.ToDouble(split[131]);
                    }

                    if (split[132] != null)
                    {
                        passRange.Rear_db_75_PassRangePlusValue = Convert.ToDouble(split[132]);
                    }

                    if (split[133] != null)
                    {
                        passRange.Rear_dE_110_PassRangeValue = Convert.ToDouble(split[133]);
                    }

                    if (split[134] != null)
                    {
                        passRange.Rear_dE_110_PassRangeMinusValue = Convert.ToDouble(split[134]);
                    }

                    if (split[135] != null)
                    {
                        passRange.Rear_dE_110_PassRangePlusValue = Convert.ToDouble(split[135]);
                    }

                    if (split[136] != null)
                    {
                        passRange.Rear_dL_110_PassRangeValue = Convert.ToDouble(split[136]);
                    }

                    if (split[137] != null)
                    {
                        passRange.Rear_dL_110_PassRangeMinusValue = Convert.ToDouble(split[137]);
                    }

                    if (split[138] != null)
                    {
                        passRange.Rear_dL_110_PassRangePlusValue = Convert.ToDouble(split[138]);
                    }

                    if (split[139] != null)
                    {
                        passRange.Rear_da_110_PassRangeValue = Convert.ToDouble(split[139]);
                    }

                    if (split[140] != null)
                    {
                        passRange.Rear_da_110_PassRangeMinusValue = Convert.ToDouble(split[140]);
                    }

                    if (split[141] != null)
                    {
                        passRange.Rear_da_110_PassRangePlusValue = Convert.ToDouble(split[141]);
                    }

                    if (split[142] != null)
                    {
                        passRange.Rear_db_110_PassRangeValue = Convert.ToDouble(split[142]);
                    }

                    if (split[143] != null)
                    {
                        passRange.Rear_db_110_PassRangeMinusValue = Convert.ToDouble(split[143]);
                    }

                    if (split[144] != null)
                    {
                        passRange.Rear_db_110_PassRangePlusValue = Convert.ToDouble(split[144]);
                    }

                    list.Add(passRange);
                }
            }

            return list;
        }

        private ObservableCollection<StructPassRange> passRangeList = new ObservableCollection<StructPassRange>();
        public ObservableCollection<StructPassRange> PassRangeList
        {
            get
            {
                return passRangeList;
            }
            set
            {
                passRangeList = value;
                NotifyPropertyChanged("PassRangeList");
            }
        }

        public void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(passRangeFilePath))
            {
                FileStream fs = File.Create(passRangeFilePath);
                fs.Close();
                fs.Dispose();
                fs = null;
            }

            if (!File.Exists(carKindFilePath))
            {
                FileStream fs = File.Create(carKindFilePath);
                fs.Close();
                fs.Dispose();
                fs = null;
            }

            List<StructPassRange> list = LoadPassRangeData();
            PassRangeList = new ObservableCollection<StructPassRange>(list);

            List<StructCarKind> _list = LoadCarKindListData();
            SettingCarKindList = new ObservableCollection<StructCarKind>(_list);

            SettingWindow settingWindow = new SettingWindow();
            settingWindow.DataContext = this;
            settingWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingWindow.ShowDialog();

            InitCarKindList();
            InitColorList();
        }

        public void PassRangeList_AddingNewItem(object sender, AddingNewItemEventArgs e)
        {

        }

        public void PassRangeList_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {

        }

        public void PassRangeList_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            /*
            if (passRangeList.Count > 0)
            {
                int count = passRangeList.Count(x => x.Color == ((StructPassRange)e.Row.Item).Color);

                if (count > 1)
                {
                    e.Cancel = true;
                    return;
                }
            }
            */
        }

        public void PassRangeListSaveButton_Click(object sender, RoutedEventArgs e)
        {
            string content = "";

            passRangeList.ToList().ForEach(x =>
            {
                content += x.Color + "," + x.Front_dE_Minus15_PassRangeValue + "," + x.Front_dE_Minus15_PassRangeMinusValue + "," + x.Front_dE_Minus15_PassRangePlusValue + "," + x.Front_dL_Minus15_PassRangeValue + "," + x.Front_dL_Minus15_PassRangeMinusValue + "," + x.Front_dL_Minus15_PassRangePlusValue + "," + x.Front_da_Minus15_PassRangeValue + "," + x.Front_da_Minus15_PassRangeMinusValue + "," + x.Front_da_Minus15_PassRangePlusValue + "," + x.Front_db_Minus15_PassRangeValue + "," + x.Front_db_Minus15_PassRangeMinusValue + "," + x.Front_db_Minus15_PassRangePlusValue + "," + x.Front_dE_15_PassRangeValue + "," + x.Front_dE_15_PassRangeMinusValue + "," + x.Front_dE_15_PassRangePlusValue + "," + x.Front_dL_15_PassRangeValue + "," + x.Front_dL_15_PassRangeMinusValue + "," + x.Front_dL_15_PassRangePlusValue + "," + x.Front_da_15_PassRangeValue + "," + x.Front_da_15_PassRangeMinusValue + "," + x.Front_da_15_PassRangePlusValue + "," + x.Front_db_15_PassRangeValue + "," + x.Front_db_15_PassRangeMinusValue + "," + x.Front_db_15_PassRangePlusValue + "," + x.Front_dE_25_PassRangeValue + "," + x.Front_dE_25_PassRangeMinusValue + "," + x.Front_dE_25_PassRangePlusValue + "," + x.Front_dL_25_PassRangeValue + "," + x.Front_dL_25_PassRangeMinusValue + "," + x.Front_dL_25_PassRangePlusValue + "," + x.Front_da_25_PassRangeValue + "," + x.Front_da_25_PassRangeMinusValue + "," + x.Front_da_25_PassRangePlusValue + "," + x.Front_db_25_PassRangeValue + "," + x.Front_db_25_PassRangeMinusValue + "," + x.Front_db_25_PassRangePlusValue + "," + x.Front_dE_45_PassRangeValue + "," + x.Front_dE_45_PassRangeMinusValue + "," + x.Front_dE_45_PassRangePlusValue + "," + x.Front_dL_45_PassRangeValue + "," + x.Front_dL_45_PassRangeMinusValue + "," + x.Front_dL_45_PassRangePlusValue + "," + x.Front_da_45_PassRangeValue + "," + x.Front_da_45_PassRangeMinusValue + "," + x.Front_da_45_PassRangePlusValue + "," + x.Front_db_45_PassRangeValue + "," + x.Front_db_45_PassRangeMinusValue + "," + x.Front_db_45_PassRangePlusValue + "," + x.Front_dE_75_PassRangeValue + "," + x.Front_dE_75_PassRangeMinusValue + "," + x.Front_dE_75_PassRangePlusValue + "," + x.Front_dL_75_PassRangeValue + "," + x.Front_dL_75_PassRangeMinusValue + "," + x.Front_dL_75_PassRangePlusValue + "," + x.Front_da_75_PassRangeValue + "," + x.Front_da_75_PassRangeMinusValue + "," + x.Front_da_75_PassRangePlusValue + "," + x.Front_db_75_PassRangeValue + "," + x.Front_db_75_PassRangeMinusValue + "," + x.Front_db_75_PassRangePlusValue + "," + x.Front_dE_110_PassRangeValue + "," + x.Front_dE_110_PassRangeMinusValue + "," + x.Front_dE_110_PassRangePlusValue + "," + x.Front_dL_110_PassRangeValue + "," + x.Front_dL_110_PassRangeMinusValue + "," + x.Front_dL_110_PassRangePlusValue + "," + x.Front_da_110_PassRangeValue + "," + x.Front_da_110_PassRangeMinusValue + "," + x.Front_da_110_PassRangePlusValue + "," + x.Front_db_110_PassRangeValue + "," + x.Front_db_110_PassRangeMinusValue + "," + x.Front_db_110_PassRangePlusValue + "," + x.Rear_dE_Minus15_PassRangeValue + "," + x.Rear_dE_Minus15_PassRangeMinusValue + "," + x.Rear_dE_Minus15_PassRangePlusValue + "," + x.Rear_dL_Minus15_PassRangeValue + "," + x.Rear_dL_Minus15_PassRangeMinusValue + "," + x.Rear_dL_Minus15_PassRangePlusValue + "," + x.Rear_da_Minus15_PassRangeValue + "," + x.Rear_da_Minus15_PassRangeMinusValue + "," + x.Rear_da_Minus15_PassRangePlusValue + "," + x.Rear_db_Minus15_PassRangeValue + "," + x.Rear_db_Minus15_PassRangeMinusValue + "," + x.Rear_db_Minus15_PassRangePlusValue + "," + x.Rear_dE_15_PassRangeValue + "," + x.Rear_dE_15_PassRangeMinusValue + "," + x.Rear_dE_15_PassRangePlusValue + "," + x.Rear_dL_15_PassRangeValue + "," + x.Rear_dL_15_PassRangeMinusValue + "," + x.Rear_dL_15_PassRangePlusValue + "," + x.Rear_da_15_PassRangeValue + "," + x.Rear_da_15_PassRangeMinusValue + "," + x.Rear_da_15_PassRangePlusValue + "," + x.Rear_db_15_PassRangeValue + "," + x.Rear_db_15_PassRangeMinusValue + "," + x.Rear_db_15_PassRangePlusValue + "," + x.Rear_dE_25_PassRangeValue + "," + x.Rear_dE_25_PassRangeMinusValue + "," + x.Rear_dE_25_PassRangePlusValue + "," + x.Rear_dL_25_PassRangeValue + "," + x.Rear_dL_25_PassRangeMinusValue + "," + x.Rear_dL_25_PassRangePlusValue + "," + x.Rear_da_25_PassRangeValue + "," + x.Rear_da_25_PassRangeMinusValue + "," + x.Rear_da_25_PassRangePlusValue + "," + x.Rear_db_25_PassRangeValue + "," + x.Rear_db_25_PassRangeMinusValue + "," + x.Rear_db_25_PassRangePlusValue + "," + x.Rear_dE_45_PassRangeValue + "," + x.Rear_dE_45_PassRangeMinusValue + "," + x.Rear_dE_45_PassRangePlusValue + "," + x.Rear_dL_45_PassRangeValue + "," + x.Rear_dL_45_PassRangeMinusValue + "," + x.Rear_dL_45_PassRangePlusValue + "," + x.Rear_da_45_PassRangeValue + "," + x.Rear_da_45_PassRangeMinusValue + "," + x.Rear_da_45_PassRangePlusValue + "," + x.Rear_db_45_PassRangeValue + "," + x.Rear_db_45_PassRangeMinusValue + "," + x.Rear_db_45_PassRangePlusValue + "," + x.Rear_dE_75_PassRangeValue + "," + x.Rear_dE_75_PassRangeMinusValue + "," + x.Rear_dE_75_PassRangePlusValue + "," + x.Rear_dL_75_PassRangeValue + "," + x.Rear_dL_75_PassRangeMinusValue + "," + x.Rear_dL_75_PassRangePlusValue + "," + x.Rear_da_75_PassRangeValue + "," + x.Rear_da_75_PassRangeMinusValue + "," + x.Rear_da_75_PassRangePlusValue + "," + x.Rear_db_75_PassRangeValue + "," + x.Rear_db_75_PassRangeMinusValue + "," + x.Rear_db_75_PassRangePlusValue + "," + x.Rear_dE_110_PassRangeValue + "," + x.Rear_dE_110_PassRangeMinusValue + "," + x.Rear_dE_110_PassRangePlusValue + "," + x.Rear_dL_110_PassRangeValue + "," + x.Rear_dL_110_PassRangeMinusValue + "," + x.Rear_dL_110_PassRangePlusValue + "," + x.Rear_da_110_PassRangeValue + "," + x.Rear_da_110_PassRangeMinusValue + "," + x.Rear_da_110_PassRangePlusValue + "," + x.Rear_db_110_PassRangeValue + "," + x.Rear_db_110_PassRangeMinusValue + "," + x.Rear_db_110_PassRangePlusValue + Environment.NewLine;
            });

            File.WriteAllText(passRangeFilePath, content);
        }

        public void PassRangeListAddButton_Click(object sender, RoutedEventArgs e)
        {
            PassRangeList.Add(new StructPassRange());
        }

        public void PassRangeListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (passRangeDataGrid != null)
            {
                int index = passRangeDataGrid.SelectedIndex;

                PassRangeList.RemoveAt(index);
            }
        }

        private DataGrid passRangeDataGrid = null;

        public void PassRangeList_Loaded(object sender, RoutedEventArgs e)
        {
            passRangeDataGrid = (DataGrid)sender;
        }

        private ObservableCollection<StructCarKind> settingCarKindList = new ObservableCollection<StructCarKind>();
        public ObservableCollection<StructCarKind> SettingCarKindList
        {
            get
            {
                return settingCarKindList;
            }
            set
            {
                settingCarKindList = value;
                NotifyPropertyChanged("SettingCarKindList");
            }
        }

        private List<StructCarKind> LoadCarKindListData()
        {
            List<StructCarKind> list = new List<StructCarKind>();

            string[] lines = File.ReadAllLines(carKindFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line != null)
                {
                    StructCarKind structCarKind = new StructCarKind();
                    structCarKind.Name = line;

                    list.Add(structCarKind);
                }
            }

            return list;
        }

        public void CarKindListAddButton_Click(object sender, RoutedEventArgs e)
        {
            SettingCarKindList.Add(new StructCarKind());
        }

        public void CarKindListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (carKindListDataGrid != null)
            {
                int index = carKindListDataGrid.SelectedIndex;

                SettingCarKindList.RemoveAt(index);
            }
        }

        public void CarKindListSaveButton_Click(object sender, RoutedEventArgs e)
        {
            string content = "";

            settingCarKindList.ToList().ForEach(x =>
            {
                content += x.Name + Environment.NewLine;
            });

            File.WriteAllText(carKindFilePath, content);
        }

        private DataGrid carKindListDataGrid = null;

        public void CarKindListAddButton_Loaded(object sender, RoutedEventArgs e)
        {
            carKindListDataGrid = (DataGrid)sender;
        }
    }
}
