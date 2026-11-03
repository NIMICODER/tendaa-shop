using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Tendaa.Web.Filters;

/// <summary>Adds antiforgery validation to API controllers only (routes starting with
/// <c>api/</c>). The WhatsApp webhook also takes POSTs but can never send an antiforgery token
/// — Meta is the caller — so a truly global filter would 400 every inbound message.</summary>
public sealed class ApiAntiforgeryConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var isApi = controller.Selectors.Any(static selector =>
            selector.AttributeRouteModel?.Template?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);

        if (isApi)
        {
            controller.Filters.Add(new ApiAntiforgeryFilter());
        }
    }
}

/// <summary>Hand-rolled equivalent of [AutoValidateAntiforgeryToken]: unsafe-verb requests must
/// carry a valid antiforgery token (400 otherwise), safe verbs pass through. The built-in
/// attribute can't be used here — its filter type is internal to the ViewFeatures assembly and
/// only registered by AddControllersWithViews/AddMvc, while this API-only app uses
/// AddControllers() (using it 500s every /api request with "No service for type
/// 'AutoValidateAntiforgeryTokenAuthorizationFilter' has been registered.").</summary>
public sealed class ApiAntiforgeryFilter : IFilterFactory, IOrderedFilter
{
    public bool IsReusable => true;

    public int Order => 1000;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new InnerFilter(serviceProvider.GetRequiredService<IAntiforgery>());

    private sealed class InnerFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var method = context.HttpContext.Request.Method;
            if (HttpMethods.IsGet(method)
                || HttpMethods.IsHead(method)
                || HttpMethods.IsOptions(method)
                || HttpMethods.IsTrace(method))
            {
                return;
            }

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestResult();
            }
        }
    }
}
