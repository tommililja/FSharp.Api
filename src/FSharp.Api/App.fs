namespace FSharp.Api

open System
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Oxpecker

module App =

    let builder = WebApplication.CreateBuilder()

    let appSettings = AppSettings.load builder.Configuration

    !builder.Services
        .AddHttpClient()
        .AddHttpContextAccessor()
        .AddProblemDetails()
        .AddScoped<IAppEnv>(fun x ->
            let newGuid = Guid.NewGuid
            let loggerFactory = x.GetRequiredService<ILoggerFactory>()
            let httpClientFactory = x.GetRequiredService<IHttpClientFactory>()
            let cancellationToken =
                x.GetRequiredService<IHttpContextAccessor>()
                    .HttpContext.RequestAborted

            AppEnv.create newGuid loggerFactory httpClientFactory cancellationToken appSettings
        )

    let app = builder.Build()

    !app
        .UseExceptionHandler()
        .UseRouting()
        .UseOxpecker(Endpoints.list)
        .Run(ApiError.notFound)

    app.Run()