namespace FSharp.Api

open System.Data
open System.Data.Common
open FSharp.Control
open Donald
open Orsak

module Database =

    let private command sql param =
        eff {
            let! conn = SqlConnection.get ()
            let! token = CancellationToken.get ()
            return
                conn
                |> Db.newCommand sql
                |> Db.setParams param
                |> Db.setCancellationToken token
        }

    let private exec sql param =
        eff {
            let! cmd = command sql param
            return! Db.Async.exec cmd
        }

    let private trySingle sql param map =
        eff {
            let! cmd = command sql param
            let! x = Db.Async.querySingle map cmd
            return x
        }

    let private list sql param map =
        eff {
            let! cmd = command sql param
            return! Db.Async.query map cmd
        }

    let private stream sql param map =
        eff {
            let! dbUnit = command sql param
            let token = dbUnit.CancellationToken
            let cmd = dbUnit.Command :?> DbCommand

            let! reader = cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token)

            return
                taskSeq {
                    use _ = cmd
                    use reader = reader
                    while! reader.ReadAsync(token) do
                        yield map (reader :> IDataReader)
                }
        }

    module Users =

        let private reader (x:IDataReader) =
            {
                Id = x.ReadGuid "Id" |> UserId.fromGuid
                Email = x.ReadString "Email" |> Email
            }

        let add user =
            exec
                "
                    INSERT INTO [User] (Id, Email)
                    VALUES (@id, @email)
                "
                [
                    "id", sqlGuid (UserId.asGuid user.Id)
                    "email", sqlString (Email.asString user.Email)
                ]

        let getList () =
            list
                "
                    SELECT Id, Email
                    FROM [User]
                "
                []
                reader

        let getStream () =
            stream
                "
                    SELECT Id, Email
                    FROM [User]
                "
                []
                reader

        let tryGet userId =
            trySingle
                "
                    SELECT Id, Email
                    FROM [User]
                    WHERE [Id] = @id
                "
                [ "id", sqlGuid (UserId.asGuid userId) ]
                reader

        let tryGetByEmail email =
            trySingle
                "
                    SELECT Id, Email
                    FROM [User]
                    WHERE [Email] = @email
                "
                [ "email", sqlString (Email.asString email) ]
                reader

        let tryDelete userId =
            trySingle
                "
                    DELETE FROM [User]
                    OUTPUT DELETED.Id
                    WHERE Id = @id
                "
                [
                    "id", sqlGuid (UserId.asGuid userId)
                ]
                ignore