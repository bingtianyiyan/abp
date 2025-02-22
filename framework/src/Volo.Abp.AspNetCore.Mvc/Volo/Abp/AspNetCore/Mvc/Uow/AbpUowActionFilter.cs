using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Volo.Abp.AspNetCore.Filters;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Volo.Abp.AspNetCore.Mvc.Uow;

public class AbpUowActionFilter : IAsyncActionFilter, IAbpFilter, ITransientDependency
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ActionDescriptor.IsControllerAction())
        {
            await next();
            return;
        }

        var methodInfo = context.ActionDescriptor.GetMethodInfo();
        var unitOfWorkAttr = UnitOfWorkHelper.GetUnitOfWorkAttributeOrNull(methodInfo);

        context.HttpContext.Items["_AbpActionInfo"] = new AbpActionInfoInHttpContext
        {
            IsObjectResult = context.ActionDescriptor.HasObjectResult()
        };

        if (unitOfWorkAttr?.IsDisabled == true)
        {
            await next();
            return;
        }

        var options = CreateOptions(context, unitOfWorkAttr);

        var unitOfWorkManager = context.GetRequiredService<IUnitOfWorkManager>();
        var cancellationTokenProvider = context.GetRequiredService<ICancellationTokenProvider>();

        //Trying to begin a reserved UOW by AbpUnitOfWorkMiddleware
        if (unitOfWorkManager.TryBeginReserved(UnitOfWork.UnitOfWorkReservationName, options))
        {
            var result = await next();
            if (Succeed(result))
            {
                await SaveChangesAsync(context, unitOfWorkManager, cancellationTokenProvider.Token);
            }
            else
            {
                await RollbackAsync(context, unitOfWorkManager, cancellationTokenProvider.Token);
            }

            return;
        }

        using (var uow = unitOfWorkManager.Begin(options))
        {
            var result = await next();
            if (Succeed(result))
            {
                await uow.CompleteAsync(cancellationTokenProvider.Token);
            }
            else
            {
                await uow.RollbackAsync(cancellationTokenProvider.Token);
            }
        }
    }

    private AbpUnitOfWorkOptions CreateOptions(ActionExecutingContext context, UnitOfWorkAttribute? unitOfWorkAttribute)
    {
        var options = new AbpUnitOfWorkOptions();

        unitOfWorkAttribute?.SetOptions(options);

        if (unitOfWorkAttribute?.IsTransactional == null)
        {
            var abpUnitOfWorkDefaultOptions = context.GetRequiredService<IOptions<AbpUnitOfWorkDefaultOptions>>().Value;
            options.IsTransactional = abpUnitOfWorkDefaultOptions.CalculateIsTransactional(
                autoValue: !string.Equals(context.HttpContext.Request.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)
            );
        }

        return options;
    }

    private async Task RollbackAsync(ActionExecutingContext context, IUnitOfWorkManager unitOfWorkManager, CancellationToken cancellationToken)
    {
        var currentUow = unitOfWorkManager.Current;
        if (currentUow != null)
        {
            await currentUow.RollbackAsync(cancellationToken);
        }
    }

    private async Task SaveChangesAsync(ActionExecutingContext context, IUnitOfWorkManager unitOfWorkManager, CancellationToken cancellationToken)
    {
        var currentUow = unitOfWorkManager.Current;
        if (currentUow != null)
        {
            try
            {
                await currentUow.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                await currentUow.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static bool Succeed(ActionExecutedContext result)
    {
        return result.Exception == null || result.ExceptionHandled;
    }
}
