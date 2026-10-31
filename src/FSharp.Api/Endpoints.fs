namespace FSharp.Api

open Oxpecker

module Endpoints =

    let private docs =
        [
            GET [ route "/" (htmlView Docs.html) ]
            GET [ route Docs.JsonFilename (jsonBytes Docs.json) ]
        ]

    let list =
        [
            subRoute "/user" [
                GET [
                    router "/" Handlers.GetUsers.handle
                    routefr "/{%O:guid}" (UserId.fromGuid >> Handlers.GetUser.handle)
                ]
                POST [ routeb "/" Handlers.RegisterUser.parser Handlers.RegisterUser.handle ]
                DELETE [ routefr "/{%O:guid}" (UserId.fromGuid >> Handlers.UnregisterUser.handle) ]
            ]
        ]
        |> List.append docs