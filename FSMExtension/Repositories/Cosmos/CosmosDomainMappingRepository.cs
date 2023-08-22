using FSMExtension.Models;
using Microsoft.Azure.Cosmos;
using System.Linq;
using System.Threading.Tasks;

namespace FSMExtension.Repositories.Cosmos
{
    /// <summary>
    /// Implementation of the IDomainMappingRepository interface which stores data in Azure Cosmos DB.
    /// This is provided as a class in a separate namespace in the event that other implementers 
    /// wish to use a different storage service.
    /// </summary>
    public class CosmosDomainMappingRepository : IDomainMappingRepository
    {
        public CosmosDomainMappingRepository(Container domainMappings)
        {
            DomainMappings = domainMappings;
        }

        private Container DomainMappings { get; }

        public async Task<DomainMapping> GetFromUserEmailAsync(string userEmail)
        {
            if (string.IsNullOrEmpty(userEmail))
                return null;

            if (!Utils.ExtractEmailParts(userEmail, out var userName, out var emailDomain))
                return null;

            DomainMapping mapping = null;
            var queryDef = new QueryDefinition("SELECT * FROM domain_mapping dm WHERE (dm.emailDomain = @emailDomain OR dm.onsightDomain = @emailDomain) AND ARRAY_CONTAINS(dm.emailUsers, @userName)")
                .WithParameter("@emailDomain", emailDomain)
                .WithParameter("@userName", userName);

            using var iter = DomainMappings.GetItemQueryIterator<DomainMapping>(queryDef);
            if (iter.HasMoreResults)
            {
                var resultSet = await iter.ReadNextAsync();
                mapping = resultSet?.FirstOrDefault();
            }

            ConnectChildrenToParent(mapping);
            return mapping;
        }

        // TODO: this doesn't perform checking to ensure that a specific user is registered
        // to use this extension. Instead, if they simply have a matching FSM account ID, they
        // will have access.
        public async Task<DomainMapping> GetFromFsmAccountIdAsync(string fsmAccountId)
        {
            DomainMapping mapping = null;

            var queryDef = new QueryDefinition("SELECT * FROM domain_mapping dm WHERE dm.sap_fsm.accountId = @accountId")
                .WithParameter("@accountId", fsmAccountId);

            using var iter = DomainMappings.GetItemQueryIterator<DomainMapping>(queryDef);
            if (iter.HasMoreResults)
            {
                var resultSet = await iter.ReadNextAsync();
                mapping = resultSet?.FirstOrDefault();
            }

            ConnectChildrenToParent(mapping);
            return mapping;
        }

        /// <summary>
        /// When reading a DomainMapping object from the database, the various child elements don't automatically
        /// have a reference to their parent. We have to do that ourselves.
        /// </summary>
        /// <param name="domain"></param>
        private void ConnectChildrenToParent(DomainMapping domain)
        {
            if (domain == null)
                return;

            domain.FsmAccount.DomainMapping = domain;
            foreach (var company in domain.FsmAccount.Companies)
            {
                company.Account = domain.FsmAccount;
            }
        }
    }
}
