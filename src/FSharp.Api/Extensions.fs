namespace FSharp.Api

open System
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

module SqlConnection =

    let get () =
        Effect.Create(fun (env:#ISqlConnection) ->
            env.GetSqlConnection()
        )

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

module Effect =

    let someOr e (effect:Effect<_, _ option, _>) =
        eff {
            let! x = effect
            let! r = Option.asResult e x
            return r
        }