namespace FSharp.Api

open Farse
open Microsoft.Extensions.Logging
open Orsak

module GetUserHandler =

    let handle userId =
        eff {
            let! logger = Logger.get "GetUserHandler"

            let! user =
                userId
                |> Database.Users.tryGet
                |> Effect.someOr NotFound

            logger.LogInformation("Found user {UserId}.", UserId.asString user.Id)

            let json =
                JObj [
                    "id", JStr (UserId.asString user.Id)
                    "email", JStr (Email.asString user.Email)
                    "birthDate", JStr (LocalDate.as8601String user.BirthDate)
                    "registeredAt", JStr (Instant.as8601String user.RegisteredAt)
                ]

            return Content json
        }
