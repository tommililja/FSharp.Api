namespace FSharp.Api

open Oxpecker

module Endpoints =
    open EndpointHandlers

    let list =
        [
            subRoute "/users" [
                GET [
                    router "/" Handlers.GetUsers.handle
                    routefr "/{%O:guid}" (UserId.fromGuid >> Handlers.GetUser.handle)
                ]
                POST [ routeb "/" Handlers.RegisterUser.parser Handlers.RegisterUser.handle ]
                DELETE [ routefr "/{%O:guid}" (UserId.fromGuid >> Handlers.UnregisterUser.handle) ]
            ]

            GET [
                route "/" (htmlView Docs.html)
                route "/openapi.json" (jsonBytes Docs.json)
            ]
        ]