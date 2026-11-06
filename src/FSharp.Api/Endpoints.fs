namespace FSharp.Api

open Oxpecker

module Endpoints =
    open EndpointHandlers

    let list =
        [
            subRoute "/users" [
                GET [
                    router "/" GetUsersHandler.handle
                    routefr "/{%O:guid}" (UserId.fromGuid >> GetUserHandler.handle)
                ]
                POST [ routeb "/" RegisterUserHandler.parse RegisterUserHandler.handle ]
                DELETE [ routefr "/{%O:guid}" (UserId.fromGuid >> UnregisterUserHandler.handle) ]
            ]

            GET [
                route "/" (htmlView Docs.html)
                route "/openapi.json" (jsonBytes Docs.json)
            ]
        ]