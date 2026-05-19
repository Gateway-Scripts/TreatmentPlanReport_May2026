using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Models;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class RxViewModel
    {
        public List<RxModel> ReportItems { get; private set; }
        public RxViewModel(PlanSetup plan)
        {
            List<Tuple<string, bool>> propertySettings = new List<Tuple<string, bool>>
    {
        new Tuple<string, bool>("Rx Id",false),
        new Tuple<string, bool>("Target Id",false),
        new Tuple<string, bool>("Dose",false),
        new Tuple<string, bool>("Status",false),
        new Tuple<string, bool>("Approver", false),
        new Tuple<string, bool>("Energy", false),
        new Tuple<string, bool>("Gating",true),
        new Tuple<string, bool>("Bolus",true),
        new Tuple < string, bool >("Notes", false),
        new Tuple<string, bool>("Primary/Boost",false)
    };
            ReportItems = new List<RxModel>();
            var rtPrescription = plan.RTPrescription;
            bool bAriaValue = rtPrescription != null;
            foreach (var setting in propertySettings)
            {
                var rxItem = new RxModel
                {
                    PropertyName = setting.Item1,
                    AriaValue = bAriaValue ? GetAriaValueFromProperty(rtPrescription, setting.Item1) : String.Empty,
                    EclipseValue = GetEcilpseValueFromProperty(plan, setting.Item1),
                    bHideEmpty = setting.Item2,

                };
                rxItem.bVisibility = !(rxItem.bHideEmpty && String.IsNullOrEmpty(rxItem.AriaValue));
                ReportItems.Add(rxItem);
            }
        }
        private string GetEcilpseValueFromProperty(PlanSetup plan, string rxPropertyName)
        {
            switch (rxPropertyName)
            {
                case "Rx Id":
                    return plan.Id;
                //case "Prescribe To":
                //    return ariaPrescription.i
                case "Target Id":
                    return plan.TargetVolumeID;
                case "Dose":
                    return $"{plan.DosePerFraction} x {plan.NumberOfFractions}";
                case "Status":
                    return plan.ApprovalStatus.ToString();
                case "Approver":
                    return plan.ApprovalHistory.OrderByDescending(ah => ah.ApprovalDateTime).First().UserDisplayName;
                case "Energy":
                    return String.Join(", ", plan.Beams.Where(b => !b.IsSetupField).Select(b => b.EnergyModeDisplayName).Distinct());
                case "Gating":
                    return plan.UseGating ? "Checked" : "Not Checked";
                //here using bolus attached to field. Can someone use bolus if only in structureset?
                case "Bolus":
                    return plan.Beams.Any(b => b.Boluses.Any()) ? "Bolus Used" : "No Bolus";
                //ARIA only
                case "Notes":
                    return String.Empty;
                //ARIA only.
                case "Prinary/Boost":
                    return String.Empty;
                default:
                    return "Could not identify property name";
            }
        }

        private string GetAriaValueFromProperty(RTPrescription ariaPrescription, string rxPropertyName)
        {
            switch (rxPropertyName)
            {
                case "Rx Id":
                    return ariaPrescription.Id;
                //case "Prescribe To":
                //    return ariaPrescription.i
                case "Target Id":
                    return ariaPrescription.Targets.Any() ? ariaPrescription.Targets.First().Id : String.Empty;
                case "Dose":
                    return ariaPrescription.Targets.Any() ? $"{ariaPrescription.Targets.First().DosePerFraction} x {ariaPrescription.NumberOfFractions}" : String.Empty;
                case "Status":
                    return ariaPrescription.Status;
                case "Approver":
                    return ariaPrescription.HistoryUserDisplayName;
                case "Energy":
                    return String.Join(", ", ariaPrescription.EnergyModes);
                case "Gating":
                    return ariaPrescription.Gating;
                case "Bolus":
                    return String.IsNullOrEmpty(ariaPrescription.BolusFrequency) && String.IsNullOrEmpty(ariaPrescription.BolusThickness) ? String.Empty : $"{ariaPrescription.BolusThickness} {ariaPrescription.BolusFrequency}";
                case "Notes":
                    return ariaPrescription.Notes;
                case "Prinary/Boost":
                    return ariaPrescription.PhaseType;
                default:
                    return "Could not identify property name";
            }
        }
    }

}
