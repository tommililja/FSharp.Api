namespace FSharp.Api

open FSharp.Control
open Microsoft.Extensions.Logging
open Farse
open Farse.Operators
open Orsak

module Handlers =

    module UserResponse =

        let fromUser (user:User) =
            JObj [
                "id", JStr (UserId.asString user.Id)
                "email", JStr (Email.asString user.Email)
            ]

    module RegisterUser =
        open Parse

        type Request = {
            Email: Email
        }

        let parser =
            parser {
                let! email = "email" &= refine string Email.parse
                return { Email = email }
            }

        let handle request =
            eff {
                let! logger = Logger.get "Handlers.RegisterUser"

                do!
                    request.Email
                    |> Database.Users.tryGetByEmail
                    |> Effect.noneOr Conflict

                let! userId = Guid.create ()

                let user = {
                    Id = UserId.fromGuid userId
                    Email = request.Email
                }

                do! Database.Users.add user

                logger.LogInformation("Registered user {UserId}.", UserId.asString user.Id)

                return CreatedAt $"/users/%s{Guid.asString userId}"
            }

    module GetUser =

        let handle userId =
            eff {
                let! logger = Logger.get "Handlers.GetUser"

                let! user =
                    userId
                    |> Database.Users.tryGet
                    |> Effect.someOr NotFound

                logger.LogInformation("Found user {UserId}.", UserId.asString user.Id)

                let json = UserResponse.fromUser user

                return Content json
            }

    module GetUsers =

        let handle () =
            eff {
                let! users = Database.Users.getStream ()

                let response =
                    users
                    |> TaskSeq.map UserResponse.fromUser

                return ContentStream response
            }

    module UnregisterUser =

        let handle userId =
            eff {
                let! logger = Logger.get "Handlers.UnregisterUser"

                do! userId
                    |> Database.Users.tryDelete
                    |> Effect.someOr NotFound

                logger.LogInformation("Unregistered user {UserId}.", UserId.asString userId)

                return NoContent
            }