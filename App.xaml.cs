using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TreatmentPlanReport.ViewModels;
using TreatmentPlanReport.Views;
using esapi = VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string _patientId;
        private string _courseId;
        private string _planId;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //initialize ESAPI. 
            //wrap this code in a try/catch to handle any exceptions that may occur.
            //remember to include a using block around the application creation.
            try
            {
                //get patient id, course id, and plan ids from the input args.
                if (e.Args.Length > 0)
                {
                    _patientId = e.Args.FirstOrDefault().Split(';').FirstOrDefault();
                    _courseId = e.Args.FirstOrDefault().Split(';').ElementAt(1);
                    _planId = e.Args.FirstOrDefault().Split(';').Last();
                }
                //after using completes, the app object will call Dispose();
                using (var app = esapi.Application.CreateApplication())
                {
                    //open a patient course and plan, then launch the application.
                    //default for now.
                    esapi.Patient patient = app.OpenPatientById("RapidPlan-01");
                    //System.Diagnostics.Debugger.Break();
//                    System.Threading.Thread.Sleep(3000);

                    esapi.Course course = patient.Courses.FirstOrDefault(c => c.Id == "Demo");
                    esapi.PlanSetup plan = course.PlanSetups.FirstOrDefault(p => p.Id == "IMRT Calc");
                    var mainView = new MainView();
                    var mainViewModel = new MainViewModel(patient,plan);
                    //tell the mainView to evaluate binding expressions using properties of the MainViewModel class.
                    mainView.DataContext = mainViewModel;
                    //show the UI.
                    mainView.ShowDialog();
                }
            }
            catch(Exception ex)
            {

            }
        }
    }
}
