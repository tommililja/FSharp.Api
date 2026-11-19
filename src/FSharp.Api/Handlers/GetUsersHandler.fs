namespace FSharp.Api

open FSharp.Control
open Farse
open Orsak

module GetUsersHandler =

    let handle () =
        eff {
            let! users = Database.Users.getStream ()

            let response =
                users
                |> TaskSeq.map (fun user ->
                    JObj [
                        "id", JStr (UserId.asString user.Id)
                        "email", JStr (Email.asString user.Email)
                        "birthDate", JStr (LocalDate.as8601String user.BirthDate)
                        "registeredAt", JStr (Instant.as8601String user.RegisteredAt)
                    ]
                )

            return ContentStream response
        }
