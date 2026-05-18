using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreatmentPlanReport.Models
{
    public class RxModel
    {
        public string PropertyName { get; set; }
        public string AriaValue { get; set; }
        public bool bHideEmpty { get; set; }
        public int AriaColumnSpan { get; set; }
        public string EclipseValue { get; set; }
        public bool bVisibility { get; set; }
    }
}
