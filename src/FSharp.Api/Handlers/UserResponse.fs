namespace FSharp.Api

open Farse

module UserResponse =

    let fromUser (user:User) =
        JObj [
            "id", JStr (UserId.asString user.Id)
            "email", JStr (Email.asString user.Email)
            "birthDate", JStr (LocalDate.as8601String user.BirthDate)
            "registeredAt", JStr (Instant.as8601String user.RegisteredAt)
        ]