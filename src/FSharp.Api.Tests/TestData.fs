namespace FSharp.Api

open System
open Microsoft.Data.SqlClient

module TestData =

    let execute (connectionString:string) (sql:string) =
        task {
            use connection = new SqlConnection(connectionString)
            do! connection.OpenAsync()
            use command = connection.CreateCommand()
            command.CommandText <- sql
            let! _ = command.ExecuteNonQueryAsync()

            ()
        }

    let insertUser (id:Guid) email conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email)
                VALUES ('%s{string id}', '%s{email}')
            "

    let insertUsers n conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email)
                SELECT NEWID(), CONCAT('user', value, '@domain.com')
                FROM GENERATE_SERIES(1, %i{n})
            "