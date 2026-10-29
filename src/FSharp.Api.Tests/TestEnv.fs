namespace FSharp.Api.Tests

open System
open System.Threading.Tasks
open FSharp.Api
open FSharp.Control
open Meziantou.Extensions.Logging.Xunit.v3
open Microsoft.Data.SqlClient
open Xunit

type TestEnv(guid:Guid, connectionString:string, output:ITestOutputHelper) =

    let connection =
        lazy (
            task {
                let conn = new SqlConnection(connectionString)
                do! conn.OpenAsync()
                return conn
            }
        )

    interface IIOEnv with
        member _.GetSqlConnection() = connection.Force()
        member _.CreateGuid() = guid
        member _.GetLogger _ = XUnitLogger.CreateLogger(output)

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            if connection.IsValueCreated && connection.Value.IsCompletedSuccessfully
            then connection.Value.Result.DisposeAsync()
            else ValueTask.CompletedTask