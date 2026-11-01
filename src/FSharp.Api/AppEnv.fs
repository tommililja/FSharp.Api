namespace FSharp.Api

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

type AppEnv(logging:ILoggerFactory, httpContext:HttpContext, connectionString) =

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

    interface IAppEnv with
        member _.GetSqlConnection() = sqlConnection.Force()
        member _.CreateGuid() = Guid.NewGuid()
        member _.GetLogger(name) = logging.CreateLogger(name)
        member _.GetCancellationToken() = httpContext.RequestAborted

    interface IDisposable with
        member _.Dispose() =
            opened ()
            |> Option.iter _.Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            match opened () with
            | Some c -> c.DisposeAsync()
            | None -> ValueTask.CompletedTask