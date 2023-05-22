using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Report
{
    public class StructSensorData
    {
        public string DateTime { get; set; }
        public string Model { get; set; }
        public string Color { get; set; }
        public string BodyNumber { get; set; }

        public StructMeasurement Measurement { get; set; }

        public StructSensorData()
        {

        }

        public StructSensorData(string dateTime, string model, string color, string bodyNumber, StructMeasurement measurement)
        {
            this.DateTime = dateTime;
            this.Model = model;
            this.Color = color;
            this.BodyNumber = bodyNumber;
            this.Measurement = measurement;
        }
    }
}
