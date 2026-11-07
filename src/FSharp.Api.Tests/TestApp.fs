namespace FSharp.Api.Tests

open System
open System.IO
open System.Net.Http
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
open Testcontainers.MsSql
open Xunit

type private HttpLoggingFilter() =
    interface IStartupFilter with
        member _.Configure(next) =
            Action<IApplicationBuilder>(fun app ->
                !app.UseHttpLogging()
                next.Invoke(app)
            )

type TestApp(factory:WebApplicationFactory<IAppEnv>, connectionString:string, client:HttpClient) =
    interface IDisposable with
        member _.Dispose() =
            client.Dispose()
            factory.Dispose()

    member _.Client = client
    member _.ConnectionString = connectionString

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

            let webAppFactory =
                {
                    new WebApplicationFactory<IAppEnv>() with
                        member _.ConfigureWebHost(builder) =
                            !builder
                                .ConfigureLogging(fun x ->
                                    !x.AddProvider(provider)
                                    !x.AddFilter("Microsoft.AspNetCore", LogLevel.Information)
                                )
                                .ConfigureTestServices(fun services ->
                                    !services
                                        .AddHttpLogging(fun options ->
                                            options.LoggingFields <-
                                                HttpLoggingFields.RequestPropertiesAndHeaders
                                                ||| HttpLoggingFields.RequestBody
                                                ||| HttpLoggingFields.ResponseBody
                                                ||| HttpLoggingFields.ResponsePropertiesAndHeaders
                                            options.CombineLogs <- true
                                        )
                                        .AddTransient<IStartupFilter, HttpLoggingFilter>()
                                        .PostConfigure<ProblemDetailsOptions>(fun options ->
                                            let existing = options.CustomizeProblemDetails
                                            options.CustomizeProblemDetails <- (fun context ->
                                                if not (isNull existing) then existing.Invoke(context)
                                                context.ProblemDetails.Extensions["traceId"] <- box "test-trace-id"
                                            )
                                        )
                                        .AddScoped<IAppEnv>(fun x ->
                                            let newGuid = GuidGen.asFn guidGen
                                            let loggerFactory = x.GetRequiredService<ILoggerFactory>()
                                            let cancellationToken =
                                                x.GetRequiredService<IHttpContextAccessor>()
                                                    .HttpContext.RequestAborted

                                            AppEnv.create newGuid loggerFactory cancellationToken connectionString
                                        )
                                )
                }

            let client = webAppFactory.CreateDefaultClient(new OpenApiValidationHandler(Docs.document))

            return new TestApp(webAppFactory, connectionString, client)
        }