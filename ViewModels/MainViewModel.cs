using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class MainViewModel
    {
        public string PlanId { get; set; }
        public MainViewModel(PlanSetup plan)
        {
            PlanId = plan.Id;
        }
    }
}
