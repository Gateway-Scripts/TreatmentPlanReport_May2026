using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Models;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class FieldViewModel
    {
        public List<FieldSummaryModel> FieldProperties { get; set; }
        public FieldViewModel(PlanSetup plan)
        {
            FieldProperties = new List<FieldSummaryModel>();
            SetFieldProperties(plan);
        }

        private void SetFieldProperties(PlanSetup plan)
        {
            var beams = plan.Beams;

            foreach (var beam in beams.OrderBy(b => b.IsSetupField).ThenBy(b => b.BeamNumber))//setup fields go to the bottom.
            {
                FieldSummaryModel fieldDisplay = new FieldSummaryModel(beam);
                FieldProperties.Add(fieldDisplay);
            }
        }
    }
}
