using FSMExtension.Models;
using System.Threading.Tasks;

namespace FSMExtension.Repositories
{
    /// <summary>
    /// Defines access to DomainMapping objects stored in a database.
    /// </summary>
    public interface IDomainMappingRepository
    {
        /// <summary>
        /// Look up a DomainMapping based on a user's email address. This email
        /// address must correspond to an Onsight domain and a user registered
        /// within the domain_mapping table.
        /// 
        /// TODO: this needed by the Mobile extension only. Can we get rid of this
        /// (and DomainMapping.EmailUsers) by passing in the FSM account ID from
        /// the mobile integration?
        /// </summary>
        /// <param name="userEmail">Email address of the user for which an Onsight/FSM mapping is requested.</param>
        /// <returns></returns>
        Task<DomainMapping> GetFromUserEmailAsync(string userEmail);

        /// <summary>
        /// Look up a DomainMapping based on a FSM account ID. Note that this assumes
        /// there is a 1:1 mapping from FSM account ID to an Onsight account.
        /// </summary>
        /// <param name="fsmAccountId"></param>
        /// <returns></returns>
        Task<DomainMapping> GetFromFsmAccountIdAsync(string fsmAccountId);
    }
}
