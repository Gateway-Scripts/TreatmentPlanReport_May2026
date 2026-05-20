using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TreatmentPlanReport.Helpers
{
    public static class DocumentServices
    {
        //Configure ARIA API URLs
        public static string tokenUrl = "https://master-ae.vic.com:44333/tokenservice/connect/token";
        public static string baseUrl = "https://master-ae.vic.com:55370/fhir/r4"; // Changed to match tokenUrl domain
        //TODO Replace here with your own client id and secret
        //===================================
        public static string clientId = "c8f6cdaf-b492-4958-a75a-9a3730c05e01";
        public static string clientSecret = "GatewayScripts_Varian!2026";
        public static string scopes = "system/ActivityDefinition.rs system/AllergyIntolerance.cruds system/AllergyIntolerance.rs system/Appointment.cruds system/Appointment.rs system/AuditEvent.c system/AuditEvent.cruds system/BodyStructure.rs system/CarePlan.rs system/CareTeam.cruds system/CareTeam.rs system/ChargeItem.cruds system/ChargeItem.rs system/Condition.cruds system/Condition.rs system/Device.rs system/DocumentReference.cruds system/DocumentReference.rs system/Group.rs system/HealthcareService.rs system/Location.rs system/Observation.rs system/Organization.rs system/Patient.cruds system/Patient.rs system/Practitioner.cruds system/Practitioner.rs system/Procedure.rs system/ServiceRequest.rs system/Task.cruds system/Task.rs system/ValueSet.rs user/ActivityDefinition.rs user/AllergyIntolerance.cruds user/AllergyIntolerance.rs user/Appointment.cruds user/Appointment.rs user/AuditEvent.c user/AuditEvent.cruds user/BodyStructure.rs user/CarePlan.rs user/CareTeam.cruds user/CareTeam.rs user/ChargeItem.cruds user/ChargeItem.rs user/Condition.cruds user/Condition.rs user/Device.rs user/DocumentReference.cruds user/DocumentReference.rs user/Group.rs user/HealthcareService.rs user/Location.rs user/Observation.rs user/Organization.rs user/Patient.cruds user/Patient.rs user/Practitioner.cruds user/Practitioner.rs user/Procedure.rs user/ServiceRequest.rs user/Task.cruds user/Task.rs user/ValueSet.rs";
        public static HttpClient client;
        //public static string token; 
        /// <summary>
        /// Connects tot he ARIA API and generates a token.
        /// </summary>
        public static void GenerateClient()
        {
            Console.WriteLine($"=== GenerateClient Starting ===");
            Console.WriteLine($"Token URL: {tokenUrl}");
            Console.WriteLine($"Base URL: {baseUrl}");
            Console.WriteLine($"Client ID: {clientId}");

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            client = new HttpClient(handler);

            Dictionary<string, string> credentials = new Dictionary<string, string>();
            credentials.Add("grant_type", "client_credentials");
            credentials.Add("client_id", clientId);
            credentials.Add("client_secret", clientSecret);
            credentials.Add("scope", scopes);

            Console.WriteLine("Requesting bearer token");
            try
            {
                var response = client.PostAsync(tokenUrl, new FormUrlEncodedContent(credentials));
                var result = response.Result.Content.ReadAsStringAsync();
                Console.WriteLine($"Token response status: {response.Result.StatusCode}");

                var tokenJson = JObject.Parse(result.Result);
                var token = tokenJson["access_token"].ToString();
                Console.WriteLine($"Bearer token acquired successfully (length: {token.Length})");

                //now that we have the bearer token, this is the authentication mechanism.
                //set the authorization of the client.
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");
                Console.WriteLine("=== GenerateClient Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GenerateClient: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public static bool InsertDocument(string filePath, string patientId, string docType,
            string hospitalId)
        {
            Console.WriteLine($"=== Starting InsertDocument ===");
            Console.WriteLine($"File: {filePath}");
            Console.WriteLine($"File Size: {new FileInfo(filePath).Length} bytes");
            Console.WriteLine($"Patient ID: {patientId}");
            Console.WriteLine($"Hospital ID: {hospitalId}");
            Console.WriteLine($"Doc Type: {docType}");
            Console.WriteLine($"Base URL: {baseUrl}");

            //patient FHIR ID (what patient gets the document).
            string patientFhirId = GetIdFromSearch("Patient", new Dictionary<string, string> { { "identifier", patientId } });
            Console.WriteLine($"Patient FHIR ID: {patientFhirId}");

            //hospital FHIR ID (what organization is uploading the document).
            string hospitalFhirId = GetIdFromSearch("Organization", new Dictionary<string, string> { { "name", hospitalId }, { "type", "prov" },{ "active", "true" } });
            Console.WriteLine($"Hospital FHIR ID: {hospitalFhirId}");

            //find the document type from ARIA
            var validDocTypes = GetValidDocumentTypes(hospitalFhirId);
            Console.WriteLine($"Valid doc types count: {validDocTypes.Count}");

            string docCode = validDocTypes.FirstOrDefault(v => v.Value == docType).Key ?? "1"; // Use first valid type or fallback
            Console.WriteLine($"Doc Code: {docCode}");

            string docCategory = "Patient Document"; // This can be adjusted based on your needs.
            Console.WriteLine($"Doc Category: {docCategory}");

            var docRef = CreateDocumentReference(patientFhirId, filePath, docCode, docType, docCategory, hospitalFhirId);

            string response = PostDocumentReference(baseUrl, client, docRef);
            Console.WriteLine($"=== InsertDocument Complete ===");
            //MessageBox.Show(response);
            return true;

        }
        private static DocumentReference CreateDocumentReference(string patientFhirId, string filePath, string docCode, string documentType, string documentCategory, string documentLocation)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string base64Content = Convert.ToBase64String(fileBytes);
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLower();

            string contentType = "application/octet-stream";
            if (extension == ".pdf") contentType = "application/pdf";
            else if (extension == ".txt") contentType = "text/plain";
            else if (extension == ".doc") contentType = "application/msword";
            else if (extension == ".docx") contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            else if (extension == ".jpg" || extension == ".jpeg") contentType = "image/jpeg";
            else if (extension == ".png") contentType = "image/png";
            else if (extension == ".tif" || extension == ".tiff") contentType = "image/tiff";

            var docRef = new DocumentReference
            {
                Status = "current",
                DocStatus = "final",
                Subject = new Reference
                {
                    ReferenceValue = $"Patient/{patientFhirId}"
                },
                Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Description = fileName,
                Type = new CodeableConcept
                {
                    Coding = new List<Coding>
                    {
                        new Coding
                        {
                            System = "http://varian.com/fhir/CodeSystem/documentreference-type",
                            Code = docCode,
                            Display = documentType
                        }
                    }
                },
                Category = new List<CodeableConcept>
                {
                    new CodeableConcept
                    {
                        Coding = new List<Coding>
                        {
                            new Coding
                            {
                                Code = documentCategory,
                                Display = documentCategory
                            }
                        }
                    }
                },
                Content = new List<DocumentReferenceContent>
                {
                    new DocumentReferenceContent
                    {
                        Attachment = new Attachment
                        {
                            ContentType = contentType,
                            Data = base64Content,
                            Title = fileName,
                            Creation = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }
                    }
                }
            };

            //docRef.AddDocumentLocation(documentLocation);

            return docRef;
        }

        private static string PostDocumentReference(string baseUrl, HttpClient client, DocumentReference docRef)
        {
            string json = JsonConvert.SerializeObject(docRef, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            });

            Console.WriteLine("Posting DocumentReference:");
            Console.WriteLine(json);

            var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
            var response = client.PostAsync($"{baseUrl}/DocumentReference", content);
            var result = response.Result.Content.ReadAsStringAsync().Result;

            Console.WriteLine($"Response Status: {response.Result.StatusCode}");
            Console.WriteLine($"Response: {result}");

            if (response.Result.IsSuccessStatusCode)
            {
                if (String.IsNullOrEmpty(result))
                {
                    Console.WriteLine("No Result Text");
                    return String.Empty;
                }
                var responseJson = JObject.Parse(result);
                return responseJson["id"].ToString();
            }
            else
            {
                return $"Failed to create DocumentReference: {result}";
            }
        }
        private static Dictionary<string, string> GetValidDocumentTypes(string publisher)
        {
            var documentTypes = new Dictionary<string, string>();
            try
            {
                string valueSetUrl = $"{baseUrl}/ValueSet/$expand?url=http://varian.com/fhir/ValueSet/documentreference-type&publisher={publisher}";
                Console.WriteLine($"Querying: {valueSetUrl}");
                var response = client.GetAsync(valueSetUrl);
                var result = response.Result.Content.ReadAsStringAsync().Result;

                if (response.Result.IsSuccessStatusCode)
                {

                    var valueSetJson = JObject.Parse(result);
                    foreach (var entry in valueSetJson["entry"])
                    {
                        var expansion = entry["resource"]["expansion"];
                        if (expansion != null && expansion["contains"] != null)
                        {
                            foreach (var item in expansion["contains"])
                            {
                                documentTypes.Add(item["code"]?.ToString(), item["display"]?.ToString());
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Could not fetch document types. Status: {response.Result.StatusCode}");
                    Console.WriteLine($"Response: {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error fetching document types: {ex.Message}");
            }

            return documentTypes;
        }

        private static string GetIdFromSearch(string profile, Dictionary<string, string> search)
        {
            string searchParams = String.Join("&", search.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string patientUrl = $"{baseUrl}/{profile}?{searchParams}&_pretty=true";
            var patientResponse = client.GetAsync(patientUrl);
            var patientResult = patientResponse.Result.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"{profile} Idenfitied: {patientResult}");
            return JObject.Parse(patientResult)["entry"][0]["resource"]["id"].ToString();
        }

    }
}
