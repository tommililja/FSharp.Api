namespace FSharp.Api.Tests

open System
open System.Net.Http
open System.Text
open System.Threading
open FSharp.Api
open Meziantou.Extensions.Logging.Xunit.v3
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open RichardSzalay.MockHttp
open Testcontainers.MsSql
open Xunit

type TestApp(factory:WebApplicationFactory<IAppEnv>, connectionString:string, client:HttpClient, httpHandlers:MockHttpMessageHandler) =
    member _.HttpHandlers = httpHandlers

    // Api

    member _.GetUser(userId) =
        client.GetAsync($"/users/%s{Guid.asString userId}")

    member _.GetUsers() =
        client.GetAsync("/users")

    member _.RegisterUser(json) =
        client.PostAsync("/users", Json.asStringContent json)

    member _.RegisterUser(body) =
        client.PostAsync("/users", new StringContent(body, Encoding.UTF8, "application/json"))

    member _.UnregisterUser(userId) =
        client.DeleteAsync($"/users/%s{Guid.asString userId}")

    // Test data

    member _.InsertUser(user) =
        connectionString
        |> TestData.insertUser user

    member _.InsertUsers(n) =
        connectionString
        |> TestData.insertUsers n

    interface IDisposable with
        member _.Dispose() =
            try httpHandlers.VerifyNoOutstandingExpectation()
            finally
                client.Dispose()
                factory.Dispose()

module TestApp =

    let private container =
        MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithReuse(true)
            .WithLabel("reuse-id", "fsharp-api-tests")
            .Build()

    let private start =
        lazy (
            task {
                do! container.StartAsync()
                let connectionString = container.GetConnectionString()
                do! TestData.createTables connectionString
                return connectionString
            }
        )

    let create (guidGen:GuidGen) =
        task {
            let! connectionString = start.Value

            do! TestData.resetTables connectionString

            let output = TestContext.Current.TestOutputHelper

            let provider =
                new XUnitLoggerProvider(output,
                    XUnitLoggerOptions(
                        IncludeLogLevel = true,
                        IncludeCategory = true,
                        TimestampFormat = "HH:mm:ss.fff"
                    )
                )

            let configureLogging (builder:ILoggingBuilder) =
                !builder
                    .AddProvider(provider)
                    .AddFilter("Microsoft.AspNetCore", LogLevel.Information)

            let httpHandlers = new MockHttpMessageHandler()

            let configureTestServices (services:IServiceCollection) =
                let configureProblemDetails (options:ProblemDetailsOptions) =
                    let existing = options.CustomizeProblemDetails
                    options.CustomizeProblemDetails <- (fun context ->
                        if not (isNull existing) then existing.Invoke(context)
                        context.ProblemDetails.Extensions["traceId"] <- box "test-trace-id"
                    )

                let configureEnv (x:IServiceProvider) =
                    let newGuid = GuidGen.asFn guidGen
                    let loggerFactory = x.GetRequiredService<ILoggerFactory>()
                    let httpClientFactory = x.GetRequiredService<IHttpClientFactory>()

                    let cancellationToken =
                        x.GetRequiredService<IHttpContextAccessor>()
                            .HttpContext.RequestAborted

                    let appSettings =
                        x.GetRequiredService<IConfiguration>()
                        |> AppSettings.load

                    AppEnv.create newGuid loggerFactory httpClientFactory cancellationToken appSettings

                !httpHandlers.Fallback
                    .Throw(InvalidOperationException("No matching mock handler found."))

                !services
                    .AddSingleton<MockHttpMessageHandler>(httpHandlers)
                    .ConfigureHttpClientDefaults(fun http ->
                        !http
                            .ConfigurePrimaryHttpMessageHandler<MockHttpMessageHandler>()
                            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
                    )

                !services
                    .PostConfigure<ProblemDetailsOptions>(configureProblemDetails)
                    .AddScoped<IAppEnv>(configureEnv)

            let configureAppConfiguration (config:IConfigurationBuilder) =
                !config.AddInMemoryCollection(
                    dict [
                        "ConnectionStrings:DefaultConnection", connectionString
                        "Integrations:ExampleService", "http://localhost"
                    ]
                )

            let factory =
                {
                    new WebApplicationFactory<IAppEnv>() with
                        member _.ConfigureWebHost(builder) =
                            !builder
                                .ConfigureAppConfiguration(configureAppConfiguration)
                                .ConfigureLogging(configureLogging)
                                .ConfigureTestServices(configureTestServices)
                }

            let client = factory.CreateDefaultClient(new OpenApiValidationHandler(Docs.document))

            return new TestApp(factory, connectionString, client, httpHandlers)
        }