using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class PlanViewModel
    {
        //Properties
        public string PlanId { get; set; }
        public string PlanName { get; set; }
        public string CourseId { get; set; }
        public string ApprovalStatus { get; set; }
        public string PlanType { get; set; }
        public string PlanIntent { get; set; }

        //Constructor to set plan information.
        public PlanViewModel(PlanSetup plan)
        {
            PlanId = plan.Id;
            PlanName = plan.Name;
            CourseId = plan.Course.Id;
            ApprovalStatus = plan.ApprovalStatus.ToString();
            PlanType = plan.PlanType.ToString();
            PlanIntent = plan.PlanIntent.ToString();
        }
    }
}
