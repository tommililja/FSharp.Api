namespace FSharp.Api.Tests

open System
open Microsoft.Data.SqlClient

type TestUser = {
    Id: Guid
    Email: string
    BirthDate: string
}

module TestData =

    let user = {
        Id = Guid.NewGuid ()
        Email = "alice@domain.com"
        BirthDate = "2000-01-01"
    }

    let execute (connectionString:string) (sql:string) =
        task {
            use connection = new SqlConnection(connectionString)
            do! connection.OpenAsync()
            use command = connection.CreateCommand()
            command.CommandText <- sql
            let! _ = command.ExecuteNonQueryAsync()

            ()
        }

    let insertUser user conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate)
                VALUES ('%s{string user.Id}', '%s{user.Email}', '%s{user.BirthDate}')
            "

    let insertUsers n conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate)
                SELECT NEWID(), CONCAT('user', value, '@domain.com'), '2000-01-01'
                FROM GENERATE_SERIES(1, %i{n})
            "