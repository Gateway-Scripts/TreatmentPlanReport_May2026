using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TreatmentPlanReport.Helpers;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TreatmentPlanReport.ViewModels
{
    public class IsoOrthogonalViewModel
    {
        private readonly Patient _patient;
        private readonly PlanningItem _planningItem;
        private readonly Course _course;
        private readonly bool _isElectronPlan;

        /// <summary>
        /// Axial/Transverse view image at the isocenter or CAX Dmax point.
        /// </summary>
        public BitmapSource TransverseImage { get; set; }

        /// <summary>
        /// Coronal/Frontal view image at the isocenter or CAX Dmax point.
        /// </summary>
        public BitmapSource FrontalImage { get; set; }

        /// <summary>
        /// Sagittal view image at the isocenter or CAX Dmax point.
        /// </summary>
        public BitmapSource SagittalImage { get; set; }

        /// <summary>
        /// Title displayed for the orthogonal views.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Initializes a new instance of the IsoOrthogonalViewModel class.
        /// </summary>
        /// <param name="patient">The current patient.</param>
        /// <param name="course">The treatment course.</param>
        /// <param name="plan">The plan setup to visualize.</param>
        public IsoOrthogonalViewModel(Patient patient, Course course, PlanSetup plan)
        {
            _patient = patient;
            _planningItem = plan;
            _course = course;
            _isElectronPlan = plan.Beams.FirstOrDefault(x => !x.IsSetupField).EnergyModeDisplayName.Contains("E");
            Title = _isElectronPlan ? "CAX DMax Orthogonal Views" : "Isocenter Orthogonal Views";
            GenerateOrthogonalImages();
        }
        /// <summary>
        /// Generates all three orthogonal view images (transverse, frontal, and sagittal).
        /// </summary>
        private void GenerateOrthogonalImages()
        {
            // Determine the center point for imaging based on plan type
            VVector imageCenter = DetermineImageCenter();

            // Select structures to render (targets, organs, and support structures)
            List<Structure> structuresToRender = GetStructuresToRender();

            // Generate all three orthogonal views with dose overlay
            TransverseImage = OrthogonalRenderer.GetTransverseImage(_planningItem, imageCenter, structuresToRender, includeDose: true);
            FrontalImage = OrthogonalRenderer.GetFrontalImage(_planningItem, imageCenter, structuresToRender, includeDose: true);
            SagittalImage = OrthogonalRenderer.GetSagittalImage(_planningItem, imageCenter, structuresToRender, includeDose: true);
        }

        /// <summary>
        /// Determines the center point for orthogonal imaging.
        /// For electron plans or plan sums, uses CAX Dmax point.
        /// For photon plans, uses the isocenter position.
        /// </summary>
        /// <returns>The 3D position to center the orthogonal views on.</returns>
        private VVector DetermineImageCenter()
        {
            // Electron plans: use CAX Dmax (point of maximum dose along central axis)
            if (_isElectronPlan)
            {
                return GetCAXDmax();
            }

            // Plan sums: CAX Dmax handles composite plans better
            if (_planningItem is PlanSum)
            {
                return GetCAXDmax();
            }

            // Photon plans: use first treatment beam's isocenter
            return (_planningItem as PlanSetup).Beams.FirstOrDefault(x => !x.IsSetupField).IsocenterPosition;
        }

        /// <summary>
        /// Retrieves structures that should be rendered in the orthogonal views.
        /// Includes target volumes (TV), organs at risk, and support structures.
        /// </summary>
        /// <returns>List of structures to render.</returns>
        private List<Structure> GetStructuresToRender()
        {
            return _planningItem.StructureSet.Structures
                .Where(ss => ss.DicomType.Contains("TV") ||
                            ss.DicomType == "ORGAN" ||
                            ss.DicomType == "SUPPORT")
                .ToList();
        }
        /// <summary>
        /// Calculates the CAX (Central Axis) Dmax point for electron beams.
        /// This is the point along the central axis where the dose is maximum.
        /// For photon plan sums, returns the most recently approved plan's isocenter.
        /// </summary>
        /// <returns>The 3D position of the CAX Dmax point.</returns>
        private VVector GetCAXDmax()
        {
            if (_planningItem is PlanSum planSum)
            {
                return GetCAXDmaxForPlanSum(planSum);
            }
            else
            {
                return GetCAXDmaxForPlanSetup(_planningItem as PlanSetup);
            }
        }

        /// <summary>
        /// Calculates CAX Dmax for a plan sum, handling mixed photon/electron plans.
        /// </summary>
        private VVector GetCAXDmaxForPlanSum(PlanSum planSum)
        {
            bool allPhoton = planSum.PlanSetups.All(ps =>
                ps.Beams.Where(b => !b.IsSetupField).Any(b => b.EnergyModeDisplayName.Contains("X")));

            bool hasPhoton = planSum.PlanSetups.Any(ps =>
                ps.Beams.Where(b => !b.IsSetupField).Any(b => b.EnergyModeDisplayName.Contains("X")));

            // If all plans are photon, use the most recently approved plan's isocenter
            if (allPhoton)
            {
                return planSum.PlanSetups
                    .Where(ps => ps.ApprovalHistory.Count() > 0 && ps.Beams != null)
                    .OrderByDescending(ps => ps.ApprovalHistory.Max(ah => ah.ApprovalDateTime))
                    .First()
                    .Beams.FirstOrDefault(b => !b.IsSetupField)
                    .IsocenterPosition;
            }

            // If there's at least one photon plan, use that plan's isocenter
            if (hasPhoton)
            {
                return planSum.PlanSetups
                    .FirstOrDefault(ps => ps.Beams.Where(b => !b.IsSetupField).Any(b => b.EnergyModeDisplayName.Contains("X")))
                    .Beams.FirstOrDefault(b => !b.IsSetupField)
                    .IsocenterPosition;
            }

            // All fields are electron - find Dmax along central axis
            var electronPlan = planSum.PlanSetups.FirstOrDefault(ps => ps.Beams.Any(b => b.EnergyModeDisplayName.Contains("E")));
            var firstBeam = electronPlan.Beams.FirstOrDefault(b => !b.IsSetupField);
            return CalculateDmaxPosition(firstBeam);
        }

        /// <summary>
        /// Calculates CAX Dmax for a single plan setup.
        /// </summary>
        private VVector GetCAXDmaxForPlanSetup(PlanSetup plan)
        {
            var firstBeam = plan.Beams.FirstOrDefault(b => !b.IsSetupField);
            return CalculateDmaxPosition(firstBeam);
        }

        /// <summary>
        /// Calculates the Dmax position along the central axis of an electron beam.
        /// Samples dose along the beam axis and finds the point with maximum dose.
        /// </summary>
        /// <param name="beam">The beam to analyze.</param>
        /// <returns>The 3D position of maximum dose along the beam's central axis.</returns>
        private VVector CalculateDmaxPosition(Beam beam)
        {
            // Get source position at the beam's gantry angle
            var sourcePosition = beam.GetSourceLocation(beam.ControlPoints.First().GantryAngle);
            var isocenterPosition = beam.IsocenterPosition;

            // Extend beyond isocenter by 20% to ensure we capture Dmax
            VVector extendedPosition = new VVector(
                isocenterPosition.x + 0.2 * (isocenterPosition.x - sourcePosition.x),
                isocenterPosition.y + 0.2 * (isocenterPosition.y - sourcePosition.y),
                isocenterPosition.z + 0.2 * (isocenterPosition.z - sourcePosition.z));

            // Sample dose along the beam axis
            int sampleCount = (int)VVector.Distance(sourcePosition, extendedPosition);
            var doseProfile = beam.Dose.GetDoseProfile(sourcePosition, extendedPosition, new double[sampleCount]);

            // Find position with maximum dose
            return doseProfile.OrderByDescending(dp => dp.Value).First().Position;
        }

    }
}
