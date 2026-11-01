namespace FSharp.Api.Tests

open System
open System.Threading
open System.Threading.Tasks
open FSharp.Api
open FSharp.Control
open Meziantou.Extensions.Logging.Xunit.v3
open Microsoft.Data.SqlClient

type TestEnv(guidGen, connectionString, output) =

    let connection =
        lazy (
            task {
                let conn = new SqlConnection(connectionString)
                do! conn.OpenAsync()
                return conn
            }
        )

    interface IAppEnv with
        member _.GetSqlConnection() = connection.Force()
        member _.CreateGuid() = GuidGen.generate guidGen
        member _.GetLogger _ = XUnitLogger.CreateLogger(output)
        member _.GetCancellationToken() = CancellationToken.None

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            if connection.IsValueCreated && connection.Value.IsCompletedSuccessfully
            then connection.Value.Result.DisposeAsync()
            else ValueTask.CompletedTask