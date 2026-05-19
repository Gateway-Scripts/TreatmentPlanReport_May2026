using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
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
        public RelayCommand PrintReportCommand { get; private set; }
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
            PrintReportCommand = new RelayCommand(OnPrint);
        }

        private void OnPrint(object obj)
        {
            FlowDocument fd = new FlowDocument { FontSize = 12, FontFamily = new System.Windows.Media.FontFamily("Calibri") };
            fd.Blocks.Add(new Paragraph(new Run("Treatment Plan Report")));
            fd.Blocks.Add(new Paragraph(new Run("Patient Info:")) { FontWeight = FontWeights.Bold });
            fd.Blocks.Add(new BlockUIContainer(new PatientView { DataContext = LocalPatientViewModel }));
            fd.Blocks.Add(new Paragraph(new Run("Plan Info:")) { FontWeight = FontWeights.Bold });
            fd.Blocks.Add(new BlockUIContainer(new PlanView { DataContext = LocalPlanViewModel }));
            fd.Blocks.Add(new Paragraph(new Run("Prescription:")) { FontWeight = FontWeights.Bold });
            fd.Blocks.Add(new BlockUIContainer(new RxView { DataContext = LocalRxViewModel }));
            //fd.Blocks.Add(new Paragraph(new Run("Reference Point:")) { FontWeight = FontWeights.Bold });
            //fd.Blocks.Add(new BlockUIContainer(new ReferencePointView { DataContext = ReferencePointViewModel }));
            fd.Blocks.Add(new Paragraph(new Run("Patient Shifts:")) { FontWeight = FontWeights.Bold });
            fd.Blocks.Add(new BlockUIContainer(new PatientShiftView { DataContext = LocalPatientShiftViewModel }));
            //foreach (var field in FieldViewModel.Fields)
            //{
            //    fd.Blocks.Add(new BlockUIContainer(new FieldDetailsView { DataContext = field }));
            //}
            Section fieldSection = new Section { BreakPageBefore = true };
            fieldSection.Blocks.Add(new Paragraph(new Run("Field Info:")) { FontWeight = FontWeights.Bold });
            fieldSection.Blocks.Add(new BlockUIContainer(new FieldSummaryView { DataContext = LocalFieldViewModel }));
            fd.Blocks.Add(fieldSection);

            BitmapSource bmp = new PngExporter().ExportToBitmap(LocalDVHViewModel.DVHPlot);
            fd.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image
            {
                Source = bmp,
                Height = 600,
                Width = 725
            }));
            fd.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image { Source = LocalOrthogonalImageViewModel.TransverseImage }));
            fd.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image { Source = LocalOrthogonalImageViewModel.FrontalImage ,
                    Margin = new System.Windows.Thickness(10)}));
            fd.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Image { Source = LocalOrthogonalImageViewModel.SagittalImage,
                    Margin= new System.Windows.Thickness(10)}));
            System.Windows.Controls.PrintDialog printer = new System.Windows.Controls.PrintDialog();
            //printer.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
            fd.PageHeight = 1056;
            fd.PageWidth = 816;
            fd.PagePadding = new System.Windows.Thickness(50);
            fd.ColumnGap = 0;
            fd.ColumnWidth = 816;
            IDocumentPaginatorSource source = fd;
            if (printer.ShowDialog() == true)
            {
                printer.PrintDocument(source.DocumentPaginator, "TreatmentPlanReport");
            }
        }
    }
}
