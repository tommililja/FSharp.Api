namespace FSharp.Api

open FSharp.Control
open Microsoft.Extensions.Logging
open Farse
open Farse.Operators
open NodaTime
open Orsak

module UserResponse =

    let fromUser (user:User) =
        JObj [
            "id", JStr (UserId.asString user.Id)
            "email", JStr (Email.asString user.Email)
            "birthDate", JStr (LocalDate.as8601String user.BirthDate)
        ]

module RegisterUserHandler =
    open Parse

    type Request = {
        Email: Email
        BirthDate: LocalDate
    }

    let parse =
        parser {
            let! email = "email" &= refine string Email.parse
            and! birthDate = "birthDate" &= refine string LocalDate.parse
            return {
                Email = email
                BirthDate = birthDate
            }
        }

    let handle request =
        eff {
            let! logger = Logger.get "RegisterUserHandler"

            do!
                request.Email
                |> Database.Users.tryGetByEmail
                |> Effect.noneOr (Conflict "User is already registered.")

            let! userId = Guid.create ()

            let user = {
                Id = UserId.fromGuid userId
                Email = request.Email
                BirthDate = request.BirthDate
            }

            do! Database.Users.add user

            logger.LogInformation("Registered user {UserId}.", UserId.asString user.Id)

            return CreatedAt $"/users/%s{Guid.asString userId}"
        }

module GetUserHandler =

    let handle userId =
        eff {
            let! logger = Logger.get "GetUserHandler"

            let! user =
                userId
                |> Database.Users.tryGet
                |> Effect.someOr NotFound

            logger.LogInformation("Found user {UserId}.", UserId.asString user.Id)

            let json = UserResponse.fromUser user

            return Content json
        }

module GetUsersHandler =

    let handle () =
        eff {
            let! users = Database.Users.getStream ()

            let response =
                users
                |> TaskSeq.map UserResponse.fromUser

            return ContentStream response
        }

module UnregisterUserHandler =

    let handle userId =
        eff {
            let! logger = Logger.get "UnregisterUserHandler"

            do! userId
                |> Database.Users.tryDelete
                |> Effect.someOr NotFound

            logger.LogInformation("Unregistered user {UserId}.", UserId.asString userId)

            return NoContent
        }