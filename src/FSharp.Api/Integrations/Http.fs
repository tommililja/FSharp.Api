namespace FSharp.Api

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Orsak
open Farse

[<NoEquality; NoComparison>]
type HttpError =
    | Unreachable of message:string
    | UnexpectedStatus of status:HttpStatusCode
    | InvalidBody of ParserError
    | Timeout

module Url =

    let withQuery query path =
        match query with
        | [] -> path
        | _ ->
            query
            |> List.map (fun (key:string, value:string) ->
                $"%s{Uri.EscapeDataString key}=%s{Uri.EscapeDataString value}"
            )
            |> String.concat "&"
            |> sprintf "%s?%s" path

module Http =

    let private trySend (url:Uri) =
        eff {
            let! client = HttpClient.get ()
            let! token = CancellationToken.get ()
            try
                let! response = client.GetAsync(url, token)
                return Ok response
            with
                | :? HttpRequestException as exn -> return Error (Unreachable exn.Message)
                | :? TaskCanceledException when not token.IsCancellationRequested -> return Error Timeout
        }

    let getJson parser (url:Uri) =
        eff {
            let! token = CancellationToken.get ()
            let! logger = Logger.get "Http.getJson"

            match! trySend url with
            | Error error -> return! Error error
            | Ok (response:HttpResponseMessage) when response.IsSuccessStatusCode ->
                let! json = HttpResponseMessage.parse parser token response
                return! json |> Result.mapError InvalidBody
            | Ok response ->
                logger.LogError("Got '{StatusCode}' from {Url}.", response.StatusCode, url)
                return! Error (UnexpectedStatus response.StatusCode)
        }