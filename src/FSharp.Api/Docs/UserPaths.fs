namespace FSharp.Api

open System.Collections.Generic
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

    let private userIdParam =
        OpenApiParameter(
            Name = "userId",
            In = ParameterLocation.Path,
            Required = true,
            Schema = guidSchema
        )

    let private userSchema =
        OpenApiSchema(
            Type = JsonSchemaType.Object,
            Required = HashSet [ "id"; "email" ],
            Properties = dict [
                "id", guidSchema
                "email", emailSchema
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
                        Required = HashSet([ "email" ]),
                        Properties = dict [
                            "email", emailSchema
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
                    "400", OpenApiResponse(Description = "Invalid request body.")
                    "409", OpenApiResponse(Description = "User already registered.")
                ],
            Tags = OpenApi.tagRefs [ "User" ]
        )

    let getUser =
        OpenApiOperation (
            Summary = "Get user",
            Description = "Gets a user by userId.",
            Parameters =
                OpenApi.parameters [
                    userIdParam
                ],
            Responses =
                OpenApi.responses [
                    "200", OpenApi.jsonResponse "The user." userSchema
                    "404", OpenApiResponse(Description = "User not found.")
                ],
            Tags = OpenApi.tagRefs [ "User" ]
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
            Tags = OpenApi.tagRefs [ "User" ]
        )

    let unregisterUser =
        OpenApiOperation (
            Summary = "Unregister user",
            Description = "Unregisters a user by userId.",
            Parameters =
                OpenApi.parameters [
                    userIdParam
                ],
            Responses =
                OpenApi.responses [
                    "204", OpenApiResponse(Description = "User unregistered.")
                    "404", OpenApiResponse(Description = "User not found.")
                ],
            Tags = OpenApi.tagRefs [ "User" ]
        )