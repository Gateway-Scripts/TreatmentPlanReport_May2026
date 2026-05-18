using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreatmentPlanReport.Models
{
    public class PatientShiftModel
    {
        public double Magnitude { get; set; }
        public ShiftDirectionEnum ShiftDirection { get; set; }
        public string ShiftText { get; set; }
    }
}
