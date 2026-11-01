namespace FSharp.Api

open System.Collections.Generic
open System.Text.Json
open FSharp.Control
open FSharp.Api.Core
open Oxpecker
open Farse

[<AutoOpen>]
module Response =

    [<Literal>]
    let private JsonContentType =
        "application/json; charset=utf-8"

    let json statusCode json : EndpointHandler =
        fun ctx ->
            task {
                ctx.Response.StatusCode <- statusCode
                ctx.Response.ContentType <- JsonContentType
                use writer = new Utf8JsonWriter(ctx.Response.BodyWriter)
                Json.writeTo writer json
                do! writer.FlushAsync()
            }

    let jsonBytes (bytes:byte[]) : EndpointHandler =
        fun ctx ->
            task {
                ctx.Response.ContentType <- JsonContentType
                ctx.Response.StatusCode <- 200
                ctx.Response.ContentLength <- bytes.Length
                let! _ = ctx.Response.BodyWriter.WriteAsync(bytes, ctx.RequestAborted)
                ()
            }

    let stream (sequence:IAsyncEnumerable<Json>) : EndpointHandler =
        fun ctx ->
            task {
                ctx.Response.StatusCode <- 200
                ctx.Response.ContentType <- JsonContentType

                let body = ctx.Response.BodyWriter
                use writer = new Utf8JsonWriter(body)

                let mutable flushed = 0L
                let flush () =
                    task {
                        writer.Flush()
                        let! _ = body.FlushAsync(ctx.RequestAborted)
                        flushed <- writer.BytesCommitted
                    }

                writer.WriteStartArray()

                do! sequence
                    |> TaskSeq.iterAsync (fun json ->
                        task {
                            Json.writeTo writer json

                            if (writer.BytesCommitted + int64 writer.BytesPending) - flushed > 16_384L then
                                do! flush ()
                        }
                    )

                writer.WriteEndArray()

                do! flush ()
            }

[<NoComparison>]
type ApiResponse =
    | Content of Json
    | NoContent
    | CreatedAt of location:string
    | Stream of IAsyncEnumerable<Json>

[<NoComparison>]
type ApiError =
    | NotFound
    | BadRequest of Json option
    | Conflict
    | InternalServerError

module ApiResponse =

    let handle = function
        | Content body -> json 200 body
        | NoContent -> setStatusCode 204
        | CreatedAt location ->
            setStatusCode 201
            >=> setHttpHeader "Location" location
        | Stream seq -> stream seq

module ApiError =

    let handle = function
        | NotFound -> setStatusCode 404
        | BadRequest body ->
            match body with
            | Some body -> json 400 body
            | None -> setStatusCode 400
        | Conflict -> setStatusCode 409
        | InternalServerError -> setStatusCode 500

[<AutoOpen>]
module Routing =

    let private resolve io : EndpointHandler =
        fun ctx ->
            task {
                let runner = ctx.GetService<IAppEnv>()

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

    // Short for route + resolve.
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