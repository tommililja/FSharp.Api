namespace FSharp.Api.Tests

open System
open FSharp.Api
open FSharp.Control
open Microsoft.Data.SqlClient

module TestEnv =

    let create guidGen cancellationToken connectionString logger =
        let sqlConnection = new SqlConnection(connectionString)
        let opened =
            lazy (
                task {
                    do! sqlConnection.OpenAsync(cancellationToken)
                    return sqlConnection
                }
            )

        {
            new IAppEnv with
                member _.GetSqlConnection() = opened.Force()
                member _.CreateGuid() = GuidGen.generate guidGen
                member _.GetLogger _ = logger
                member _.GetCancellationToken() = cancellationToken

            interface IDisposable with
                member _.Dispose() =
                    sqlConnection.Dispose()

            interface IAsyncDisposable with
                member _.DisposeAsync() =
                    sqlConnection.DisposeAsync()
        }