namespace FSharp.Api

open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi

module UserPaths =

    let private guidSchema =
        OpenApiSchema(
            Type = JsonSchemaType.String,
            Pattern = "^[0-9a-f]{32}$",
            Description = "Guid",
            Examples = OpenApi.examples [ "fd0a0dc373e04a749aa0df860f6bf78f" ]
        )

    let private emailSchema =
        OpenApiSchema(
            Type = JsonSchemaType.String,
            Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            Description = "Email",
            Examples = OpenApi.examples [ "alice@domain.com" ]
        )

    let private dateSchema description =
        OpenApiSchema(
            Type = JsonSchemaType.String,
            Format = "date",
            Description = description,
            Examples = OpenApi.examples [ "2026-01-01" ]
        )

    let private userIdParam =
        OpenApiParameter(
            Name = "id",
            In = ParameterLocation.Path,
            Required = true,
            Schema = guidSchema
        )

    let private userSchema =
        OpenApiSchema(
            Type = JsonSchemaType.Object,
            Required = HashSet [ "id"; "email"; "birthDate" ],
            Properties = dict [
                "id", guidSchema
                "email", emailSchema
                "birthDate", dateSchema "Birth date (ISO 8601, yyyy-MM-dd)"
            ]
        )

    let registerUser =
        OpenApiOperation (
            Summary = "Register user",
            Description = "Registers a user.",
            RequestBody =
                OpenApi.jsonRequest
                    (OpenApiSchema (
                        Type = JsonSchemaType.Object,
                        Required = HashSet([ "email"; "birthDate" ]),
                        Properties = dict [
                            "email", emailSchema
                            "birthDate", dateSchema "Birth date (ISO 8601, yyyy-MM-dd)"
                        ]
                    )),
            Responses =
                OpenApi.responses [
                    "201", OpenApiResponse(
                        Description = "User registered.",
                        Headers = OpenApi.headers [
                            "Location", OpenApiHeader(
                                Description = "URL of the registered user.",
                                Schema = OpenApiSchema(Type = JsonSchemaType.String)
                            )
                        ])
                    "400", OpenApi.validationProblemJsonResponse "Invalid request body."
                    "409", OpenApi.problemJsonResponse "User already registered."
                ],
            Tags = OpenApi.tagRefs [ "Users" ]
        )

    let getUser =
        OpenApiOperation (
            Summary = "Get user",
            Description = "Gets a user by id.",
            Parameters =
                OpenApi.parameters [
                    userIdParam
                ],
            Responses =
                OpenApi.responses [
                    "200", OpenApi.jsonResponse "The user." userSchema
                    "404", OpenApi.problemJsonResponse "User not found."
                ],
            Tags = OpenApi.tagRefs [ "Users" ]
        )

    let getUsers =
        OpenApiOperation(
            Summary = "Get users",
            Description = "Gets all users.",
            Responses =
                OpenApi.responses [
                    "200",
                        OpenApi.jsonResponse "All users."
                            (OpenApiSchema(Type = JsonSchemaType.Array, Items = userSchema))
                ],
            Tags = OpenApi.tagRefs [ "Users" ]
        )

    let unregisterUser =
        OpenApiOperation (
            Summary = "Unregister user",
            Description = "Unregisters a user by id.",
            Parameters =
                OpenApi.parameters [
                    userIdParam
                ],
            Responses =
                OpenApi.responses [
                    "204", OpenApiResponse(Description = "User unregistered.")
                    "404", OpenApi.problemJsonResponse "User not found."
                ],
            Tags = OpenApi.tagRefs [ "Users" ]
        )

    let paths =
        [
            "/users", OpenApi.pathItem [
                HttpMethod.Get, getUsers
                HttpMethod.Post, registerUser
            ]
            "/users/{id}", OpenApi.pathItem [
                HttpMethod.Get, getUser
                HttpMethod.Delete, unregisterUser
            ]
        ]