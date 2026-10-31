namespace FSharp.Api.Tests

open System
open System.Net
open System.Threading
open FSharp.Api
open Xunit
open Expecto.Flip
open Farse

module UserTests =

    [<Fact>]
    let ``POST /user should register a User and return 201 with the correct location`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            let! register =
                app.Client.PostAsync("/user", request)
                |> Task.map (Expect.statusCode HttpStatusCode.Created)

            let! user =
                app.Client.GetAsync(register.Headers.Location)
                |> Task.map (Expect.statusCode HttpStatusCode.OK)

            let! stream = user.Content.ReadAsStreamAsync()
            let! json = stream |> Json.fromStreamAsync CancellationToken.None

            let expected =
                JObj [
                    "id", JStr (Guid.asString id)
                    "email", JStr email
                ]

            let actual = json |> Expect.wantOk "Expected valid JSON."

            return Expect.equalJson expected actual
        }

    [<Fact>]
    let ``POST /user should return 400 when the email is invalid`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alicedomain.com"

            use! app = TestApp.create id

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            do!
                app.Client.PostAsync("/user", request)
                |> Task.map (Expect.statusCode HttpStatusCode.BadRequest >> ignore)
        }

    [<Fact>]
    let ``POST /user should return 409 when the User is already registered`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            do! TestData.insertUser id email app.ConnectionString

            let request =
                JObj [ "email", JStr email ]
                |> Json.asStringContent

            do!
                app.Client.PostAsync("/user", request)
                |> Task.map (Expect.statusCode HttpStatusCode.Conflict >> ignore)
        }

    [<Fact>]
    let ``GET /user should return all Users`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            do! TestData.insertUsers 100 app.ConnectionString

            let! users =
                app.Client.GetAsync("/user")
                |> Task.map (Expect.statusCode HttpStatusCode.OK)

            let! stream = users.Content.ReadAsStreamAsync()
            let! json = stream |> Json.fromStreamAsync CancellationToken.None

            let actual =
                match json with
                | Ok (JArr list) -> list.Length
                | _ -> failwith "Expected an array."

            return Expect.equal "Expected equal values." 100 actual
        }

    [<Fact>]
    let ``GET /user/id should return 404 when the User is not found`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            do!
                app.Client.GetAsync($"/user/%s{Guid.asString id}")
                |> Task.map (Expect.statusCode HttpStatusCode.NotFound >> ignore)
        }

    [<Fact>]
    let ``DELETE /user/id should unregister the User and return 204`` () =
        task {
            let id = Guid.NewGuid()
            let email = "alice@domain.com"

            use! app = TestApp.create id

            do! TestData.insertUser id email app.ConnectionString

            do!
                app.Client.DeleteAsync($"/user/%s{Guid.asString id}")
                |> Task.map (Expect.statusCode HttpStatusCode.NoContent >> ignore)

            let! user = app.Client.GetAsync($"/user/%s{Guid.asString id}")

            return Expect.statusCode HttpStatusCode.NotFound user
        }

    [<Fact>]
    let ``DELETE /user/id should return 404 when the user is not found`` () =
        task {
            let id = Guid.NewGuid()

            use! app = TestApp.create id

            do!
                app.Client.DeleteAsync($"/user/%s{Guid.asString id}")
                |> Task.map (Expect.statusCode HttpStatusCode.NotFound >> ignore)
        }
