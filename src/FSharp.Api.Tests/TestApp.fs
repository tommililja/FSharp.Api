namespace FSharp.Api.Tests

open System
open System.IO
open System.Net.Http
open FSharp.Api
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Testcontainers.MsSql
open Xunit

type TestApp(factory:WebApplicationFactory<IOEnv>, connectionString:string, client:HttpClient) =
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

    let create id =
        task {
            let! connectionString = start.Value

            do! TestData.execute connectionString resetSql

            let output = TestContext.Current.TestOutputHelper

            let factory =
                {
                    new WebApplicationFactory<IOEnv>() with
                        member _.ConfigureWebHost(builder) =
                            !builder.ConfigureTestServices(fun services ->
                                !services.AddScoped<IIOEnv>(fun _ ->
                                    TestEnv(id, connectionString, output) :> IIOEnv)
                            )
                }

            let client = factory.CreateDefaultClient(new OpenApiValidationHandler(Docs.document))

            return new TestApp(factory, connectionString, client)
        }