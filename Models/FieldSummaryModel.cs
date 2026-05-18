using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TreatmentPlanReport.Models
{
    public class FieldSummaryModel
    {
        public string FieldId { get; set; }
        public string Energy { get; set; }
        public string Technique { get; set; }
        public string Gantry { get; set; }
        public string Collimator { get; set; }
        public string Couch { get; set; }
        public string FieldX { get; set; }
        public string FieldY { get; set; }
        public string MU { get; set; }
        public string FieldName { get; set; }
        public string MLCType { get; set; }
        public string X1 { get; set; }
        public string X2 { get; set; }
        public string Y1 { get; set; }
        public string Y2 { get; set; }
        public string SSD { get; set; }
        public string MachineId { get; set; }
        public string Wedge { get; set; }
        public string Bolus { get; set; }
        public string DoseRate { get; set; }
        public FieldSummaryModel(Beam beam)
        {
            FieldId = SetFieldId(beam);
            Energy = SetEnergy(beam);
            Technique = GetTechnique(beam);
            Gantry = GetGantry(beam);
            Collimator = GetCollimator(beam);
            Couch = GetCouch(beam);
            VRect<double> jaws = GetJaws(beam);
            FieldX = ((jaws.X2 - jaws.X1) / 10.0).ToString("F1");
            FieldY = ((jaws.Y2 - jaws.Y1) / 10.0).ToString("F1");
            MU = SetMU(beam);
            FieldName = beam.Name;
            MLCType = SetMLC(beam);
            X1 = (jaws.X1 / 10.0).ToString("F1");
            X2 = (jaws.X2 / 10.0).ToString("F1");
            Y1 = (jaws.Y1 / 10.0).ToString("F1");
            Y2 = (jaws.Y2 / 10.0).ToString("F1");
            SSD = GetSSD(beam);
            MachineId = SetMachineId(beam);
            Wedge = SetWedge(beam);
            Bolus = SetBolus(beam);
            DoseRate = SetDR(beam);
        }


        private string GetSSD(Beam _beam)
        {
            if (Double.IsNaN(_beam.SSD)) { return "-"; }
            return (_beam.SSD / 10.0).ToString("F1");
        }

        private string GetCouch(Beam _beam)
        {
            if (!_beam.ControlPoints.Any()) { return "NA"; }
            return _beam.PatientSupportAngleToUser(_beam.ControlPoints.First().PatientSupportAngle).ToString();
        }

        private string GetCollimator(Beam _beam)
        {
            if (!_beam.ControlPoints.Any()) { return "NA"; }
            return _beam.CollimatorAngleToUser(_beam.ControlPoints.First().CollimatorAngle).ToString();
        }

        private VRect<double> GetJaws(Beam _beam)
        {
            // VRect<double> jawPositions = new VMS.TPS.Common.Model.Types.VRect<double>();
            if (_beam.ControlPoints.Any())
            {
                double X1 = _beam.ControlPoints.Min(cp => cp.JawPositions.X1);
                double X2 = _beam.ControlPoints.Max(cp => cp.JawPositions.X2);
                double Y1 = _beam.ControlPoints.Min(cp => cp.JawPositions.Y1);
                double Y2 = _beam.ControlPoints.Max(cp => cp.JawPositions.Y2);
                return new VRect<double>(X1, Y1, X2, Y2);
            }
            return new VRect<double>(0, 0, 0, 0);
        }

        private string GetGantry(Beam _beam)
        {
            if (_beam.Technique.Id.ToUpper().Contains("ARC"))
            {
                double gantryStart = _beam.GantryAngleToUser(_beam.ControlPoints.First().GantryAngle);
                double gantryEnd = _beam.GantryAngleToUser(_beam.ControlPoints.Last().GantryAngle);
                string gantryDirection = _beam.GantryDirection == GantryDirection.Clockwise ? "CW" : "CCW";
                return $"{gantryStart} {gantryDirection} {gantryEnd}";
            }
            return _beam.GantryAngleToUser(_beam.ControlPoints.First().GantryAngle).ToString();
        }

        private string GetTechnique(Beam _beam)
        {
            if (String.IsNullOrEmpty(_beam.Technique?.Id))
            {
                return "NA";
            }
            return _beam.Technique.Id;
        }

        private string SetMU(Beam _beam)
        {
            if (Double.IsNaN(_beam.Meterset.Value)) { return "-"; }
            if (_beam.Meterset.Value.ToString().Contains('.'))
            {
                if (_beam.Meterset.Value.ToString().Split('.').Last().Length > 2)
                {
                    return _beam.Meterset.Value.ToString("F1");
                }
            }
            return _beam.Meterset.Value.ToString();
        }

        private string SetBolus(Beam _beam)
        {
            if (_beam.Boluses.Any())
            {
                return String.Join(", ", _beam.Boluses.Select(bl => bl.Id));
            }
            return "-";
        }

        private string SetWedge(Beam _beam)
        {
            if (_beam.Wedges.Any())
            {
                return String.Join(", ", _beam.Wedges.Select(w => w.Id));
            }
            return "-";
        }

        private string SetScale(Beam _beam)
        {
            if (_beam.TreatmentUnit != null)
            {
                return _beam.TreatmentUnit.MachineScaleDisplayName;
            }
            return "NA";
        }

        private string SetMLC(Beam _beam)
        {
            if (_beam.IsSetupField) { return "-"; }
            if (_beam.MLC == null) { return "-"; }
            return _beam.MLCPlanType.ToString();
        }

        private string SetFieldWeight(Beam _beam)
        {
            return _beam.WeightFactor.ToString("F1");
        }

        private string SetDR(Beam _beam)
        {
            return _beam.DoseRate.ToString();
        }

        private string SetEnergy(Beam _beam)
        {
            return _beam.EnergyModeDisplayName ?? "-";
        }

        private string SetFieldType(Beam _beam)
        {
            if (_beam.IsSetupField) { return "Setup"; }
            if (_beam.EnergyModeDisplayName.Contains("E")) { return "Electron"; }
            return "Photon";
        }

        private string SetMachineId(Beam _beam)
        {
            return _beam.TreatmentUnit?.Id ?? "NA";
        }


        private string SetFieldId(Beam _beam)
        {
            return _beam.Id;
        }
    }
}
