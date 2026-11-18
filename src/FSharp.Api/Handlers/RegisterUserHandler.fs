namespace FSharp.Api

open Farse
open Farse.Operators
open Microsoft.Extensions.Logging
open NodaTime
open Orsak

type RegisterUserRequest = {
    Email: Email
    BirthDate: LocalDate
}

module RegisterUserRequest =
    open Parse

    let parse =
        parser {
            let! email = "email" &= refine string Email.parse
            and! birthDate = "birthDate" &= refine string LocalDate.parse
            return {
                Email = email
                BirthDate = birthDate
            }
        }

module RegisterUserHandler =

    let handle request =
        eff {
            let! logger = Logger.get "RegisterUserHandler"

            do!
                request.Email
                |> Database.Users.tryGetByEmail
                |> Effect.noneOr (Conflict "User is already registered.")

            let! _data =
                request.Email
                |> ExampleService.getData
                |> Effect.changeError (ignore >> InternalServerError)

            let! now = Instant.now ()
            let! userId = Guid.create ()

            let user = {
                Id = UserId.fromGuid userId
                Email = request.Email
                BirthDate = request.BirthDate
                RegisteredAt = now
            }

            do! Database.Users.add user

            logger.LogInformation("Registered user {UserId}.", UserId.asString user.Id)

            return CreatedAt $"/users/%s{Guid.asString userId}"
        }