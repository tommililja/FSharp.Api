namespace FSharp.Api.Tests

open System
open System.Net
open FSharp.Api
open Xunit
open Expecto.Flip
open Farse

module UserTests =

    [<Fact>]
    let ``POST /users should register a User and return 201 with the correct location`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            let! register = app.Client.PostAsync("/users", request)
            Expect.equal "Expected equal status codes" HttpStatusCode.Created register.StatusCode

            let! getUser = app.Client.GetAsync(register.Headers.Location)
            Expect.equal "Expected equal status codes" HttpStatusCode.OK getUser.StatusCode

            let! json = HttpResponseMessage.asJson getUser

            let expected =
                JObj [
                    "id", JStr (Guid.asString id)
                    "email", JStr email
                ]

            let actual = json |> Expect.wantOk "Expected valid JSON."

            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``POST /users should return 400 when the email is invalid`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alicedomain.com"

            use! app = TestApp.create id

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            let! register = app.Client.PostAsync("/users", request)

            Expect.equal "Expected equal status codes." HttpStatusCode.BadRequest register.StatusCode
        }

    [<Fact>]
    let ``POST /users should return 409 when the User is already registered`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            do! TestData.insertUser id email app.ConnectionString

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            let! register = app.Client.PostAsync("/users", request)

            Expect.equal "Expected equal status codes." HttpStatusCode.Conflict register.StatusCode
        }

    [<Fact>]
    let ``GET /users should return all Users`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            do! TestData.insertUsers 100 app.ConnectionString

            let! getUsers = app.Client.GetAsync("/users")
            Expect.equal "Expected equal status codes." HttpStatusCode.OK getUsers.StatusCode

            let! json = HttpResponseMessage.asJson getUsers

            let actual =
                match json with
                | Ok (JArr list) -> list.Length
                | _ -> failwith "Expected an array."

            return Expect.equal "Expected equal values." 100 actual
        }

    [<Fact>]
    let ``GET /users/id should return 404 when the User is not found`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            let! getUser = app.Client.GetAsync($"/users/%s{Guid.asString id}")

            Expect.equal "Expected equal status codes." HttpStatusCode.NotFound getUser.StatusCode
        }

    [<Fact>]
    let ``DELETE /users/id should unregister the User and return 204`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            do! TestData.insertUser id email app.ConnectionString

            let! unregister = app.Client.DeleteAsync($"/users/%s{Guid.asString id}")
            Expect.equal "Expected equal status codes." HttpStatusCode.NoContent unregister.StatusCode

            let! getUser = app.Client.GetAsync($"/users/%s{Guid.asString id}")
            Expect.equal "Expected equal status codes." HttpStatusCode.NotFound getUser.StatusCode
        }

    [<Fact>]
    let ``DELETE /users/id should return 404 when the user is not found`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            let! unregister = app.Client.DeleteAsync($"/users/%s{Guid.asString id}")

            Expect.equal "Expected equal status codes." HttpStatusCode.NotFound unregister.StatusCode
        }