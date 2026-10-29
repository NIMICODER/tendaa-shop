using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Implementations;
using Tendaa.Infrastructure.Data.Interfaces;
using Tendaa.Infrastructure.WhatsApp;

namespace Tendaa.Application.Extensions;

/// <summary>DI registration, one extension method per concern rather than one per project layer.
/// Called explicitly, in sequence, from Tendaa.Web's Program.cs.</summary>
public static class ServiceCollections
{
    public static IServiceCollection BindOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MetaModelOptions>()
            .Bind(configuration.GetSection(MetaModelOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<DashboardOptions>()
            .Bind(configuration.GetSection(DashboardOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentShopAccessor, CurrentShopAccessor>();
        services.AddDbContext<TendaaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHttpClient<IWhatsAppSender, WhatsAppGraphApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
            client.BaseAddress = new Uri("https://graph.facebook.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        });

        return services;
    }

    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IModelPingService, ModelPingService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IHandoffService, HandoffService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IInboundEventService, InboundEventService>();
        services.AddSingleton<InboundEventChannel>();

        return services;
    }

    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    public static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("Application", "Tendaa")
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console()
            .WriteTo.File("logs/tendaa-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
