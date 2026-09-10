using System.Reflection;
using Helpdesk.Application.Messaging;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helpdesk.API.DependencyInjection;

public static class RequestBusServiceCollectionExtensions
{
    public static IServiceCollection AddRequestBus(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var handlerRegistrations = assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false } && !type.ContainsGenericParameters)
            .SelectMany(type => type.ImplementedInterfaces
                .Where(@interface => @interface.IsGenericType &&
                                     @interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(@interface => new
                {
                    ServiceType = @interface,
                    ImplementationType = type.AsType()
                }))
            .ToList();

        var duplicateRegistrations = handlerRegistrations
            .GroupBy(registration => registration.ServiceType)
            .Where(group => group.Select(x => x.ImplementationType).Distinct().Skip(1).Any())
            .ToList();

        if (duplicateRegistrations.Count != 0)
        {
            var details = string.Join("; ", duplicateRegistrations.Select(group =>
                $"{group.Key} => {string.Join(", ", group.Select(x => x.ImplementationType.FullName).Distinct())}"));
            throw new InvalidOperationException($"Duplicate request handlers detected: {details}");
        }

        foreach (var registration in handlerRegistrations)
        {
            services.TryAddTransient(registration.ServiceType, registration.ImplementationType);
        }

        services.TryAddScoped<IRequestSender, RequestSender>();
        return services;
    }
}
