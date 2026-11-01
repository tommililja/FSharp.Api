namespace FSharp.Api

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Data.SqlClient
open Microsoft.Extensions.Logging

type IGuid =
    abstract CreateGuid: unit -> Guid

type ISqlConnection =
    abstract GetSqlConnection: unit -> Task<SqlConnection>

type ICancellationToken =
    abstract GetCancellationToken: unit -> CancellationToken

type ILogging =
    abstract GetLogger: string -> ILogger

type IAppEnv =
    inherit IGuid
    inherit ISqlConnection
    inherit ICancellationToken
    inherit ILogging