namespace FSharp.Api

open System.Collections.Generic
open System.Text.Json.Nodes
open Microsoft.OpenApi

module OpenApi =

    let private stringSchema description =
        OpenApiSchema(Type = JsonSchemaType.String, Description = description) :> IOpenApiSchema

    let private problemProperties =
        [
            "type", stringSchema "A URI identifying the kind of problem."
            "title", stringSchema "A short summary of the kind of problem."
            "status", OpenApiSchema(Type = JsonSchemaType.Integer, Description = "The HTTP status code.") :> IOpenApiSchema
            "detail", stringSchema "An explanation of this occurrence of the problem."
            "instance", stringSchema "The path of the request."
            "traceId", stringSchema "Identifies the request in the logs."
        ]

    let private problemSchema =
        OpenApiSchema(
            Type = JsonSchemaType.Object,
            Required = HashSet [ "type"; "title"; "status" ],
            Properties = dict problemProperties
        )

    let private validationProblemSchema =
        let errors =
            OpenApiSchema(
                Type = JsonSchemaType.Object,
                Description = "Error messages per JSON path.",
                AdditionalProperties =
                    OpenApiSchema(
                        Type = JsonSchemaType.Array,
                        Items = OpenApiSchema(Type = JsonSchemaType.String)
                    )
            ) :> IOpenApiSchema

        OpenApiSchema(
            Type = JsonSchemaType.Object,
            Required = HashSet [ "type"; "title"; "status" ],
            Properties = dict (problemProperties @ [ "errors", errors ])
        )

    let private problemContent schema =
        dict [ "application/problem+json", OpenApiMediaType(Schema = schema) :> IOpenApiMediaType ]

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

    let problemJsonResponse description =
        OpenApiResponse(
            Description = description,
            Content = problemContent problemSchema
        )

    let validationProblemJsonResponse description =
        OpenApiResponse(
            Description = description,
            Content = problemContent validationProblemSchema
        )

    let jsonRequest schema =
        OpenApiRequestBody(
            Required = true,
            Content = dict [
                "application/json", OpenApiMediaType(Schema = schema)
            ]
        )