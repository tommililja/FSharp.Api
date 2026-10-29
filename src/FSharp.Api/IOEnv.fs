namespace FSharp.Api

open System
open System.Threading.Tasks
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

type ILogging =
    abstract GetLogger: string -> ILogger

module Logger =

    let get name =
        IO.create (fun (env:#ILogging) ->
            env.GetLogger(name)
        )

type ISqlConnection =
    abstract GetSqlConnection: unit -> Task<SqlConnection>

module SqlConnection =

    let get () =
        IO.createFromTask (fun (env:#ISqlConnection) ->
            env.GetSqlConnection()
        )

type IGuid =
    abstract CreateGuid: unit -> Guid

module Guid =

    let create () =
        IO.create (fun (env:#IGuid) ->
            env.CreateGuid()
        )

type IIOEnv =
    inherit IGuid
    inherit ISqlConnection
    inherit ILogging

type IOEnv(logging:ILoggerFactory, connectionString) =

    let sqlConnection =
        lazy (
            task {
                let conn = new SqlConnection(connectionString)
                do! conn.OpenAsync()
                return conn
            }
        )

    let opened () =
        if sqlConnection.IsValueCreated && sqlConnection.Value.IsCompletedSuccessfully
        then Some sqlConnection.Value.Result
        else None

    interface IIOEnv with
        member _.GetSqlConnection() = sqlConnection.Force()
        member _.CreateGuid() = Guid.NewGuid()
        member _.GetLogger(name) = logging.CreateLogger(name)

    interface IDisposable with
        member _.Dispose() =
            opened ()
            |> Option.iter _.Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            match opened () with
            | Some c -> c.DisposeAsync()
            | None -> ValueTask.CompletedTask