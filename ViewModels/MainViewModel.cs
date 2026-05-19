using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Helpers;
using TreatmentPlanReport.Views;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class MainViewModel
    {
        public PatientViewModel LocalPatientViewModel { get; set; }
        public PlanViewModel LocalPlanViewModel { get; set; }
        public RxViewModel LocalRxViewModel { get; set; }
        public PatientShiftViewModel LocalPatientShiftViewModel { get; set; }
        public FieldViewModel LocalFieldViewModel { get; set; }
        public DVHViewModel LocalDVHViewModel { get; set; }
        public IsoOrthogonalViewModel LocalOrthogonalImageViewModel { get; set; }
        public MainViewModel(Patient patient, PlanSetup plan)
        {
            LocalPatientViewModel = new PatientViewModel(patient);
            LocalPlanViewModel = new PlanViewModel(plan);
            LocalRxViewModel = new RxViewModel(plan);
            LocalPatientShiftViewModel = new PatientShiftViewModel(plan);
            LocalFieldViewModel = new FieldViewModel(plan);
            //generate event helper.
            EventHelper eventHelper = new EventHelper();
            LocalDVHViewModel = new DVHViewModel(plan, eventHelper);
            LocalOrthogonalImageViewModel = new IsoOrthogonalViewModel(patient, plan.Course, plan);
        }
    }
}
