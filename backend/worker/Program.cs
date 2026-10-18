using Api.Hosting;
using Api.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;

// The worker is foundation's shared host plus this edition's data: the single-tenant
// connection factory. The jobs (email outbox, user lifecycle, announcements) live in
// OrkyoWorkerHost.BuildJobs, where a test pins that every declared job is registered — a
// hand-copied list here is how the outbox drain went unregistered in every release up to 1.6.7.
return await OrkyoWorkerHost.RunAsync(
    "Orkyo.Community.Worker",
    args,
    configureEditionServices: (context, services) =>
        services.AddSingleton<IDbConnectionFactory>(
            _ => SingleTenantDbConnectionFactory.FromConfiguration(context.Configuration)),
    extraJobs: _ => []);
