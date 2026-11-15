namespace FSharp.Api.Tests

open System
open System.IO
open System.Net.Http
open System.Text
open System.Threading
open FSharp.Api
open Meziantou.Extensions.Logging.Xunit.v3
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpLogging
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open RichardSzalay.MockHttp
open Testcontainers.MsSql
open Xunit

type private HttpLoggingFilter() =
    interface IStartupFilter with
        member _.Configure(next) =
            Action<IApplicationBuilder>(fun app ->
                !app.UseHttpLogging()
                next.Invoke(app)
            )

type TestApp(factory:WebApplicationFactory<IAppEnv>, connectionString:string, client:HttpClient, httpHandlers:MockHttpMessageHandler) =
    member _.Client = client
    member _.ConnectionString = connectionString
    member _.HttpHandlers = httpHandlers

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
                do! TestData.execute connectionString (File.ReadAllText "Scripts/CreateTables.sql")
                return connectionString
            }
        )

    let private resetSql = File.ReadAllText "Scripts/ResetTables.sql"

    let create (guidGen:GuidGen) =
        task {
            let! connectionString = start.Value

            do! TestData.execute connectionString resetSql

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
                let addHttpLogging (options:HttpLoggingOptions) =
                    options.LoggingFields <-
                        HttpLoggingFields.RequestPropertiesAndHeaders
                        ||| HttpLoggingFields.RequestBody
                        ||| HttpLoggingFields.ResponseBody
                        ||| HttpLoggingFields.ResponsePropertiesAndHeaders
                    options.CombineLogs <- true

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

                    let appSettings = {
                        ConnectionString = connectionString
                        Integrations = {
                            ExampleService = Uri("http://localhost")
                        }
                    }

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
                    .AddHttpLogging(addHttpLogging)
                    .AddTransient<IStartupFilter, HttpLoggingFilter>()
                    .PostConfigure<ProblemDetailsOptions>(configureProblemDetails)
                    .AddScoped<IAppEnv>(configureEnv)

            let factory =
                {
                    new WebApplicationFactory<IAppEnv>() with
                        member _.ConfigureWebHost(builder) =
                            !builder
                                .ConfigureLogging(configureLogging)
                                .ConfigureTestServices(configureTestServices)
                }

            let client = factory.CreateDefaultClient(new OpenApiValidationHandler(Docs.document))

            return new TestApp(factory, connectionString, client, httpHandlers)
        }