using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nexus.Web.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : Controller
{
    [Route("Error")]
    public IActionResult Index()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error is not null)
            HttpContext.RequestServices.GetRequiredService<ILogger<ErrorController>>()
                .LogError(feature.Error, "Unhandled request error for {Path}", feature.Path);

        return View("Error");
    }
}
