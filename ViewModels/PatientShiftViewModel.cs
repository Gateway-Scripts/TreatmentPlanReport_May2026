using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Models;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TreatmentPlanReport.ViewModels
{
    public class PatientShiftViewModel
    {
        public string DICOMOrigin { get; set; }
        public string UserOrigin { get; set; }
        public string Isocenter { get; set; }
        public string ShiftOrigin { get; set; }

        private VVector _shiftPosition;
        public PatientShiftModel XShift { get; set; }
        public PatientShiftModel YShift { get; set; }
        public PatientShiftModel ZShift { get; set; }
        private PlanSetup _plan;
        public PatientShiftViewModel(PlanSetup plan)
        {
            _plan = plan;
            ShiftOrigin = "User Origin";
            GetCouchShifts();
        }
        private void GetCouchShifts()
        {
            List<VVector> isocenters = _plan.Beams.Select(b => b.IsocenterPosition).Distinct().ToList();
            var image = _plan.StructureSet.Image;
            foreach (var isocenter in isocenters)
            {
                //var userIsocenter = image.DicomToUser(isocenter, _plan);
                Isocenter += $"({isocenter.x / 10.0:F2}, {isocenter.y / 10.0:F2}, {isocenter.z / 10.0:F2})\n";
            }
            var dicomOrigin = image.Origin;
            DICOMOrigin = $"({dicomOrigin.x / 10.0:F2}, {dicomOrigin.y / 10.0:F2}, {dicomOrigin.z / 10.0:F2})";
            Beam b1 = _plan.Beams.Where(x => !x.IsSetupField).OrderBy(x => x.BeamNumber).FirstOrDefault();
            var uo = _plan.StructureSet.Image.UserOrigin;
            //var userOrigin = image.DicomToUser(uo, _plan);
            UserOrigin = $"({uo.x / 10.0:F2}, {uo.y / 10.0:F2}, {uo.z / 10.0:F2})";
            VVector shiftStart = new VVector();
            if (b1 != null)
            {
                //more simple implementation using DICOM 2 User
                var shift = _plan.StructureSet.Image.DicomToUser(b1.IsocenterPosition, _plan);
                XShift = new PatientShiftModel
                {
                    Magnitude = shift.x,
                    ShiftDirection = ShiftDirectionEnum.XDir,
                    ShiftText = Math.Abs(shift.x) < 0.01 ? "No Shift" :
                    shift.x < 0 ? $"Left: {Math.Abs(shift.x / 10.0):F2}" : $"Right: {Math.Abs(shift.x / 10.0):F2}"
                };
                YShift = new PatientShiftModel
                {
                    Magnitude = shift.y,
                    ShiftDirection = ShiftDirectionEnum.YDir,
                    ShiftText = Math.Abs(shift.y) < 0.01 ? "No Shift" :
                    shift.y < 0 ? $"Down: {Math.Abs(shift.y / 10.0):F2}" : $"Up: {Math.Abs(shift.y / 10.0):F2}"
                };
                ZShift = new PatientShiftModel
                {
                    Magnitude = shift.z,
                    ShiftDirection = ShiftDirectionEnum.ZDir,
                    ShiftText = Math.Abs(shift.z) < 0.01 ? "No Shift" :
                    shift.z < 0 ? $"Out: {Math.Abs(shift.z / 10.0):F2}" : $"In: {Math.Abs(shift.z / 10.0):F2}"
                };
            }
        }
    }
}
