using HUtill;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Hansero;
using System.Diagnostics;

namespace DBManager
{
    public class DBManager
    {
        private IniFile m_Config = null;
        private MySqlConnection conn = null;
        private LogManager logManager;
        private bool isOpen = false;

        public DBManager()
        {
            logManager = new LogManager(true, true);
            m_Config = new IniFile(Environment.CurrentDirectory + "\\Config.ini");
        }

        public void Open()
        {
            try
            {
                string connStr = "Server=" + m_Config.GetString("DB", "Server", "localhost") + "; Database=" + m_Config.GetString("DB", "Database", "vision") + "; Uid=" + m_Config.GetString("DB", "Uid", "root") + "; Pwd=" + m_Config.GetString("DB", "Pwd", "") + ";";
                conn = new MySqlConnection(connStr);
                conn.Open();
                isOpen = true;
            }
            catch (Exception e)
            {
                logManager.Error("데이터베이스 오픈 실패 : " + e.Message);
            }
        }

        public void Close()
        {
            try
            {
                if (isOpen)
                {
                    conn.Close();
                    isOpen = false;
                }
                else
                {
                    logManager.Error("데이터베이스가 오픈되어있지 않습니다.");
                }
            }
            catch (Exception e)
            {
                logManager.Error("데이터베이스 닫기 실패 : " + e.Message);
            }
        }

        public int ExecuteNonQuery(string qry)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Open();

            Stopwatch _sw = new Stopwatch();
            _sw.Start();
            MySqlCommand cmd = new MySqlCommand(qry, conn);
            int result = cmd.ExecuteNonQuery();
            _sw.Stop();
            logManager.Trace("실제 ExecuteNonQuery 소요 시간 : " + _sw.ElapsedMilliseconds + "ms");

            Close();
            sw.Stop();
            logManager.Trace("ExecuteNonQuery 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

            return result;
        }

        public List<Dictionary<string, string>> ExecuteReader(string qry)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Open();

            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();

            MySqlCommand cmd = new MySqlCommand(qry, conn);
            MySqlDataReader msdr = cmd.ExecuteReader();
            
            while (msdr.Read())
            {
                Dictionary<string, string> item = new Dictionary<string, string>();

                for (int i = 0; i < msdr.FieldCount; i++)
                {
                    // item.Add()
                    item.Add(msdr.GetName(i), msdr[i].ToString());
                }

                list.Add(item);
            }

            msdr.Close();
            msdr.Dispose();
            msdr = null;

            Close();

            sw.Stop();
            Console.WriteLine("ExecuteReader 소요 시간 : " + sw.ElapsedMilliseconds + "ms");

            return list;
        }
    }
}
