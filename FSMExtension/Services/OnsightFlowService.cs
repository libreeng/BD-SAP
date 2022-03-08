using FSMExtension.Dtos;
using FSMExtension.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FSMExtension.Services
{
    public interface IOnsightFlowService
    {
        Task<List<Tuple<string, string, bool>>> AvailableWorkflowsAsync(CompanyInfo company, string selectedWorkflowId, string fieldtechEmail);
        Task<List<JobOfWorkFlow>> ImportJobsAsync(CompanyInfo company, FsmActivity activity);
    }
    public class OnsightFlowService : IOnsightFlowService
    {
        public OnsightFlowService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        private HttpClient HttpClient { get; }

        public async Task<List<Tuple<string, string, bool>>> AvailableWorkflowsAsync(
            CompanyInfo company,
            string selectedWorkFlowId,
            string fieldtechEmail)
        {
            var token = await GetFlowTokenAsync(company.Account.DomainMapping.OnsightApiKey, company.Account.DomainMapping.TeamName);
            var allUsersForTeam = await GetUsersForTeam(token);
            var userIdForAssignedFieldTech = allUsersForTeam.Where(aut => aut.emailAddress == fieldtechEmail).FirstOrDefault();
            var listOfWorkFlows = await GetAllWorkFlows(token);
            var assignedWorkFlows = listOfWorkFlows.Where(lwf => lwf.liveObjectIds != null && lwf.activeVersionId != null);
            var unassignedWorkFlows = listOfWorkFlows.Where(lwf => (lwf.liveObjectIds == null || lwf.liveObjectIds.Count == 0) && lwf.activeVersionId != null);
            var validWorkFlowsForFieldTech = assignedWorkFlows.Where(lwf => lwf.liveObjectIds.Contains(userIdForAssignedFieldTech?.userId ?? string.Empty)).ToList();

            validWorkFlowsForFieldTech.AddRange(unassignedWorkFlows.ToList());

            var validWFs = new List<Tuple<string, string, bool>>();
            foreach (var workFlow in validWorkFlowsForFieldTech)
            {
                var validWf = new Tuple<string, string, bool>(workFlow.workflowId, workFlow.name, selectedWorkFlowId == workFlow.workflowId);
                validWFs.Add(validWf);
            }
            return validWFs;
        }

        private async static Task<string> GetFlowTokenAsync(string token, string teamName)
        {
            string responseString = string.Empty;
            var requestUri = new Uri("https://flow-token-generator-app.azurewebsites.net/api/token");
            var message = new HttpRequestMessage(HttpMethod.Post, requestUri);
            message.Headers.Add("X-Api-Key", token);
            message.Content = System.Net.Http.Json.JsonContent.Create(new { teamName = teamName });

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                responseString = await response.Content.ReadAsStringAsync();
            }
            return responseString;
        }

        private async static Task<List<WorkFlow>> GetAllWorkFlows(string token)
        {
            var listWorkflows = new List<WorkFlow>();
            var requestUri = new Uri("https://gateway.flow.librestream.com/workflows/v2");
            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                listWorkflows = JsonSerializer.Deserialize<List<WorkFlow>>(responseString);
            }

            return listWorkflows;
        }

        private async static Task<List<FlowUser>> GetUsersForTeam(string token)
        {
            var flowUsers = new List<FlowUser>();
            var requestUri = new Uri("https://accounts.flow.librestream.com/api/team/user");
            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();

                flowUsers = JsonSerializer.Deserialize<List<FlowUser>>(responseString);
            }
            return flowUsers;
        }

        public async Task<List<JobOfWorkFlow>> ImportJobsAsync(
            CompanyInfo company,
            FsmActivity activity)
        {
            var allCompletedJobsForActivityWithDetails = new List<JobOfWorkFlow>();

            var token = await GetFlowTokenAsync(company.Account.DomainMapping.OnsightApiKey, company.Account.DomainMapping.TeamName);
            var allWorkFlowJobs = await GetJobsOfWorkflow(token);

            var getcompletedJobsForActivity = allWorkFlowJobs.Where(awj => awj.metadata.metadata.ContainsKey("sapActivityCode") && awj.metadata.metadata.ContainsValue(activity.Code));

            foreach (var jobOfWorkflow in getcompletedJobsForActivity)
            {
                if (jobOfWorkflow.metadata.status == "Completed")
                {
                    var completedJobsWithDetails = await GetAllJobsOfWorkFlowWithDetails(jobOfWorkflow.jobId, token);
                    allCompletedJobsForActivityWithDetails.Add(completedJobsWithDetails);
                }
            }

            // download the report data for job of workflow for the activity
            foreach (var workFlowJob in allCompletedJobsForActivityWithDetails)
            {
                var completedReport = await GetCompletedReport(workFlowJob.jobId, token);
                if (completedReport != null)
                {
                    workFlowJob.completedReportURL = completedReport.reportUrl;
                }
            }
            return allCompletedJobsForActivityWithDetails;
        }

        private async Task<ReportResponse> GetCompletedReport(string jobId, string token)
        {
            var reportResponse = new ReportResponse();

            var requestUri = new Uri("https://gateway.flow.librestream.com/reportgenerator/v1/generate?jobId=" + jobId);

            var message = new HttpRequestMessage(HttpMethod.Post, requestUri);
            message.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                reportResponse = JsonSerializer.Deserialize<ReportResponse>(responseString);                
            }
            return reportResponse;
        }

        private async static Task<JobOfWorkFlow> GetAllJobsOfWorkFlowWithDetails(string jobId, string token)
        {
            var jobOfWorkFlow = new JobOfWorkFlow();
            var requestUri = new Uri("https://gateway.flow.librestream.com/jobs/v1/bson/" + jobId);

            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                jobOfWorkFlow = JsonSerializer.Deserialize<JobOfWorkFlow>(responseString);
            }
            return jobOfWorkFlow;
        }

        private async static Task<List<JobOfWorkFlow>> GetJobsOfWorkflow(string token)
        {
            var jobsOfWorkFlow = new List<JobOfWorkFlow>();
            var requestUri = new Uri("https://gateway.flow.librestream.com/jobs/v1/");
            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Authorization", string.Format("Bearer {0}", token));

            var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(message);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                jobsOfWorkFlow = JsonSerializer.Deserialize<List<JobOfWorkFlow>>(responseString);
            }
            return jobsOfWorkFlow;
        }
    }
}
