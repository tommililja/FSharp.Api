namespace FSharp.Api

open System.Collections.Generic
open System.Text.Json
open FSharp.Control
open Oxpecker
open Farse

[<AutoOpen>]
module Response =

    [<Literal>]
    let private JsonContentType = "application/json; charset=utf-8"

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