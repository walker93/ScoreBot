Imports System.ComponentModel
Imports System.Text
Imports Telegram.Bot
Imports Telegram.Bot.Types

Module Module1
    Dim WithEvents api As Api
    Dim flush As Boolean
    Dim classifica As New Dictionary(Of ULong, Integer)
    Dim membri As New Dictionary(Of ULong, String)
    Dim time_start As Date = Date.UtcNow
    Sub Main(ByVal args() As String)
        api = New Api(token)
        Dim bot = api.GetMe.Result
        Console.WriteLine(bot.Username & ": " & bot.Id)
        carica()
        flush = args(0).Contains("flush")
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
        'controllo flush, se attivo ignoro il messaggio
        If flush Then
            If message.Date < time_start Then Exit Sub
        End If
        If message.Chat.Type <> ChatType.Group Then Exit Sub

        'se il membro non è nei dizionari lo aggiungo
        If Not membri.ContainsKey(message.From.Id) Then
            membri.Add(message.From.Id, message.From.FirstName)
            classifica.Add(message.From.Id, 0)
        Else
            'se lo è, verifico che il nome corrisponda
            If Not membri.Item(message.From.Id) = message.From.FirstName Then
                membri.Item(message.From.Id) = membri.Item(message.From.Id) = message.From.FirstName
            End If
        End If
        salva()

        If message.Type = MessageType.TextMessage Then
            'è un messaggio di testo, lo processo
            Console.WriteLine(message.Text)
            If message.Text.ToLower.StartsWith("/aggiungi") AndAlso admins.Contains(message.From.Id) Then
                'Aggiungi punti
                Dim params() As String = message.Text.Split(" ")
                Dim punti As Integer
                If params.Length < 2 Then Exit Sub

                If Integer.TryParse(params(1), punti) Then
                    If message.ReplyToMessage IsNot Nothing Then
                        classifica.Item(message.ReplyToMessage.From.Id) += punti
                        api.SendTextMessage(message.Chat.Id, message.ReplyToMessage.From.FirstName & " guadagna " & punti & " punti!",, message.MessageId)
                    Else
                        If membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = params.Last.ToLower()).Count() > 0 Then
                            Dim membro As ULong
                            For Each record As KeyValuePair(Of ULong, String) In membri
                                If record.Value.ToLower = params.Last.ToLower Then membro = record.Key
                            Next
                            If membro <> 0 Then
                                classifica.Item(membro) += punti

                                api.SendTextMessage(message.Chat.Id, params.Last & " guadagna " & punti & " punti!",, message.MessageId)
                            Else
                                api.SendTextMessage(message.Chat.Id, "Utente non trovato",, message.MessageId)
                            End If
                        End If
                    End If
                Else
                    api.SendTextMessage(message.Chat.Id, "Impossibile riconoscere valore punteggio",, message.MessageId)
                End If
            ElseIf message.Text.ToLower.StartsWith("/togli") AndAlso admins.Contains(message.From.Id) Then
                'Togli punti
                Dim params() As String = message.Text.Split(" ")
                Dim punti As Integer
                If params.Length < 2 Then Exit Sub

                If Integer.TryParse(params(1), punti) Then
                    If message.ReplyToMessage IsNot Nothing Then
                        classifica.Item(message.ReplyToMessage.From.Id) -= punti
                        api.SendTextMessage(message.Chat.Id, message.ReplyToMessage.From.FirstName & " perde " & punti & " punti!",, message.MessageId)
                    Else
                        If membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = params.Last.ToLower()).Count() > 0 Then
                            Dim membro As ULong
                            For Each record As KeyValuePair(Of ULong, String) In membri
                                If record.Value.ToLower = params.Last.ToLower Then membro = record.Key
                            Next
                            If membro <> 0 Then
                                classifica.Item(membro) -= punti
                                api.SendTextMessage(message.Chat.Id, params.Last & " perde " & punti & " punti!",, message.MessageId)
                            Else
                                api.SendTextMessage(message.Chat.Id, "Utente non trovato",, message.MessageId)
                            End If
                        End If
                    End If
                Else
                    api.SendTextMessage(message.Chat.Id, "Impossibile riconoscere valore punteggio",, message.MessageId)
                End If
            ElseIf message.Text.ToLower.StartsWith("/reset") AndAlso admins.Contains(message.From.Id) Then
                'Azzera punteggio
                Dim keys() = classifica.Keys.ToArray
                For Each key In keys
                    classifica.Item(key) = 0
                Next

            ElseIf message.Text.ToLower.StartsWith("/classifica") Then
                'mostra classifica
                Dim reply As New StringBuilder
                Dim i As Integer = 1
                Dim sortedList = From pair In classifica
                                 Order By pair.Value Descending
                For Each pair In sortedList
                    reply.AppendLine(i & "° " & membri.Item(pair.Key) & ": " & classifica.Item(pair.Key))
                    i += 1
                Next
                api.SendTextMessage(message.Chat.Id, reply.ToString,, message.MessageId)
            End If
        Else
            'non è un messaggio di testo, ma di servizio
            If message.NewChatParticipant IsNot Nothing Then
                'nuovo membro
                Dim membro = message.NewChatParticipant
                membri.Add(membro.Id, membro.FirstName)
                classifica.Add(membro.Id, 0)

            ElseIf message.LeftChatParticipant IsNot Nothing Then
                'uscito membro
                Dim membro = message.LeftChatParticipant
                membri.Remove(membro.Id)
                classifica.Remove(membro.Id)
            End If
        End If
        salva()
    End Sub

    Sub carica()
        'legge da file la classifica e la inserisce nel dizionario
        Dim file_classifica As String = "classifica.txt"
        If Not IO.File.Exists(file_classifica) Then IO.File.WriteAllText(file_classifica, "")
        For Each line As String In IO.File.ReadAllLines(file_classifica)
            classifica.Add(line.Split(";")(0), line.Split(";")(1))
        Next


        'legge da file membri e li inserisce nel dizionario
        Dim file_membri As String = "membri.txt"
        If Not IO.File.Exists(file_membri) Then IO.File.WriteAllText(file_membri, "")
        For Each line As String In IO.File.ReadAllLines(file_membri)
            membri.Add(line.Split(";")(0), line.Split(";")(1))
        Next
    End Sub

    Sub modifica_punti(punti As Integer, message As Message)

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

    End Sub

End Module
