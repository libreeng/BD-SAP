using FSMExtension.Models;
using System.Threading.Tasks;

namespace FSMExtension.Repositories
{
    /// <summary>
    /// Defines access to DomainMapping objects stored in a database.
    /// </summary>
    public interface IDomainMappingRepository
    {
        Task<DomainMapping> GetFromDomainAsync(string onsightDomain);
        Task<DomainMapping> GetFromFsmAccountIdAsync(string fsmAccountId);
    }
}
