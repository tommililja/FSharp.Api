namespace FSharp.Api

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

            let json = UserResponse.fromUser user

            return Content json
        }
