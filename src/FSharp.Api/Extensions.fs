namespace FSharp.Api

open System
open System.Data
open System.Net.Http
open Donald
open Farse
open NodaTime.Text
open Orsak

[<AutoOpen>]
module Operators =

    let (!) = ignore

module Option =

    let asResult e = function
        | Some x -> Ok x
        | None -> Error e

module Guid =

    let create () =
        Effect.Create(fun (env:#IGuid) ->
            env.CreateGuid()
        )

    let asString (x:Guid) =
        x.ToString("N")

module Instant =

    let now () =
        Effect.Create(fun (env:#IInstant) ->
            env.GetCurrentTime()
        )

    let as8601String =
        InstantPattern.ExtendedIso.Format

module LocalDate =

    let parse str =
        match LocalDatePattern.Iso.Parse(str) with
        | result when result.Success -> Ok result.Value
        | _ -> Error "Expected 'yyyy-MM-dd' format."

    let as8601String x =
        LocalDatePattern.Iso.Format(x)

module SqlConnection =

    let get () =
        Effect.Create(fun (env:#ISqlConnection) ->
            env.GetSqlConnection()
        )

module Db =

    let useDateTime2 (dbUnit:DbUnit) =
        for param in dbUnit.Command.Parameters do
            let dataParam = param :?> IDbDataParameter
            if dataParam.DbType = DbType.DateTime then
                dataParam.DbType <- DbType.DateTime2
        dbUnit

module CancellationToken =

    let get () =
        Effect.Create(fun (env:#ICancellationToken) ->
            env.GetCancellationToken()
        )

module Logger =

    let get name =
        Effect.Create (fun (env:#ILogging) ->
            env.GetLogger(name)
        )

module HttpClient =

    let get () =
        Effect.Create (fun (env:#IHttpClient) ->
            env.GetHttpClient()
        )

module HttpResponseMessage =

    let parse parser token (message:HttpResponseMessage) =
        task {
            use! stream = message.Content.ReadAsStreamAsync(token)
            let! result = parser |> Parser.parseAsync stream token
            return result
        }

module Effect =

    let someOr e (effect:Effect<_, _ option, _>) =
        eff {
            let! x = effect
            let! r = Option.asResult e x
            return r
        }

    let noneOr e (effect:Effect<_, _ option, _>) =
        eff {
            let! x = effect
            return!
                match x with
                | Some _ -> Error e
                | None -> Ok ()
        }