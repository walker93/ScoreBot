﻿Imports System.ComponentModel
Imports System.Text
Imports Telegram.Bot
Imports Telegram.Bot.Types

Module Module1
    Dim WithEvents api As Api

    Dim classifica As New Dictionary(Of ULong, Integer)
    Dim membri As New Dictionary(Of ULong, String)

    Sub Main()
        api = New Api(token)
        Dim bot = api.GetMe.Result
        Console.WriteLine(bot.Username & ": " & bot.Id)

        carica()

        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start()
    End Sub
    Sub run()
        api.StartReceiving()
        Console.WriteLine("bot attivo")
        Console.ReadKey()
        api.StopReceiving()
    End Sub

    'Private Sub api_InlineQueryReceived(sender As Object, e As InlineQueryEventArgs) Handles api.InlineQueryReceived
    '    Dim query As InlineQuery = e.InlineQuery
    '    Console.WriteLine(Now.ToShortDateString + " " + query.Id + ": " + query.Query)
    '    Dim results() As InlineQueryResult
    '    Dim res As New InlineQueryResultArticle
    '    res.Id = 0
    '    res.Title = "+" + query.Query
    '    res.MessageText = res.Title + "per l'utente"
    '    res.HideUrl = True
    'End Sub

    Private Sub api_MessageReceived(sender As Object, e As MessageEventArgs) Handles api.MessageReceived
        Dim message As Message = e.Message
        If message.Chat.Type <> ChatType.Group Then Exit Sub

        If Not membri.ContainsKey(message.From.Id) Then
            membri.Add(message.From.Id, message.From.FirstName)
            classifica.Add(message.From.Id, 0)
            salva()
        End If
        If message.Type = MessageType.TextMessage Then
            Console.WriteLine(message.Text)
            If message.Text.ToLower.StartsWith("/aggiungi") AndAlso admins.Contains(message.From.Id) Then
                'Aggiungi punti

            ElseIf message.Text.ToLower.StartsWith("/togli") AndAlso admins.Contains(message.From.Id) Then
                'Togli punti

            ElseIf message.Text.ToLower.StartsWith("/reset") AndAlso admins.Contains(message.From.Id) Then
                'Azzera punteggio
                Dim keys() = classifica.Keys.ToArray
                For Each key In keys
                    classifica.Item(key) = 0
                Next
                salva()
            ElseIf message.Text.ToLower.StartsWith("/classifica") Then
                'mostra classifica
                Dim reply As New StringBuilder

                For Each key In classifica.Keys
                    reply.AppendLine(membri.Item(key) & ": " & classifica.Item(key))
                Next
                api.SendTextMessage(message.Chat.Id, reply.ToString,, message.MessageId)
            End If
        Else
            If message.NewChatParticipant IsNot Nothing Then
                'nuovo membro
                Dim membro = message.NewChatParticipant
                membri.Add(membro.Id, membro.FirstName)
                classifica.Add(membro.Id, 0)
                salva()
            ElseIf message.LeftChatParticipant IsNot Nothing Then
                'uscito membro
                Dim membro = message.LeftChatParticipant
                membri.Remove(membro.Id)
                classifica.Remove(membro.Id)
            End If
        End If

    End Sub

    Sub carica()
        'legge da file la classifica e la inserisce nel dizionario
        Dim file_classifica As String = "classifica.txt"
        If Not IO.File.Exists(file_classifica) Then IO.File.WriteAllText(file_classifica, "")
        For Each line As String In IO.File.ReadAllLines(file_classifica)
            classifica.Add(line.Split(";")(0), line.Split(";")(1))
        Next
        classifica.OrderBy(Function(x) x.Value)

        'legge da file membri e li inserisce nel dizionario
        Dim file_membri As String = "membri.txt"
        If Not IO.File.Exists(file_membri) Then IO.File.WriteAllText(file_membri, "")
        For Each line As String In IO.File.ReadAllLines(file_membri)
            membri.Add(line.Split(";")(0), line.Split(";")(1))
        Next
    End Sub

    Sub salva()
        'scrive su file la nuova classifica
        Dim file_classifica As String = "classifica.txt"
        Dim lines() As String
        IO.File.Delete(file_classifica)
        For Each record As KeyValuePair(Of ULong, Integer) In classifica
            lines.Add(record.Key & ";" & record.Value)
        Next
        IO.File.WriteAllLines(file_classifica, lines)


        Dim file_membri As String = "membri.txt"
        lines = {}
        IO.File.Delete(file_membri)
        For Each record As KeyValuePair(Of ULong, String) In membri
            lines.Add(record.Key & ";" & record.Value)
        Next
        IO.File.WriteAllLines(file_membri, lines)
        classifica.OrderBy(Function(x) x.Value)
    End Sub

End Module
