namespace FSharp.Api

open Microsoft.Extensions.Logging
open Orsak

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
