using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace TreatmentPlanReport.ViewModels
{
    public class PatientViewModel
    {
        public string PatientId { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string DateOfBirth { get; set; }
        public string Hospital { get; set; }
        public string PrimaryOncologist { get; set; }
        public PatientViewModel(Patient patient)
        {
            PatientId = patient.Id;
            LastName = patient.LastName;
            FirstName = patient.FirstName;
            DateOfBirth = GetDOB(patient.DateOfBirth);
            Hospital = patient.Hospital.Id;
            PrimaryOncologist = patient.PrimaryOncologistName;
        }

        public string GetDOB(DateTime? dateOfBirth)
        {
            //ternary operator check for null DateOfBirth
            return dateOfBirth.HasValue ? dateOfBirth.Value.ToShortDateString() : "No DOB";
        }

        //parmeterless constructor for unit testing.
        public PatientViewModel()
        {
            
        }
    }
}
