namespace FSharp.Api

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Oxpecker

module App =

    let builder = WebApplication.CreateBuilder()

    let connectionString =
        builder.Configuration
            .GetConnectionString("DefaultConnection")

    !builder.Services.AddHttpContextAccessor()
    !builder.Services
        .AddScoped<IAppEnv>(fun x ->
            let factory = x.GetRequiredService<ILoggerFactory>()
            let cancellationToken =
                x.GetRequiredService<IHttpContextAccessor>()
                    .HttpContext.RequestAborted

            new AppEnv(factory, cancellationToken, connectionString)
        )

    let app = builder.Build()

    !app
        .UseRouting()
        .Use(Default.exceptionMiddleware)
        .UseOxpecker(Endpoints.list)
        .Run(Default.notFoundHandler)

    app.Run()