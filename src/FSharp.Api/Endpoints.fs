namespace FSharp.Api

open Oxpecker

module Endpoints =

    let private docs =
        [
            GET [ route "/" (htmlView Docs.html) ]
            GET [ route Docs.JsonFilename (jsonString Docs.json) ]
        ]

    let list =
        [
            subRoute "/user" [
                GET [
                    router "/" GetUsers.handle
                    routefr "/{%O:guid}" (UserId.fromGuid >> GetUser.handle)
                ]
                POST [ routeb "/" RegisterUser.parser RegisterUser.handle ]
                DELETE [ routefr "/{%O:guid}" (UserId.fromGuid >> Unregister.handle) ]
            ]
        ]
        |> List.append docs