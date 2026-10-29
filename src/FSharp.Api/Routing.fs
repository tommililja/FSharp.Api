namespace FSharp.Api

open FSharp.Control
open Oxpecker
open Farse

[<AutoOpen>]
module Routing =

    let private resolve io : EndpointHandler =
        fun ctx ->
            task {
                let runner = ctx.GetService<IIOEnv>()

                let! result =
                    io
                    |> IO.run runner
                    |> AsyncResult.asAsync
                    |> Async.startAsTask ctx.RequestAborted

                let handler:EndpointHandler =
                    match result with
                    | Ok x -> ApiResponse.handle x
                    | Error e -> ApiError.handle e

                return! handler ctx
            }

    // Short for routef + resolve.
    let routefr path handler =
        routef path (handler >> resolve)

    // Short for route + warbler + resolve
    let router path handler =
        route path (fun ctx ->
            let handler = handler ()
            resolve handler ctx
        )

    // Short for route + parse body + resolve.
    let routeb path parser handler : Endpoint =
        route path (fun ctx ->
            task {
                let! request =
                    parser
                    |> Parser.parseAsync
                        ctx.Request.Body
                        ctx.RequestAborted

                let response =
                    match request with
                    | Ok x -> handler x |> resolve
                    | Error _ ->
                        // TODO: Return schema error.
                        BadRequest None |> ApiError.handle

                return! response ctx
            }
        )