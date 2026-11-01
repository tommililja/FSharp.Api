namespace FSharp.Api

open System
open System.Threading.Tasks
open FSharp.Api.Core
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

type IGuid =
    abstract CreateGuid: unit -> Guid

module Guid =

    let create () =
        IO.create (fun (env:#IGuid) ->
            env.CreateGuid()
        )

type ISqlConnection =
    abstract GetSqlConnection: unit -> Task<SqlConnection>

module SqlConnection =

    let get () =
        IO.createFromTask (fun (env:#ISqlConnection) ->
            env.GetSqlConnection()
        )

type ILogging =
    abstract GetLogger: string -> ILogger

module Logger =

    let get name =
        IO.create (fun (env:#ILogging) ->
            env.GetLogger(name)
        )

type IAppEnv =
    inherit IGuid
    inherit ISqlConnection
    inherit ILogging