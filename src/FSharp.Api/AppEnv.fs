namespace FSharp.Api

open System
open System.Net.Http
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

module AppEnv =

    let create newGuid
        (loggerFactory:ILoggerFactory)
        (httpClientFactory:IHttpClientFactory)
        cancellationToken appSettings =

        let sqlConnection = new SqlConnection(appSettings.ConnectionString)
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
                member _.CreateGuid() = newGuid ()
                member _.GetLogger(name) = loggerFactory.CreateLogger(name)
                member _.GetCancellationToken() = cancellationToken
                member _.GetHttpClient() = httpClientFactory.CreateClient()
                member _.GetAppSettings() = appSettings

            interface IDisposable with
                member _.Dispose() =
                    sqlConnection.Dispose()

            interface IAsyncDisposable with
                member _.DisposeAsync() =
                    sqlConnection.DisposeAsync()
        }