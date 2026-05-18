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
        public PatientViewModel LocalPatientViewModel { get; set; }
        public MainViewModel(Patient patient, PlanSetup plan)
        {
            PlanId = plan.Id;
            LocalPatientViewModel = new PatientViewModel(patient);
        }
    }
}
