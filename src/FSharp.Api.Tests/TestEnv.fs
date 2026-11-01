namespace FSharp.Api.Tests

open System
open System.Threading
open FSharp.Api
open FSharp.Control
open Meziantou.Extensions.Logging.Xunit.v3
open Microsoft.Data.SqlClient

type TestEnv(guidGen, cancellationToken, connectionString, output) =

    let sqlConnection = new SqlConnection(connectionString)

    let opened =
        lazy (
            task {
                do! sqlConnection.OpenAsync(cancellationToken)
                return sqlConnection
            }
        )

    interface IAppEnv with
        member _.GetSqlConnection() = opened.Force()
        member _.CreateGuid() = GuidGen.generate guidGen
        member _.GetLogger _ = XUnitLogger.CreateLogger(output)
        member _.GetCancellationToken() = CancellationToken.None

    interface IDisposable with
        member _.Dispose() =
            sqlConnection.Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            sqlConnection.DisposeAsync()