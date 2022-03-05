using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FSMExtension.Models
{
    /// <summary>
    /// Represents an entry in our domain_mapping table, which maps an Onsight Domain
    /// account to a SAP FSM account.
    /// </summary>
    public class DomainMapping
    {
        [JsonProperty("id")]
        internal string Id { get; set; }

        [JsonProperty("onsightDomain")]
        public string OnsightDomain { get; set; }

        [JsonProperty("teamName")]
        public string TeamName { get; set; }

        /// <summary>
        /// The email user names of all registered users of the extension.
        /// This assumes that the OnsightDomain is the same as each user's
        /// email domain.
        /// </summary>
        [JsonProperty("emailUsers")]
        public IEnumerable<string> EmailUsers { get; set; }

        [JsonProperty("onsightApiKey")]
        public string OnsightApiKey { get; set; }

        [JsonProperty("sap_fsm")]
        public MappedAccountInfo FsmAccount { get; set; }            

        public override string ToString()
        {
            return $"{OnsightDomain} <-> FSM Account '{FsmAccount.Name}'";
        }
    }

    public class MappedAccountInfo
    {
        private WeakReference<DomainMapping> _domainMapping;


        [JsonProperty("accountId")]
        public string Id { get; set; }

        [JsonProperty("accountName")]
        public string Name { get; set; }

        [JsonProperty("installs")]
        public IEnumerable<InstallInfo> Installs { get; set; }

        [JsonProperty("companies")]
        public IEnumerable<CompanyInfo> Companies { get; set; }
        
        [JsonProperty("customization")]
        public Customization Customization { get; set; }

        [JsonIgnore]
        public DomainMapping DomainMapping
        {
            get
            {
                _domainMapping.TryGetTarget(out var act);
                return act;
            }
            set
            {
                _domainMapping = new WeakReference<DomainMapping>(value);
            }
        }

        internal InstallInfo FindInstall(string cloudHost)
        {
            return Installs.FirstOrDefault(i => string.Equals(i.CloudHost, cloudHost, StringComparison.InvariantCultureIgnoreCase));
        }

        internal CompanyInfo FindCompany(string companyId)
        {
            return Companies.FirstOrDefault(c => string.Equals(c.Id, companyId, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    public class InstallInfo
    {
        [JsonProperty("cloudHost")]
        public string CloudHost { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("clientSecret")]
        public string ClientSecret { get; set; }

        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; }
    }

    public class CompanyInfo
    {
        private WeakReference<MappedAccountInfo> _account;


        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public MappedAccountInfo Account
        {
            get
            {
                _account.TryGetTarget(out var act);
                return act;
            }
            set
            {
                _account = new WeakReference<MappedAccountInfo>(value);
            }
        }

        [JsonProperty("identityProvider")]
        public IdentityProvider IdentityProvider { get; set; }

        public override string ToString()
        {
            return $"FSM Company {Id}";
        }
    }
}
