using System;
using System.Collections.Generic;

namespace FSMExtension.Models
{
    public class WorkFlow
    {
        public string workflowId { get; set; }
        public DateTime created { get; set; }
        public DateTime lastUpdated { get; set; }
        public string activeVersionId { get; set; }
        public string teamName { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public List<Version> versions { get; set; }
        public bool isArchived { get; set; }
        public string workflowIdForDraft { get; set; }
        public string workflowIdForApproval { get; set; }
        public bool approvalsMigrated { get; set; }
        public List<string> liveObjectIds { get; set; }
    }

    public class Version
    {
        public string versionId { get; set; }
        public string fileReference { get; set; }
        public int fileSize { get; set; }
        public DateTime uploaded { get; set; }
        public string authorId { get; set; }
        public string authorName { get; set; }
        public string versionNotes { get; set; }
        public List<string> approvals { get; set; }
        public string mode { get; set; }
        public bool isArchived { get; set; }
        public string downloadSignature { get; set; }
        public DateTime downloadSignatureExpiry { get; set; }
    }

    public class FlowUser
    {
        public string userId { get; set; }
        public string username { get; set; }
        public string emailAddress { get; set; }
        public string name { get; set; }
        public DateTime created { get; set; }
        public bool locked { get; set; }
    }

    public class ReportResponse
    {
        public string jobId { get; set; }
        public string status { get; set; }
        public string reportUrl { get; set; }
    }

    public class FlowReport
    {
        public string blobData;
        public string jobTitle;
        public string workOrderId;
    }

    public class JobOfWorkFlow
    {
        public string jobId { get; set; }
        public Metadata metadata { get; set; }
        public List<CompletedSteps> completedSteps { get; set; }
        public string team { get; set; }
        public bool excluded { get; set; }
        public string completedReportURL { get; set; }
    }

    public class CurrentStep
    {
        public SingleStep singleStep { get; set; }
        public ReportStep reportStep { get; set; }
    }

    public class CompletedSteps
    {
        public SingleStep singleStep { get; set; }
        public ReportStep reportStep { get; set; }
    }

    public class ReportStep : SingleStep
    {
        public List<SingleStep> steps { get; set; }
    }

    public class Metadata
    {
        public string clientJobId { get; set; }
        public string workflowId { get; set; }
        public string workflowVersionId { get; set; }
        public string jobTitle { get; set; }
        public Dictionary<string, string> metadata { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public DateTime modified { get; set; }
        public string status { get; set; }
        public string userId { get; set; }
        public SingleStep currentStep { get; set; }
        public string username { get; set; }
        public string workflowName { get; set; }
    }

    public class SingleStep
    {
        public string[] values { get; set; }
        public string[] valueResourceIds { get; set; }
        public string uniqueStepId { get; set; }
        public string previousUniqueStepId { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string deviceId { get; set; }
        public int? stepNumber { get; set; }
        public string stepId { get; set; }
        public string stepTitle { get; set; }
        public string stepDescription { get; set; }
        public string stepType { get; set; }
        public string connectionType { get; set; }
        public DateTime started { get; set; }
        public DateTime completed { get; set; }
        public DateTime updated { get; set; }
        public string cancelled { get; set; }
        public Dictionary<string, int> timeEvents { get; set; }
        public string note { get; set; }
        //public string? coordinates { get; set; }
        public string parentStepId { get; set; }
        public string parentUniqueStepId { get; set; }
        public string parentStepTitle { get; set; }
        public string parentStepDescription { get; set; }
        public Dictionary<string, string> metadata { get; set; }
    }
}


