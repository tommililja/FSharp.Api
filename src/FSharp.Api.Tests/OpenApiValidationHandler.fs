namespace FSharp.Api.Tests

open System
open System.Collections.Concurrent
open System.Net.Http
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Json.Schema
open Microsoft.OpenApi

module private PathTemplate =

    let private segments (path:string) =
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)

    let private isParameter (segment:string) =
        segment.StartsWith '{' && segment.EndsWith '}'

    let matches (template:string) (path:string) =
        let templateSegments = segments template
        let pathSegments = segments path |> Array.map Uri.UnescapeDataString
        templateSegments.Length = pathSegments.Length
        && Array.forall2 (fun ts ps ->
            isParameter ts || String.Equals(ts, ps, StringComparison.OrdinalIgnoreCase)
        ) templateSegments pathSegments

    let tryFind (templates:string seq) (path:string) =
        templates
        |> Seq.filter (fun template -> matches template path)
        |> Seq.sortBy (fun template -> segments template |> Array.filter isParameter |> Array.length)
        |> Seq.tryHead

    let stripBasePath (basePaths:string list) (path:string) =
        basePaths
        |> List.tryFind (fun basePath ->
            path.Equals(basePath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
        )
        |> Option.map (fun basePath ->
            let rest = path.Substring(basePath.Length)
            if rest = String.Empty then "/"
            else rest
        )
        |> Option.defaultValue path

module private EvaluationResult =

    let getErrors (result:EvaluationResults) =
        [
            yield result
            if isNull result.Details |> not then
                yield! result.Details
        ]
        |> List.filter (fun x -> isNull x.Errors |> not)
        |> List.collect (fun x ->
            let location =
                match string x.InstanceLocation with
                | "" -> "$"
                | location -> location

            x.Errors
            |> Seq.map (fun error -> $"%s{location}: %s{error.Value} (%s{error.Key})")
            |> List.ofSeq
        )

type ErrorReason =
    | UndocumentedPath
    | UndocumentedOperation
    | UndocumentedStatusCode
    | UndocumentedContentType of contentType:string
    | MissingBody of expected:string list
    | InvalidJson of message:string
    | SchemaMismatch of errors:string list

module private ErrorReason =

    let asString = function
        | UndocumentedPath -> "The path is not documented."
        | UndocumentedOperation -> "The method is not documented for this path."
        | UndocumentedStatusCode -> "The status code is not documented for this operation."
        | UndocumentedContentType contentType -> $"The content type '%s{contentType}' is not documented for this response."
        | MissingBody expected ->
            let expected = String.concat ", " expected
            $"Expected a body (%s{expected}), but the response was empty."
        | InvalidJson message -> $"The response body is not valid JSON: %s{message}."
        | SchemaMismatch errors ->
            errors
            |> List.map (sprintf "  %s")
            |> String.concat "\n"
            |> sprintf "The response body does not match the documentation:\n%s"

[<NoComparison>]
type SchemaValidationError = {
    Method: HttpMethod
    Path: string
    StatusCode: int
    Reason: ErrorReason
}

module private SchemaValidationError =

    let asString (error:SchemaValidationError) =
        $"%O{error.Method} %s{error.Path} (%i{error.StatusCode}): %s{ErrorReason.asString error.Reason}"

type OpenApiValidationException(error:SchemaValidationError) =
    inherit Exception(SchemaValidationError.asString error)

    member _.Error = error

type OpenApiValidationHandler(document:OpenApiDocument) =
    inherit DelegatingHandler()

    let schemas = ConcurrentDictionary<IOpenApiSchema, JsonSchema>(HashIdentity.Reference)

    let evaluationOptions = EvaluationOptions(OutputFormat = OutputFormat.List)

    let paths =
        match document.Paths with
        | null -> OpenApiPaths()
        | paths -> paths

    let basePaths =
        document.Servers
        |> Option.ofObj
        |> Option.map List.ofSeq
        |> Option.defaultValue []
        |> List.choose (fun (server:OpenApiServer) ->
            match Uri.TryCreate(server.Url, UriKind.RelativeOrAbsolute) with
            | true, uri when uri.IsAbsoluteUri -> Some uri.AbsolutePath
            | true, _ -> Some server.Url
            | false, _ -> None
        )
        |> List.map _.TrimEnd('/')
        |> List.filter (String.IsNullOrEmpty >> not)

    let tryFindOperation method (pathItem:IOpenApiPathItem) =
        match pathItem.Operations with
        | null -> None
        | operations ->
            match operations.TryGetValue(method) with
            | true, operation -> Some operation
            | _ -> None

    let tryFindResponse statusCode (operation:OpenApiOperation) =
        match operation.Responses with
        | null -> None
        | responses ->
            [ $"%i{statusCode}"; $"%i{statusCode / 100}XX"; "default" ]
            |> List.tryPick (fun key ->
                match responses.TryGetValue(key) with
                | true, response -> Some response
                | _ -> None
            )

    let isJson (mediaType:string) =
        mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
        || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)

    let compile (schema:IOpenApiSchema) token =
        task {
            match schemas.TryGetValue(schema) with
            | true, compiled -> return compiled
            | _ ->
                let! json = schema.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, token)
                return schemas.GetOrAdd(schema, JsonSchema.FromText(json))
        }

    let evaluate (schema:JsonSchema) (body:string) =
        try use json = JsonDocument.Parse(body)
            match schema.Evaluate(json.RootElement, evaluationOptions) with
            | result when result.IsValid -> Ok ()
            | result ->
                result
                |> EvaluationResult.getErrors
                |> SchemaMismatch
                |> Error
        with :? JsonException as exn ->
            Error (InvalidJson exn.Message)

    let validateBody (docs:IOpenApiResponse) mediaType body token =
        task {
            let content =
                docs.Content
                |> Option.ofObj
                |> Option.filter (fun x -> x.Count > 0)

            match content, mediaType with
            | None, _ when String.IsNullOrEmpty(body) -> return Ok ()
            | None, actual ->
                return
                    actual
                    |> Option.defaultValue "None"
                    |> UndocumentedContentType
                    |> Error
            | Some content, None ->
                if String.IsNullOrEmpty(body)
                then return Error (MissingBody (List.ofSeq content.Keys))
                else return Error (UndocumentedContentType "None")
            | Some content, Some actual ->
                let contentMediaType =
                    content
                    |> Seq.tryFind (fun x -> String.Equals(x.Key, actual, StringComparison.OrdinalIgnoreCase))
                    |> Option.map _.Value

                match contentMediaType with
                | None -> return Error (UndocumentedContentType actual)
                | Some mediaType when isNull mediaType.Schema || not (isJson actual) -> return Ok ()
                | Some mediaType ->
                    let! schema = compile mediaType.Schema token
                    return evaluate schema body
        }

    let validate (request:HttpRequestMessage) (response:HttpResponseMessage) (token:CancellationToken) =
        task {
            do! response.Content.LoadIntoBufferAsync(token)
            let! body = response.Content.ReadAsStringAsync(token)

            let path = PathTemplate.stripBasePath basePaths request.RequestUri.AbsolutePath
            let statusCode = int response.StatusCode

            let mediaType =
                response.Content.Headers.ContentType
                |> Option.ofObj
                |> Option.map _.MediaType

            let! result =
                match PathTemplate.tryFind paths.Keys path with
                | None -> Task.FromResult(Error UndocumentedPath)
                | Some template ->
                    match tryFindOperation request.Method paths[template] with
                    | None -> Task.FromResult(Error UndocumentedOperation)
                    | Some operation ->
                        match tryFindResponse statusCode operation with
                        | None -> Task.FromResult(Error UndocumentedStatusCode)
                        | Some documentation -> validateBody documentation mediaType body token

            return
                result
                |> Result.mapError (fun error ->
                    {
                        Method = request.Method
                        Path = path
                        StatusCode = statusCode
                        Reason = error
                    }
                )
        }

    override _.SendAsync(request:HttpRequestMessage, token:CancellationToken) =
        let send = base.SendAsync(request, token)
        task {
            let! response = send
            let! result = validate request response token
            match result with
            | Ok () -> return response
            | Error e -> return raise (OpenApiValidationException e)
        }