using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RotiseriaAPI.Services;

namespace RotiseriaAPI.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    public string FeatureCode { get; }

    public RequireFeatureAttribute(string featureCode)
    {
        FeatureCode = featureCode;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var accessService = context.HttpContext.RequestServices.GetRequiredService<SubscriptionAccessService>();
        try
        {
            await accessService.EnsureFeatureAsync(FeatureCode);
            await next();
        }
        catch (FeatureLockedException ex)
        {
            context.Result = new ObjectResult(new
            {
                error = "FEATURE_LOCKED",
                feature = ex.FeatureCode,
                message = ex.Message,
                upgradeRequired = true
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
        catch (SubscriptionBlockedException ex)
        {
            context.Result = new ObjectResult(new
            {
                error = "SUBSCRIPTION_BLOCKED",
                message = ex.Message,
                regularizeRequired = true
            })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
        }
        catch (PlanLimitException ex)
        {
            context.Result = new ObjectResult(new
            {
                error = "PLAN_LIMIT_REACHED",
                message = ex.Message,
                limitReached = true
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
