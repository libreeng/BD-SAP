using FSMExtension.Data;
using FSMExtension.Models;
using FSMExtension.Models.Fsm;
using FSMExtension.Repositories;
using FSMExtension.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
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
            var emailDomain = Utils.ExtractEmailDomain(from);

            // Try finding the Onsight API Key corresponding to the 'from' email address
            var domainMapping = await DomainRepository.GetFromDomainAsync(emailDomain);
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
            var fromDomain = Utils.ExtractEmailDomain(fromEmail);
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

            // Get contact details from activity.contact
            var contact = await FsmApiService.GetContactAsync(cloudHost, company, activity.Contact);
            if (contact != null)
            {
                var toDomain = Utils.ExtractEmailDomain(contact.EmailAddress);
                var isMemberOfDomain = string.Equals(fromDomain, toDomain, StringComparison.InvariantCultureIgnoreCase);

                contacts.Add(new Contact
                {
                    Name = $"{contact.FirstName} {contact.LastName}",
                    Title = isMemberOfDomain ? contact.PositionName : "not in domain",
                    Role = ContactRole.Expert,
                    Connection = isMemberOfDomain ? Url.Action(
                        nameof(GetConnection),
                        "Connections",
                        new
                        {
                            from = fromEmail,
                            to = contact.EmailAddress,
                            meta = $"eqp:{activity.EquipmentId};act:{activityId}"
                        }
                    ) : null
                });
            }

            // Get responsible's details from activity.responsibles[]
            var responsibles = await FsmApiService.GetPersonsAsync(cloudHost, company, activity.Responsibles);
            contacts.AddRange(responsibles.Select(r => new Contact
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
                            to = contact.EmailAddress,
                            meta = $"eqp:{activity.EquipmentId};act:{activityId}"
                        }
                    )
                })
            );

            return Ok(contacts);
        }
    }
}
