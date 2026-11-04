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
            let errors : IDictionary<string, string[]> =
                errors
                |> List.map (_.Path >> JsonPath.asString)
                |> List.distinct
                |> List.map (fun path -> path, [| "The value is missing or invalid." |])
                |> dict

            Results.ValidationProblem(errors,
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
    | ContentStream of IAsyncEnumerable<Json>
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

    let handle = function
        | NotFound -> problem 404 "Resource not found."
        | BadRequest (detail, errors) -> validationProblem detail errors
        | Conflict detail -> problem 409 detail
        | InternalServerError -> problem 500 "An unexpected error occurred."

[<AutoOpen>]
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
                        let details =
                            match e with
                            | Json _ -> "Invalid JSON.", []
                            | Errors list -> "Invalid request body.", list

                        details
                        |> BadRequest
                        |> ApiError.handle

                return! response ctx
            }
        )