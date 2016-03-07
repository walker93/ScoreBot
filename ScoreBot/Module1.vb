Imports System.Text
Imports Telegram.Bot
Imports Telegram.Bot.Types

Module Module1
    Dim WithEvents api As Api
    Dim flush As Boolean = False
    Dim classifica As New Dictionary(Of ULong, Integer)
    Dim membri As New Dictionary(Of ULong, String)
    Dim time_start As Date = Date.UtcNow

    Sub Main(ByVal args() As String)
        api = New Api(token)
        Dim bot = api.GetMe.Result
        Console.WriteLine(bot.Username & ": " & bot.Id)
        carica()
        If args.Length > 0 Then flush = args(0).Contains("flush")
        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start()
    End Sub

    Async Sub run()
        Dim updates() As Update
        Dim offset As Integer = 0
        While True
            Try
                updates = Await api.GetUpdates(offset,, 20).ConfigureAwait(False)
                For Each up As Update In updates
                    Select Case up.Type
                        Case UpdateType.MessageUpdate
                            process_Message(up.Message)
                    End Select
                    offset = up.Id + 1
                Next
            Catch ex As AggregateException
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("error getting updates: " & ex.InnerException.Message)
            End Try
        End While
        'api.StartReceiving()
        'Console.WriteLine("bot attivo")
        'Console.ReadKey()
        'api.StopReceiving()
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
        process_Message(message)
    End Sub

    Sub process_Message(message As Message)
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
                membri.Item(message.From.Id) = message.From.FirstName
            End If
        End If
        salva()

        If message.Type = MessageType.TextMessage Then
            'è un messaggio di testo, lo processo
            Console.WriteLine(message.Text)

#Region "aggiungi"
            If message.Text.ToLower.StartsWith("/aggiungi") AndAlso admins.Contains(message.From.Id) Then
                'Aggiungi punti
                Dim params() As String = message.Text.Split(" ")
                If params.Length < 2 Then Exit Sub
                Dim punti As Integer
                If Integer.TryParse(params(1), punti) Then
                    api.SendTextMessage(message.Chat.Id, modifica_punti(punti, message, params.Last),, message.MessageId)
                Else
                    api.SendTextMessage(message.Chat.Id, "Impossibile riconoscere valore punteggio",, message.MessageId)
                End If
#End Region

#Region "togli"
            ElseIf message.Text.ToLower.StartsWith("/togli") AndAlso admins.Contains(message.From.Id) Then
                'Togli punti
                Dim params() As String = message.Text.Split(" ")
                Dim punti As Integer
                If params.Length < 2 Then Exit Sub
                If Integer.TryParse(params(1), punti) Then
                    api.SendTextMessage(message.Chat.Id, modifica_punti(-punti, message, params.Last),, message.MessageId)
                Else
                    api.SendTextMessage(message.Chat.Id, "Impossibile riconoscere valore punteggio",, message.MessageId)
                End If
#End Region

#Region "azzera"
            ElseIf message.Text.ToLower.StartsWith("/reset") AndAlso admins.Contains(message.From.Id) Then
                'Azzera punteggio
                Dim keys() = classifica.Keys.ToArray
                For Each key In keys
                    classifica.Item(key) = 0
                Next
#End Region

#Region "classifica"
            ElseIf message.Text.ToLower.StartsWith("/classifica") Then
                'mostra classifica
                Dim reply As New StringBuilder
                Dim i As Integer = 1
                Dim sortedList = From pair In classifica
                                 Order By pair.Value Descending

                Dim params() As String = message.Text.Split(" ")
                params.RemoveAt(0)
                Dim params_list As New List(Of String)
                Dim trovato As Boolean = True
                If params.Length > 0 Then
                    Try
                        params_list.Add(membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = params.Last.ToLower()).First)
                    Catch ex As Exception
                        trovato = False
                        Console.WriteLine("nessun membro con quel nome")
                    End Try
                End If
                'invio tutta la classifica
                If Not trovato Then reply.AppendLine("Utente non trovato, mostro classifica generale").AppendLine()
                For Each pair In sortedList
                    If params_list.Count = 0 Then
                        'nessun parametro, aggiungo tutti

                        reply.AppendLine(i & "° " & membri.Item(pair.Key) & ": " & classifica.Item(pair.Key))
                    Else
                        If params_list.Contains(membri.Item(pair.Key)) Then
                            reply.AppendLine(i & "° " & membri.Item(pair.Key) & ": " & classifica.Item(pair.Key))

                        End If
                    End If
                    i += 1
                Next
                api.SendTextMessage(message.Chat.Id, reply.ToString,, message.MessageId)

            End If
#End Region

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

    Function modifica_punti(punti As Integer, message As Message, nome As String) As String
        Dim action As String = " guadagna "
        Dim reply As String = "Membro non trovato"
        If punti < 0 Then action = " perde "
        If message.ReplyToMessage IsNot Nothing Then
            If classifica.ContainsKey(message.ReplyToMessage.From.Id) Then
                classifica.Item(message.ReplyToMessage.From.Id) += punti
                Return message.ReplyToMessage.From.FirstName & action & Math.Abs(punti) & " punti!"
            End If
        Else
            If membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = nome.ToLower()).Count() > 0 Then
                Dim membro As ULong
                For Each record As KeyValuePair(Of ULong, String) In membri
                    If record.Value.ToLower = nome.ToLower Then membro = record.Key
                Next
                If membro <> 0 Then
                    classifica.Item(membro) += punti
                    Return nome & action & Math.Abs(punti) & " punti!"
                End If
            End If
        End If
        Return reply
    End Function

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
