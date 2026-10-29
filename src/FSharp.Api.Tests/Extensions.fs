namespace FSharp.Api.Tests

open System.Net.Http
open System.Text
open Farse
open Expecto.Flip

module Expect =

    let equalJson x y =
        match Json.diff x y with
        | Some diff -> failwith $"Expected equal Json values: \n\n%s{diff}."
        | None -> ()

    let statusCode code (response:HttpResponseMessage) =
        Expect.equal "Expected equal status codes." code response.StatusCode
        response

module Json =

    let asStringContent json =
        let string = Json.asString Raw json
        new StringContent(string, Encoding.UTF8, "application/json")