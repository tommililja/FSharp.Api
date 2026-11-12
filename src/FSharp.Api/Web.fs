namespace FSharp.Api

open System.Collections.Generic
open System.Text.Json
open FSharp.Control
open Microsoft.AspNetCore.Http
open Orsak
open Oxpecker
open Farse

module EndpointHandlers =

    [<Literal>]
    let private JsonContentType =
        "application/json; charset=utf-8"

    let validationProblem detail errors : EndpointHandler =
        fun ctx ->
            let errors =
                errors
                |> List.map (fun e ->
                    e.Path
                    |> JsonPath.asString, [| e.Details |]
                )

            Results.ValidationProblem(dict errors,
                detail = detail,
                instance = ctx.Request.Path.Value
            ).ExecuteAsync(ctx)

    let problem (statusCode:int) detail : EndpointHandler =
        fun ctx ->
            Results.Problem(
                statusCode = statusCode,
                detail = detail,
                instance = ctx.Request.Path.Value
            ).ExecuteAsync(ctx)

    let json statusCode json : EndpointHandler =
        fun ctx ->
            task {
                ctx.Response.StatusCode <- statusCode
                ctx.Response.ContentType <- JsonContentType
                use writer = new Utf8JsonWriter(ctx.Response.BodyWriter)
                Json.writeTo writer json
                writer.Flush()
                let! _ = ctx.Response.BodyWriter.FlushAsync(ctx.RequestAborted)
                ()
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

    let stream (items:IAsyncEnumerable<Json>) : EndpointHandler =
        fun ctx ->
            task {
                ctx.Response.StatusCode <- 200
                ctx.Response.ContentType <- JsonContentType

                let body = ctx.Response.BodyWriter
                use writer = new Utf8JsonWriter(body)

                let mutable started = false
                let mutable flushed = 0L

                // Sends the headers and opens the array. Called once the first row has been read,
                // so a failure before that leaves the response untouched for the exception handler.
                let start () =
                    task {
                        do! ctx.Response.StartAsync(ctx.RequestAborted)
                        writer.WriteStartArray()
                        started <- true
                    }

                let flush () =
                    task {
                        writer.Flush()
                        let! _ = body.FlushAsync(ctx.RequestAborted)
                        flushed <- writer.BytesCommitted
                    }

                do!
                    items
                    |> TaskSeq.iterAsync (fun json ->
                        task {
                            if not started then
                                do! start ()

                            Json.writeTo writer json

                            if (writer.BytesCommitted + int64 writer.BytesPending) - flushed > 16_384L then
                                do! flush ()
                        }
                    )

                if not started then
                    do! start ()

                writer.WriteEndArray()

                do! flush ()
            }

[<NoComparison>]
type ApiResponse =
    | Content of Json
    | ContentStream of stream:IAsyncEnumerable<Json>
    | CreatedAt of location:string
    | NoContent

[<NoComparison; NoEquality>]
type ApiError =
    | NotFound
    | BadRequest of detail:string * ParseError list
    | Conflict of detail:string
    | InternalServerError

module ApiResponse =
    open EndpointHandlers

    let handle = function
        | Content body -> json 200 body
        | NoContent -> setStatusCode 204
        | CreatedAt location ->
            setStatusCode 201
            >=> setHttpHeader "Location" location
        | ContentStream seq -> stream seq

module ApiError =
    open EndpointHandlers

    let notFound : EndpointHandler =
        problem 404 "Resource not found."

    let handle = function
        | NotFound -> notFound
        | BadRequest (detail, errors) ->
            match errors with
            | [] -> problem 400 detail
            | items -> validationProblem detail items
        | Conflict detail -> problem 409 detail
        | InternalServerError -> problem 500 "An unexpected error occurred."

module Routing =

    let private resolve eff : EndpointHandler =
        fun ctx ->
            task {
                let env = ctx.GetService<IAppEnv>()

                let! result = eff |> Effect.run env

                let handler =
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

    // Short for route + parse ctx + resolve.
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
                    | Error e ->
                        let detail =
                            match e with
                            | Json _ -> "Invalid JSON.", []
                            | Errors list -> "Invalid request body.", list

                        detail
                        |> BadRequest
                        |> ApiError.handle

                return! response ctx
            }
        )