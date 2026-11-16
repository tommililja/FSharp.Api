namespace FSharp.Api

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open NodaTime
open Orsak

[<NoComparison>]
type Integrations = {
    ExampleService: Uri
}

[<NoComparison>]
type AppSettings = {
    ConnectionString: string
    Integrations: Integrations
}

type IAppSettings =
    abstract GetAppSettings: unit -> AppSettings

module AppSettings =

    let load (config:IConfiguration) =
        {
            ConnectionString = config.GetConnectionString("DefaultConnection")
            Integrations = {
                ExampleService = Uri(config["Integrations:ExampleService"])
            }
        }

    let get () =
        Effect.Create(fun (env:#IAppSettings) ->
            env.GetAppSettings()
        )

type IGuid =
    abstract CreateGuid: unit -> Guid

type IInstant =
    abstract GetCurrentTime: unit -> Instant

type ISqlConnection =
    abstract GetSqlConnection: unit -> Task<SqlConnection>

type ICancellationToken =
    abstract GetCancellationToken: unit -> CancellationToken

type ILogging =
    abstract GetLogger: string -> ILogger

type IHttpClient =
    abstract GetHttpClient: unit -> HttpClient

type IAppEnv =
    inherit IGuid
    inherit IInstant
    inherit ILogging
    inherit ISqlConnection
    inherit ICancellationToken
    inherit IHttpClient
    inherit IAppSettings