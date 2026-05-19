using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreatmentPlanReport.Helpers;

namespace TreatmentPlanReport.Models
{
    public class StructureSelectionModel
    {
        //check to see if this requires INotifyPropertyChanged.
        private EventHelper _eventHelper;
        
        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                UpdatePlot();
            }
        }

        private void UpdatePlot()
        {
            if (_eventHelper != null)
            {
                _eventHelper.Publish<StructureSelectionModel>("StructureSelectionChanged", this);
            }
        }

        public string StructureId { get; set; }
        public string DicomType { get; internal set; }

        public StructureSelectionModel(EventHelper eventHelper)
        {
            _eventHelper = eventHelper;
        }
        public StructureSelectionModel()
        {

        }
    }
}
