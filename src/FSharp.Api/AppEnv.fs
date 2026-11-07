namespace FSharp.Api

open System
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

module AppEnv =

    let create (factory:ILoggerFactory) cancellationToken connectionString =
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
                member _.CreateGuid() = Guid.NewGuid()
                member _.GetLogger(name) = factory.CreateLogger(name)
                member _.GetCancellationToken() = cancellationToken

            interface IDisposable with
                member _.Dispose() =
                    sqlConnection.Dispose()

            interface IAsyncDisposable with
                member _.DisposeAsync() =
                    sqlConnection.DisposeAsync()
        }