using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TreatmentPlanReport.Helpers
{
    public class DocumentReference
    {
        [JsonProperty("resourceType")]
        public string ResourceType { get; set; } = "DocumentReference";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }

        [JsonProperty("extension")]
        public List<Extension> Extension { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } // current | entered-in-error

        [JsonProperty("docStatus")]
        public string DocStatus { get; set; } // preliminary | final | entered-in-error | amended

        [JsonProperty("type")]
        public CodeableConcept Type { get; set; }

        [JsonProperty("category")]
        public List<CodeableConcept> Category { get; set; }

        [JsonProperty("subject")]
        public Reference Subject { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; } // ISO 8601 format (instant)

        [JsonProperty("author")]
        public List<Reference> Author { get; set; }

        [JsonProperty("authenticator")]
        public Reference Authenticator { get; set; }

        [JsonProperty("custodian")]
        public Reference Custodian { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("content")]
        public List<DocumentReferenceContent> Content { get; set; }

        [JsonProperty("context")]
        public DocumentReferenceContext Context { get; set; }
    }

    public class Meta
    {
        [JsonProperty("versionId")]
        public string VersionId { get; set; }

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; }

        [JsonProperty("profile")]
        public List<string> Profile { get; set; }

        [JsonProperty("security")]
        public List<Coding> Security { get; set; }

        [JsonProperty("tag")]
        public List<Coding> Tag { get; set; }
    }

    public class Extension
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("valueString")]
        public string ValueString { get; set; }

        [JsonProperty("valueDateTime")]
        public string ValueDateTime { get; set; }

        [JsonProperty("valueReference")]
        public Reference ValueReference { get; set; }

        // Add other value[x] types as needed
    }

    public class CodeableConcept
    {
        [JsonProperty("coding")]
        public List<Coding> Coding { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class Coding
    {
        [JsonProperty("system")]
        public string System { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }
    }

    public class Reference
    {
        [JsonProperty("reference")]
        public string ReferenceValue { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }
    }

    public class Identifier
    {
        [JsonProperty("system")]
        public string System { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class DocumentReferenceContent
    {
        [JsonProperty("attachment")]
        public Attachment Attachment { get; set; }
    }

    public class Attachment
    {
        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; } // Base64 encoded

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("size")]
        public int? Size { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("creation")]
        public string Creation { get; set; }
    }

    public class DocumentReferenceContext
    {
        [JsonProperty("related")]
        public List<Reference> Related { get; set; }
    }
}