using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Helpers;
using TreatmentPlanReport.Models;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TreatmentPlanReport.ViewModels
{
    public class DVHViewModel
    {
        public ObservableCollection<StructureSelectionModel> Structures { get; set; }
        public PlotModel DVHPlot { get; private set; }

        private PlanningItem _plan;
        private EventHelper _eventHelper;

        public DVHViewModel(PlanningItem plan, EventHelper eventHelper)
        {
            Structures = new ObservableCollection<StructureSelectionModel>();
            _plan = plan;
            _eventHelper = eventHelper;
            SetPlotProperties();

            _eventHelper.Subscribe<StructureSelectionModel>("StructureSelectionChanged", OnStructureSelectionChanged);
            SetDefaults();
            //GenerateDVH();

        }



        public void SetDefaults()
        {
            Structures.Clear();
            foreach (var structure in _plan.StructureSet.Structures.Where(st => st.DicomType != "MARKER" && st.DicomType != "SUPPORT"))
            {
                Structures.Add(new StructureSelectionModel(_eventHelper)
                {
                    StructureId = structure.Id,
                    IsSelected = structure.DicomType.Contains("TV") || structure.DicomType.Equals("ORGAN")

                });
            }
        }


        private void SetPlotProperties()
        {
            DVHPlot = new PlotModel();
            DVHPlot.Title = $"DVH for {_plan.Id}";
            DVHPlot.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.RightTop,
                LegendPlacement = LegendPlacement.Outside,
                //LegendOrientation = LegendOrientation.Horizontal,
            });
            DVHPlot.Axes.Add(new LinearAxis
            {
                Title = $"Dose [{GetDoseUnit()}]",
                Position = AxisPosition.Bottom
            });
            DVHPlot.Axes.Add(new LinearAxis
            {
                Title = $"Volume [%]",
                Position = AxisPosition.Left
            });
        }

        private void OnStructureSelectionChanged(StructureSelectionModel model)
        {
            if (model.IsSelected)
            {
                //draw DVH
                GeneratePlotForStructure(model);
            }
            else
            {
                //remove DVH plot
                if (DVHPlot.Series.Any(s => s.Title.Equals(model.StructureId)))
                {
                    var seriesToRemove = DVHPlot.Series.FirstOrDefault(s => s.Title.Equals(model.StructureId));
                    DVHPlot.Series.Remove(seriesToRemove);
                    DVHPlot.InvalidatePlot(true);
                }
            }
        }
        public void GeneratePlotForStructure(StructureSelectionModel structureModel)
        {
            Structure structure = _plan.StructureSet.Structures.FirstOrDefault(st => st.Id == structureModel.StructureId);
            DVHData dvhData = _plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.Relative, 1);
            LineSeries series = new LineSeries()
            {
                Title = structure.Id,
                Color = OxyColor.FromRgb(structure.Color.R, structure.Color.G, structure.Color.B)
            };
            foreach (var dvhPoint in dvhData.CurveData)
            {
                series.Points.Add(new DataPoint(dvhPoint.DoseValue.Dose, dvhPoint.Volume));
            }
            DVHPlot.Series.Add(series);
            DVHPlot.InvalidatePlot(true);
        }


        private string GetDoseUnit()
        {
            string doseUnit = string.Empty;
            if (_plan is PlanSetup)
            {
                //this is a plan setup
                doseUnit = (_plan as PlanSetup).TotalDose.UnitAsString;
            }
            else
            {
                //this is a plan sum
                var planSetup = (_plan as PlanSum).PlanSetups.FirstOrDefault();
                doseUnit = planSetup.TotalDose.UnitAsString;
            }

            return doseUnit;
        }
    }
}
