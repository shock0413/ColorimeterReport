using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Report
{
    public class StructReportData
    {
        public DateTime StartDateTime { get; set; }
        public string DateTime { get; set; }
        public string Model { get; set; }
        public string Color { get; set; }
        public string Comment { get; set; }
        public string BodyNumber { get; set; }
        public string Result { get; set; }

        // 프론트 휀다
        public string FR_FENDA_dE_Minus15 { get; set; }
        public string FR_FENDA_dE_15 { get; set; }
        public string FR_FENDA_dE_25 { get; set; }
        public string FR_FENDA_dE_45 { get; set; }
        public string FR_FENDA_dE_75 { get; set; }
        public string FR_FENDA_dE_110 { get; set; }
        public string FR_FENDA_L_Minus15 { get; set; }
        public string FR_FENDA_a_Minus15 { get; set; }
        public string FR_FENDA_b_Minus15 { get; set; }
        public string FR_FENDA_L_15 { get; set; }
        public string FR_FENDA_a_15 { get; set; }
        public string FR_FENDA_b_15 { get; set; }
        public string FR_FENDA_L_25 { get; set; }
        public string FR_FENDA_a_25 { get; set; }
        public string FR_FENDA_b_25 { get; set; }
        public string FR_FENDA_L_45 { get; set; }
        public string FR_FENDA_a_45 { get; set; }
        public string FR_FENDA_b_45 { get; set; }
        public string FR_FENDA_L_75 { get; set; }
        public string FR_FENDA_a_75 { get; set; }
        public string FR_FENDA_b_75 { get; set; }
        public string FR_FENDA_L_110 { get; set; }
        public string FR_FENDA_a_110 { get; set; }
        public string FR_FENDA_b_110 { get; set; }
        public string FR_FENDA_dL_Minus15 { get; set; }
        public string FR_FENDA_da_Minus15 { get; set; }
        public string FR_FENDA_db_Minus15 { get; set; }
        public string FR_FENDA_dL_15 { get; set; }
        public string FR_FENDA_da_15 { get; set; }
        public string FR_FENDA_db_15 { get; set; }
        public string FR_FENDA_dL_25 { get; set; }
        public string FR_FENDA_da_25 { get; set; }
        public string FR_FENDA_db_25 { get; set; }
        public string FR_FENDA_dL_45 { get; set; }
        public string FR_FENDA_da_45 { get; set; }
        public string FR_FENDA_db_45 { get; set; }
        public string FR_FENDA_dL_75 { get; set; }
        public string FR_FENDA_da_75 { get; set; }
        public string FR_FENDA_db_75 { get; set; }
        public string FR_FENDA_dL_110 { get; set; }
        public string FR_FENDA_da_110 { get; set; }
        public string FR_FENDA_db_110 { get; set; }

        // 프론트 범퍼
        public string FR_BUMPER_dE_Minus15 { get; set; }
        public string FR_BUMPER_dE_15 { get; set; }
        public string FR_BUMPER_dE_25 { get; set; }
        public string FR_BUMPER_dE_45 { get; set; }
        public string FR_BUMPER_dE_75 { get; set; }
        public string FR_BUMPER_dE_110 { get; set; }
        public string FR_BUMPER_L_Minus15 { get; set; }
        public string FR_BUMPER_a_Minus15 { get; set; }
        public string FR_BUMPER_b_Minus15 { get; set; }
        public string FR_BUMPER_L_15 { get; set; }
        public string FR_BUMPER_a_15 { get; set; }
        public string FR_BUMPER_b_15 { get; set; }
        public string FR_BUMPER_L_25 { get; set; }
        public string FR_BUMPER_a_25 { get; set; }
        public string FR_BUMPER_b_25 { get; set; }
        public string FR_BUMPER_L_45 { get; set; }
        public string FR_BUMPER_a_45 { get; set; }
        public string FR_BUMPER_b_45 { get; set; }
        public string FR_BUMPER_L_75 { get; set; }
        public string FR_BUMPER_a_75 { get; set; }
        public string FR_BUMPER_b_75 { get; set; }
        public string FR_BUMPER_L_110 { get; set; }
        public string FR_BUMPER_a_110 { get; set; }
        public string FR_BUMPER_b_110 { get; set; }
        public string FR_BUMPER_dL_Minus15 { get; set; }
        public string FR_BUMPER_da_Minus15 { get; set; }
        public string FR_BUMPER_db_Minus15 { get; set; }
        public string FR_BUMPER_dL_15 { get; set; }
        public string FR_BUMPER_da_15 { get; set; }
        public string FR_BUMPER_db_15 { get; set; }
        public string FR_BUMPER_dL_25 { get; set; }
        public string FR_BUMPER_da_25 { get; set; }
        public string FR_BUMPER_db_25 { get; set; }
        public string FR_BUMPER_dL_45 { get; set; }
        public string FR_BUMPER_da_45 { get; set; }
        public string FR_BUMPER_db_45 { get; set; }
        public string FR_BUMPER_dL_75 { get; set; }
        public string FR_BUMPER_da_75 { get; set; }
        public string FR_BUMPER_db_75 { get; set; }
        public string FR_BUMPER_dL_110 { get; set; }
        public string FR_BUMPER_da_110 { get; set; }
        public string FR_BUMPER_db_110 { get; set; }

        // 리어 QTR
        public string RR_QTR_dE_Minus15 { get; set; }
        public string RR_QTR_dE_15 { get; set; }
        public string RR_QTR_dE_25 { get; set; }
        public string RR_QTR_dE_45 { get; set; }
        public string RR_QTR_dE_75 { get; set; }
        public string RR_QTR_dE_110 { get; set; }
        public string RR_QTR_L_Minus15 { get; set; }
        public string RR_QTR_a_Minus15 { get; set; }
        public string RR_QTR_b_Minus15 { get; set; }
        public string RR_QTR_L_15 { get; set; }
        public string RR_QTR_a_15 { get; set; }
        public string RR_QTR_b_15 { get; set; }
        public string RR_QTR_L_25 { get; set; }
        public string RR_QTR_a_25 { get; set; }
        public string RR_QTR_b_25 { get; set; }
        public string RR_QTR_L_45 { get; set; }
        public string RR_QTR_a_45 { get; set; }
        public string RR_QTR_b_45 { get; set; }
        public string RR_QTR_L_75 { get; set; }
        public string RR_QTR_a_75 { get; set; }
        public string RR_QTR_b_75 { get; set; }
        public string RR_QTR_L_110 { get; set; }
        public string RR_QTR_a_110 { get; set; }
        public string RR_QTR_b_110 { get; set; }
        public string RR_QTR_dL_Minus15 { get; set; }
        public string RR_QTR_da_Minus15 { get; set; }
        public string RR_QTR_db_Minus15 { get; set; }
        public string RR_QTR_dL_15 { get; set; }
        public string RR_QTR_da_15 { get; set; }
        public string RR_QTR_db_15 { get; set; }
        public string RR_QTR_dL_25 { get; set; }
        public string RR_QTR_da_25 { get; set; }
        public string RR_QTR_db_25 { get; set; }
        public string RR_QTR_dL_45 { get; set; }
        public string RR_QTR_da_45 { get; set; }
        public string RR_QTR_db_45 { get; set; }
        public string RR_QTR_dL_75 { get; set; }
        public string RR_QTR_da_75 { get; set; }
        public string RR_QTR_db_75 { get; set; }
        public string RR_QTR_dL_110 { get; set; }
        public string RR_QTR_da_110 { get; set; }
        public string RR_QTR_db_110 { get; set; }

        // 리어 범퍼
        public string RR_BUMPER_dE_Minus15 { get; set; }
        public string RR_BUMPER_dE_15 { get; set; }
        public string RR_BUMPER_dE_25 { get; set; }
        public string RR_BUMPER_dE_45 { get; set; }
        public string RR_BUMPER_dE_75 { get; set; }
        public string RR_BUMPER_dE_110 { get; set; }
        public string RR_BUMPER_L_Minus15 { get; set; }
        public string RR_BUMPER_a_Minus15 { get; set; }
        public string RR_BUMPER_b_Minus15 { get; set; }
        public string RR_BUMPER_L_15 { get; set; }
        public string RR_BUMPER_a_15 { get; set; }
        public string RR_BUMPER_b_15 { get; set; }
        public string RR_BUMPER_L_25 { get; set; }
        public string RR_BUMPER_a_25 { get; set; }
        public string RR_BUMPER_b_25 { get; set; }
        public string RR_BUMPER_L_45 { get; set; }
        public string RR_BUMPER_a_45 { get; set; }
        public string RR_BUMPER_b_45 { get; set; }
        public string RR_BUMPER_L_75 { get; set; }
        public string RR_BUMPER_a_75 { get; set; }
        public string RR_BUMPER_b_75 { get; set; }
        public string RR_BUMPER_L_110 { get; set; }
        public string RR_BUMPER_a_110 { get; set; }
        public string RR_BUMPER_b_110 { get; set; }
        public string RR_BUMPER_dL_Minus15 { get; set; }
        public string RR_BUMPER_da_Minus15 { get; set; }
        public string RR_BUMPER_db_Minus15 { get; set; }
        public string RR_BUMPER_dL_15 { get; set; }
        public string RR_BUMPER_da_15 { get; set; }
        public string RR_BUMPER_db_15 { get; set; }
        public string RR_BUMPER_dL_25 { get; set; }
        public string RR_BUMPER_da_25 { get; set; }
        public string RR_BUMPER_db_25 { get; set; }
        public string RR_BUMPER_dL_45 { get; set; }
        public string RR_BUMPER_da_45 { get; set; }
        public string RR_BUMPER_db_45 { get; set; }
        public string RR_BUMPER_dL_75 { get; set; }
        public string RR_BUMPER_da_75 { get; set; }
        public string RR_BUMPER_db_75 { get; set; }
        public string RR_BUMPER_dL_110 { get; set; }
        public string RR_BUMPER_da_110 { get; set; }
        public string RR_BUMPER_db_110 { get; set; }

        // Front Delta
        public string FR_DELTA { get; set; }

        // REAR Delta
        public string RR_DELTA { get; set; }

        public string FR_Result { get; set; }
        public string RR_Result { get; set; }
    }
}
