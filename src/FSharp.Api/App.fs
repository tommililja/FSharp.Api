namespace FSharp.Api

open System
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

    !builder.Services
        .AddHttpContextAccessor()
        .AddProblemDetails()
        .AddScoped<IAppEnv>(fun x ->
            let newGuid = Guid.NewGuid
            let loggerFactory = x.GetRequiredService<ILoggerFactory>()
            let cancellationToken =
                x.GetRequiredService<IHttpContextAccessor>()
                    .HttpContext.RequestAborted

            AppEnv.create newGuid loggerFactory cancellationToken connectionString
        )

    let app = builder.Build()

    !app
        .UseExceptionHandler()
        .UseRouting()
        .UseOxpecker(Endpoints.list)
        .Run(EndpointHandlers.problem 404 "Resource not found.")

    app.Run()