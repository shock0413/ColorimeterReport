using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Report
{
    public class StructMeasurement
    {
        public string CheckZone { get; set; }
        public string dE_Minus15 { get; set; }
        public string dE_15 { get; set; }
        public string dE_25 { get; set; }
        public string dE_45 { get; set; }
        public string dE_75 { get; set; }
        public string dE_110 { get; set; }
        public string L_Minus15 { get; set; }
        public string a_Minus15 { get; set; }
        public string b_Minus15 { get; set; }
        public string L_15 { get; set; }
        public string a_15 { get; set; }
        public string b_15 { get; set; }
        public string L_25 { get; set; }
        public string a_25 { get; set; }
        public string b_25 { get; set; }
        public string L_45 { get; set; }
        public string a_45 { get; set; }
        public string b_45 { get; set; }
        public string L_75 { get; set; }
        public string a_75 { get; set; }
        public string b_75 { get; set; }
        public string L_110 { get; set; }
        public string a_110 { get; set; }
        public string b_110 { get; set; }
        public string dL_Minus15 { get; set; }
        public string da_Minus15 { get; set; }
        public string db_Minus15 { get; set; }
        public string dL_15 { get; set; }
        public string da_15 { get; set; }
        public string db_15 { get; set; }
        public string dL_25 { get; set; }
        public string da_25 { get; set; }
        public string db_25 { get; set; }
        public string dL_45 { get; set; }
        public string da_45 { get; set; }
        public string db_45 { get; set; }
        public string dL_75 { get; set; }
        public string da_75 { get; set; }
        public string db_75 { get; set; }
        public string dL_110 { get; set; }
        public string da_110 { get; set; }
        public string db_110 { get; set; }

        public StructMeasurement()
        {

        }

        public StructMeasurement(string checkzone, string de_minus15, string de_15, string de_25, string de_45, string de_75, string de_110, string L_minus15, string a_minus15, string b_minus15, string L_15, string a_15, string b_15, string L_25, string a_25, string b_25, string L_45, string a_45, string b_45, string L_75, string a_75, string b_75, string L_110, string a_110, string b_110)
        {
            this.CheckZone = checkzone;
            this.dE_Minus15 = de_minus15;
            this.dE_15 = de_15;
            this.dE_25 = de_25;
            this.dE_45 = de_45;
            this.dE_75 = de_75;
            this.dE_110 = de_110;
            this.L_Minus15 = L_minus15;
            this.a_Minus15 = a_minus15;
            this.b_Minus15 = b_minus15;
            this.L_15 = L_15;
            this.a_15 = a_15;
            this.b_15 = b_15;
            this.L_25 = L_25;
            this.a_25 = a_25;
            this.b_25 = b_25;
            this.L_45 = L_45;
            this.a_45 = a_45;
            this.b_45 = b_45;
            this.L_75 = L_75;
            this.a_75 = a_75;
            this.b_75 = b_75;
            this.L_110 = L_110;
            this.a_110 = a_110;
            this.b_110 = b_110;
        }

        public StructMeasurement(string checkzone, string de_minus15, string de_15, string de_25, string de_45, string de_75, string de_110, string L_minus15, string a_minus15, string b_minus15, string L_15, string a_15, string b_15, string L_25, string a_25, string b_25, string L_45, string a_45, string b_45, string L_75, string a_75, string b_75, string L_110, string a_110, string b_110, string dL_minus15, string da_minus15, string db_minus15, string dL_15, string da_15, string db_15, string dL_25, string da_25, string db_25, string dL_45, string da_45, string db_45, string dL_75, string da_75, string db_75, string dL_110, string da_110, string db_110)
        {
            this.CheckZone = checkzone;
            this.dE_Minus15 = de_minus15;
            this.dE_15 = de_15;
            this.dE_25 = de_25;
            this.dE_45 = de_45;
            this.dE_75 = de_75;
            this.dE_110 = de_110;
            this.L_Minus15 = L_minus15;
            this.a_Minus15 = a_minus15;
            this.b_Minus15 = b_minus15;
            this.L_15 = L_15;
            this.a_15 = a_15;
            this.b_15 = b_15;
            this.L_25 = L_25;
            this.a_25 = a_25;
            this.b_25 = b_25;
            this.L_45 = L_45;
            this.a_45 = a_45;
            this.b_45 = b_45;
            this.L_75 = L_75;
            this.a_75 = a_75;
            this.b_75 = b_75;
            this.L_110 = L_110;
            this.a_110 = a_110;
            this.b_110 = b_110;
            this.dL_Minus15 = dL_minus15;
            this.da_Minus15 = da_minus15;
            this.db_Minus15 = db_minus15;
            this.dL_15 = dL_15;
            this.da_15 = da_15;
            this.db_15 = db_15;
            this.dL_25 = dL_25;
            this.da_25 = da_25;
            this.db_25 = db_25;
            this.dL_45 = dL_45;
            this.da_45 = da_45;
            this.db_45 = db_45;
            this.dL_75 = dL_75;
            this.da_75 = da_75;
            this.db_75 = db_75;
            this.dL_110 = dL_110;
            this.da_110 = da_110;
            this.db_110 = db_110;
        }
    }
}
