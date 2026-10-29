namespace FSharp.Api

open Microsoft.AspNetCore.Builder
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
        .AddScoped<IIOEnv>(fun x ->
            let logging = x.GetRequiredService<ILoggerFactory>()
            new IOEnv(logging, connectionString) :> IIOEnv
        )

    let app = builder.Build()

    !app
        .UseRouting()
        .Use(Default.exceptionMiddleware)
        .UseOxpecker(Endpoints.list)
        .Run(Default.notFoundHandler)

    app.Run()