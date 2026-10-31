namespace FSharp.Api.Tests

open System.Net.Http
open System.Text
open System.Threading
open Farse

module Expect =

    let equalJson x y =
        match Json.diff x y with
        | Some diff -> failwith $"Expected equal Json values: \n\n%s{diff}."
        | None -> ()

module HttpResponseMessage =

    let asJson (x:HttpResponseMessage) =
        task {
            let! stream = x.Content.ReadAsStreamAsync()
            return! stream |> Json.fromStreamAsync CancellationToken.None
        }

module Json =

    let asStringContent json =
        let string = Json.asString Raw json
        new StringContent(string, Encoding.UTF8, "application/json")