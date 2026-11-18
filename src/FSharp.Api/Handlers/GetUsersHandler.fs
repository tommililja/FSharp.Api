namespace FSharp.Api

open FSharp.Control
open Orsak

module GetUsersHandler =

    let handle () =
        eff {
            let! users = Database.Users.getStream ()

            let response =
                users
                |> TaskSeq.map UserResponse.fromUser

            return ContentStream response
        }
