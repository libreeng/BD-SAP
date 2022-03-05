using FSMExtension.Repositories;
using FSMExtension.Repositories.Cosmos;
using FSMExtension.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;

namespace FSMExtension
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddControllers();
            services.AddMvc()
                .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddCookie(options => options.SlidingExpiration = true)
                .AddJwtBearer(options =>
                {
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Security:Tokens:Issuer"],
                        ValidAudience = Configuration["Security:Tokens:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(Configuration["Security:Tokens:Key"]))
                    };
                });

            services.AddHttpContextAccessor();

            services.AddSingleton<HttpClient>();
            var mappingsContainerName = Configuration.GetValue<string>("CosmosDB:MappingsContainer");
            var mappingsContainer = OpenContainer(mappingsContainerName);
            var domainMappingRepo = new CosmosDomainMappingRepository(mappingsContainer);
            services.AddSingleton<IDomainMappingRepository>(domainMappingRepo);

            services.AddSingleton<IFsmApiService, FsmApiService>();
            services.AddSingleton<IOpenIdService, OpenIdService>();
            services.AddSingleton<IOnsightConnectService, OnsightConnectService>();
            services.AddSingleton<IOnsightWorkspaceService, OnsightWorkspaceService>();
            services.AddSingleton<IOnsightFlowService, OnsightFlowService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAuthentication();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });
        }

        private Container OpenContainer(string containerName)
        {
            var cosmosEndpoint = Configuration.GetValue<string>("CosmosDB:Endpoint");
            var cosmosKey = Configuration.GetValue<string>("CosmosDB:Key");
            var dbName = Configuration.GetValue<string>("CosmosDB:Database");
            var cosmosClient = new CosmosClient(cosmosEndpoint, cosmosKey);

            return cosmosClient.GetContainer(dbName, containerName);
        }
    }
}
