using FSMExtension.Models;
using FSMExtension.Models.Fsm;
using FSMExtension.Repositories;
using FSMExtension.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FSMExtension.Controllers
{
    /// <summary>
    /// Manages authentication of clients to our Connections API.
    /// </summary>
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        public AuthController(
            IOpenIdService openIdService,
            IDomainMappingRepository accountRepo,
            IFsmApiService fsmApiService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            OpenIdService = openIdService;
            DomainMappingRepository = accountRepo;
            FsmApiService = fsmApiService;
            Configuration = configuration;
            Logger = logger;
        }

        private IOpenIdService OpenIdService { get; }

        private IDomainMappingRepository DomainMappingRepository { get; }

        private IFsmApiService FsmApiService { get; }

        private IConfiguration Configuration { get; }

        private ILogger<AuthController> Logger { get; }

        /// <summary>
        /// Generates an Identity Provider 'authorize' URL based on provided information.
        /// 
        /// If the customer has an OpenID Connect provider configured, this will return that
        /// provider's authorize URL.
        /// 
        /// If no OpenID Connect provider is configured, the alternative /login URL is returned instead.
        /// </summary>
        /// <param name="cloudHost">The FSM cloud host (e.g., "eu.corsesuite.com").</param>
        /// <param name="accountId">The FSM account number.</param>
        /// <param name="companyId">The FSM company number.</param>
        /// <param name="userId">The FSM user number.</param>
        /// <returns>A URL pointing to the account's defined identity provider.</returns>
        [AllowAnonymous]
        [HttpPost]
        [Produces("text/plain")]
        [Route("provider")]
        public async Task<IActionResult> Provider(
            [FromForm] string cloudHost,
            [FromForm] string accountId,
            [FromForm] string companyId,
            [FromForm] string userId)
        {
            var companyIdentifier = new FsmUserId(cloudHost, accountId, companyId, userId);

            var domainMapping = await DomainMappingRepository.GetFromFsmAccountIdAsync(accountId);
            var company = domainMapping.FsmAccount.FindCompany(companyId);
            if (company == null)
                return NotFound();

            var idp = company.IdentityProvider;
            var state = EncodeState(accountId, companyId, userId, cloudHost);

            string url;
            if (company.IdentityProvider == null)
            {
                // Customer does not have a 3rd-party OpenID Connect provider configured;
                // we will still verify that we can access FSM APIs on their behalf,
                // but will not perform additional identity verification.
                // Return a URL to our direct login URL.
                url = Url.Action(nameof(LoginWithoutOpenId), new { state });
            }
            else
            {
                // Customer has listed a 3rd-party OpenID Connect provider.
                // Return a URL to the provider's authorize/consent screen.
                var callbackUri = Url.Action(nameof(Callback), "Auth", null, "https");
                var nonce = CalculateNonce();
                url = Uri.EscapeUriString($"{idp.AuthorizeUrl}?client_id={idp.ClientId}&scope=openid email profile&response_type=code&redirect_uri={callbackUri}&state={state}&nonce={nonce}");
            }

            return Ok(url);
        }

        /// <summary>
        /// The redirect/callback URL which is invoked by an OpenID Connect identity provider during
        /// the authorization phase.
        /// </summary>
        /// <param name="code">The authorization code returned by the OpenID Connect provider.</param>
        /// <param name="state">State info pertaining to the account/company/user who initiated the authorize call.</param>
        /// <returns>A redirection to the index page along with the authorization token as a query parameter.</returns>
        [HttpGet]
        [Route("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest("Missing authentication code parameter");
            if (string.IsNullOrEmpty(state))
                return NotFound();

            // Extract account, company, and user from query parameter
            var userIdentifier = DecodeState(state);

            // Look up FSM Company in database
            var domainMapping = await DomainMappingRepository.GetFromFsmAccountIdAsync(userIdentifier.AccountId);
            var fsmCompany = domainMapping.FsmAccount.FindCompany(userIdentifier.CompanyId);
            if (fsmCompany == null)
                return BadRequest("FSM company not found in Librestream database");

            var idp = fsmCompany.IdentityProvider;
            if (idp == null)
                return BadRequest("No identity provider defined for FSM company");

            var redirectUri = Url.Action(nameof(Callback), "Auth", null, "https");
            var openIdEmail = await OpenIdService.GetEmailAsync(idp, code, redirectUri);
            if (string.IsNullOrEmpty(openIdEmail))
                return BadRequest("User info not supplied by identity provider");

            var accessInfo = await GetAuthInfoAsync(userIdentifier.CloudHost, fsmCompany, userIdentifier.UserId, openIdEmail);
            if (accessInfo == null)
                return BadRequest("Mismatched user credentials");

            // Pass token + fromEmail to Index page as query params (not ideal)
            return new RedirectToPageResult("/Index", new { from = accessInfo.Email, t = accessInfo.Token });
        }

        /// <summary>
        /// An alternative login URL which, if successful, returns an access token to the fsm/connections API.
        /// 
        /// This will be called if the customer does not have a 3rd-party OpenID Connect provider configured;
        /// even so, we will authenticate the user if all of the following are true:
        ///     a) they have an entry in our domain_mapping database table,
        ///     b) their FSM Account number is associated with this domain_mapping,
        ///     c) we can connect to the FSM APIs using the configured Client Credentials, and
        ///     d) we can successfully lookup the given userId using the FSM APIs.
        /// </summary>
        /// <param name="state">A string containing the FSM account+user information of the caller.</param>
        /// <returns></returns>
        [HttpGet]
        [Route("login/{state}")]
        public async Task<IActionResult> LoginWithoutOpenId([FromRoute] string state)
        {
            // Extract account, company, and user from query parameter
            var userIdentifier = DecodeState(state);
            if (userIdentifier == null)
                return NotFound();

            var domainMapping = await DomainMappingRepository.GetFromFsmAccountIdAsync(userIdentifier.AccountId);
            var fsmCompany = domainMapping.FsmAccount.FindCompany(userIdentifier.CompanyId);
            var fsmUser = await FsmApiService.GetUserAsync(userIdentifier.CloudHost, fsmCompany, userIdentifier.UserId);

            if (fsmUser == null)
                return BadRequest("Access to FSM data service not allowed: ensure Client ID and Client Secret have been configured with Onsight Extension.");

            var accessInfo = GenerateUserAccessInfo(fsmUser.Email);

            // Pass token + fromEmail to Index page as query params (not ideal)
            return new RedirectToPageResult("/Index", new { from = accessInfo.Email, t = accessInfo.Token });
        }

        private async Task<UserAccessInfo> GetAuthInfoAsync(string cloudHost, CompanyInfo fsmCompany, string userId, string userEmail)
        {
            var fsmUser = await FsmApiService.GetUserAsync(cloudHost, fsmCompany, userId);

            // Compare FSM user.email to the submitted userEmail and ensure they're the same before proceeding.
            if (string.Equals(fsmUser.Email, userEmail, StringComparison.InvariantCultureIgnoreCase))
                return GenerateUserAccessInfo(userEmail);

            return null;
        }

        /// <summary>
        /// Generates a JWT containing our Connections API's "token". This token must be 
        /// passed in all calls to /connections.
        /// </summary>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        private UserAccessInfo GenerateUserAccessInfo(string userEmail)
        {
            var claims = new[]
{
                    new Claim(JwtRegisteredClaimNames.Sub, userEmail),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Security:Tokens:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(Configuration["Security:Tokens:Issuer"],
                Configuration["Security:Tokens:Issuer"],
                claims,
                expires: DateTime.Now.AddMinutes(int.Parse(Configuration["Security:Tokens:Timeout"])),
                signingCredentials: creds);

            return new UserAccessInfo
            (
                userEmail,
                new JwtSecurityTokenHandler().WriteToken(token)
            );
        }

        /// <summary>
        /// Calculates a random value to pass to the OpenID Connect provider as part of authentication.
        /// Some providers are more restrict when it comes to requiring this parameter.
        /// </summary>
        /// <returns></returns>
        private static string CalculateNonce()
        {
            var bytes = new byte[20];

            // Generate a cryptographically random set of bytes
            using (var rnd = RandomNumberGenerator.Create())
            {
                rnd.GetBytes(bytes);
            }

            // Base64 encode and then return
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// This simply puts the FSM account info together into a Base64 string to be passed back to our authentication
        /// callback or login actions. Since those methods must use HTTP GET, we are forced to use query params
        /// to pass these values through the authentication chain. Encrypting these might make sense for those who
        /// are sensitive to passing plain-text account numbers as query parameters.
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="companyId"></param>
        /// <param name="userId"></param>
        /// <param name="cloudHost"></param>
        /// <returns></returns>
        private static string EncodeState(string accountId, string companyId, string userId, string cloudHost)
        {
            var text = $"{accountId}::{companyId}::{userId}::{cloudHost}";
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Decodes the given Base64 state query parameter.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private static FsmUserId DecodeState(string state)
        {
            var bytes = Convert.FromBase64String(state);
            var text = Encoding.UTF8.GetString(bytes);
            var parts = text.Split("::");
            if (parts.Length != 4)
                return null;

            return new FsmUserId(cloudHost: parts[3], accountId: parts[0], companyId: parts[1], userId: parts[2]);
        }
    }
}
