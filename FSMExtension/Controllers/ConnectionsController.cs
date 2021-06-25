using FSMExtension.Data;
using FSMExtension.Models;
using FSMExtension.Models.Fsm;
using FSMExtension.Repositories;
using FSMExtension.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FSMExtension.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ConnectionsController : ControllerBase
    {
        public ConnectionsController(
            IOnsightConnectService connectService,
            IFsmApiService fsmApiService,
            IDomainMappingRepository domainRepo,
            ILogger<ConnectionsController> logger)
        {
            OnsightConnectService = connectService;
            FsmApiService = fsmApiService;
            DomainRepository = domainRepo;
            FsmMetadataBuilder = new FsmMetadataBuilder();
            Logger = logger;
        }

        private IOnsightConnectService OnsightConnectService { get; }

        private IFsmApiService FsmApiService { get; }

        private IDomainMappingRepository DomainRepository { get; }

        private IMetadataBuilder FsmMetadataBuilder { get; }

        private ILogger<ConnectionsController> Logger { get; }

        [HttpGet]
        [Route("fsm/connection")]
        public async Task<IActionResult> GetConnection(
            [FromQuery] string from,
            [FromQuery] string to,
            [FromQuery] string meta)
        {
            Logger.LogDebug($"GetConnection: from={from}, to={to}, meta={meta}");

            // Try finding the Onsight API Key corresponding to the 'from' email address
            var domainMapping = await DomainRepository.GetFromUserEmailAsync(from);
            Logger.LogDebug($"GetConnection get domain_mapping success = {domainMapping != null}");
            if (domainMapping == null)
                return NotFound();

            // Generate metadata structure based on CrmSource and 'meta' string
            var metadata = FsmMetadataBuilder.Build(meta);

            // Fetch and return Onsight Connect URL using APIKey + 'from' + 'to' + metadata struct
            var platform = Utils.DetectPlatform(Request);
            Logger.LogInformation($"Detected platform {platform}.");
            var uri = await OnsightConnectService.GetUriAsync(platform, domainMapping.OnsightApiKey, from, to, metadata);

            if (string.IsNullOrEmpty(uri))
                return NotFound();

            return Ok(uri);
        }

        /// <summary>
        /// Gets a list of Connections defined within the given CRM source object.
        /// </summary>
        /// <param name="crm"></param>
        /// <param name="fromEmail"></param>
        /// <param name="crmSourceId"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [Route("fsm/connections")]
        public async Task<IActionResult> GetConnections(
            [FromQuery(Name = "h")] string cloudHost,
            [FromQuery(Name = "a")] string accountId,
            [FromQuery(Name = "c")] string companyId,
            [FromQuery(Name = "av")] string activityId,
            [FromQuery(Name = "u")] string userId,
            [FromQuery(Name = "from")] string fromEmail)
        {
            var companyIdentifier = new FsmUserId(cloudHost, accountId, companyId, userId);

            // Look up FSM company (and its associated FSM auth token) based on 'accountId' + 'companyId'
            var domainMapping = await DomainRepository.GetFromFsmAccountIdAsync(accountId);
            var company = domainMapping.FsmAccount.FindCompany(companyId);
            if (company == null)
                return NotFound();

            // Get activity details based on 'crmSourceId'
            var activity = await FsmApiService.GetActivityAsync(cloudHost, company, activityId);
            if (activity == null)
                return NotFound();

            var contacts = new List<Contact>();

            // Get remote expert (there are two different options)
            var remoteExpert = await GetRemoteExpertAsync(cloudHost, company, activity, fromEmail);
            if (remoteExpert != null)
                contacts.Add(remoteExpert);

            // Get responsible's details from activity.responsibles[]. This is the assigned field worker.
            var responsibles = await FsmApiService.GetPersonsAsync(cloudHost, company, activity.Responsibles);
            contacts.AddRange(responsibles.Select(r =>
            {
                return new Contact
                {
                    Name = $"{r.FirstName} {r.LastName}",
                    Title = r.PositionName ?? r.JobTitle,
                    Role = ContactRole.FieldTech,
                    Connection = Url.Action(
                        nameof(GetConnection),
                        "Connections",
                        new
                        {
                            from = fromEmail,
                            to = r.EmailAddress,
                            meta = $"eqp:{activity.EquipmentId};act:{activityId}"
                        }
                    )
                };
            }));

            foreach (var c in contacts)
            {
                Logger.LogDebug($"/connections generated URL '{c.Connection}'");
            }

            return Ok(contacts);
        }

        /// <summary>
        /// Fetches contact information about the Activity's remote expert. The expert is either:
        ///     a) designated using custom fields associated with the Activity's equipment, or
        ///     b) the Activity's Contact.
        /// </summary>
        /// <param name="cloudHost"></param>
        /// <param name="company"></param>
        /// <param name="activity"></param>
        /// <param name="fromEmail"></param>
        /// <returns></returns>
        private async Task<Contact> GetRemoteExpertAsync(string cloudHost, CompanyInfo company, Dtos.FsmActivity activity, string fromEmail)
        {
            var expertName = string.Empty;
            var expertEmail = string.Empty;
            var expertTitle = string.Empty;

            // Use Equipment's designated expert, if available
            var equipmentContact = await FsmApiService.GetEquipmentContactAsync(cloudHost, company, activity.EquipmentId);
            if (equipmentContact != null)
            {
                expertName = equipmentContact.FirstName;
                expertEmail = equipmentContact.EmailAddress;
            }

            if (string.IsNullOrEmpty(expertEmail))
            {
                // Otherwise, fall back to using the Activity's Contact
                var activityContact = await FsmApiService.GetContactAsync(cloudHost, company, activity.Contact);
                expertEmail = activityContact.EmailAddress;

                // Bail out if we don't have an "expert" associated with the Activity
                if (string.IsNullOrEmpty(expertEmail))
                    return null;

                expertName = $"{activityContact.FirstName} {activityContact.LastName}";
                expertTitle = activityContact.PositionName;
            }

            return new Contact
            {
                Name = expertName,
                Title = expertTitle,
                Role = ContactRole.Expert,
                Connection = Url.Action(
                        nameof(GetConnection),
                        "Connections",
                        new
                        {
                            from = fromEmail,
                            to = expertEmail,
                            meta = $"eqp:{activity.EquipmentId};act:{activity.Id}"
                        }
                    )
            };
        }
    }
}
