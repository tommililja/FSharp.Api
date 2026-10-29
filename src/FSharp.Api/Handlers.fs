namespace FSharp.Api

open FSharp.Control
open Microsoft.Extensions.Logging
open Farse
open Farse.Operators

type RegisterUser = {
    Email: Email
}

module RegisterUser =
    open Parse

    let parser =
        parser {
            let! email = "email" &= refine string Email.parse
            return { Email = email }
        }

    let handle request =
        io {
            let! logger = Logger.get "RegisterUser"

            let! user =
                request.Email
                |> Database.Users.tryGetByEmail

            if user.IsSome then
                logger.LogWarning "User is already registered."
                do! Error Conflict

            let! userId = Guid.create ()

            let user = {
                Id = UserId.fromGuid userId
                Email = request.Email
            }

            do! Database.Users.add user

            logger.LogInformation $"Registered user %s{UserId.asString user.Id}."

            return CreatedAt $"/user/%s{Guid.asString userId}"
        }

module GetUser =

    let handle userId =
        io {
            let! logger = Logger.get "GetUser"

            let! user =
                userId
                |> Database.Users.tryGet
                |> IO.someOr NotFound

            logger.LogInformation $"Found user %s{UserId.asString user.Id}."

            let json =
                JObj [
                    "id", JStr (UserId.asString userId)
                    "email", JStr (Email.asString user.Email)
                ]

            return Content json
        }

module GetUsers =

    let handle () =
        io {
            let! users = Database.Users.getStream ()

            let response =
                users
                |> TaskSeq.map (fun user ->
                    JObj [
                        "id", JStr (UserId.asString user.Id)
                        "email", JStr (Email.asString user.Email)
                    ]
                )

            return Stream response
        }

module Unregister =

    let handle userId =
        io {
            let! logger = Logger.get "Unregister"

            do! userId
                |> Database.Users.tryDelete
                |> IO.someOr NotFound

            logger.LogInformation $"Deleted user %s{UserId.asString userId}."

            return NoContent
        }