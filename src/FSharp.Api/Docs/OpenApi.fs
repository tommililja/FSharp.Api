namespace FSharp.Api

open System.Collections.Generic
open System.Text.Json.Nodes
open Microsoft.OpenApi

module OpenApi =

    let headers list =
        list
        |> List.map (fun (k, v) -> k, v :> IOpenApiHeader)
        |> dict

    let tagRefs list =
        list
        |> List.map OpenApiTagReference
        |> HashSet

    let parameters list =
        list
        |> List.map (fun x -> x :> IOpenApiParameter)
        |> ResizeArray

    let responses list =
        let responses = OpenApiResponses()
        list
        |> List.iter responses.Add
        responses

    let paths list =
        let paths = OpenApiPaths()
        list
        |> List.iter paths.Add
        paths

    let pathItem list =
        let item = OpenApiPathItem()
        list
        |> List.iter item.AddOperation
        item

    let examples<'a> list =
        list
        |> List.map (fun x -> JsonValue.Create<'a>(x) :> JsonNode)
        |> ResizeArray

    let jsonResponse description schema =
        OpenApiResponse(
            Description = description,
            Content = dict [
                "application/json", OpenApiMediaType(Schema = schema)
            ]
        )

    let jsonRequest schema =
        OpenApiRequestBody(
            Required = true,
            Content = dict [
                "application/json", OpenApiMediaType(Schema = schema)
            ]
        )