Imports System.Text
Imports Telegram.Bot
Imports Telegram.Bot.Types

Module Module1
    Dim WithEvents api As Api
    Dim flush As Boolean = False
    Dim classifica As New Dictionary(Of ULong, Integer)
    Dim membri As New Dictionary(Of ULong, String)
    Dim utenti As New List(Of Integer)
    Dim time_start As Date = Date.UtcNow
    Dim query_points() As Integer = {100, 50, 20, 10, 0, -10, -20, -50, -100}
    Sub Main(ByVal args() As String)
        api = New Api(token)
        Dim bot = api.GetMe.Result
        Console.WriteLine(bot.Username & ": " & bot.Id)
        load_admins()
        carica()
        If args.Length > 0 Then flush = args(0).Contains("flush")
        Dim thread As New Threading.Thread(New Threading.ThreadStart(AddressOf run))
        thread.Start()
    End Sub

    Sub run()
        Dim updates() As Update
        Dim offset As Integer = 0
        While True
            Try
                updates = api.GetUpdates(offset,, 20).Result
                For Each up As Update In updates
                    Select Case up.Type
                        Case UpdateType.MessageUpdate
                            'process_Message(up.Message)
                        Case UpdateType.InlineQueryUpdate
                            process_query(up.InlineQuery)
                        Case UpdateType.ChosenInlineResultUpdate
                            process_ChosenQuery(up.ChosenInlineResult)
                    End Select
                    offset = up.Id + 1
                Next
            Catch ex As AggregateException
                Threading.Thread.Sleep(20 * 1000)
                Console.WriteLine("{0} {1} Error: {2}", Now.ToShortDateString, Now.ToShortTimeString, ex.InnerException.Message)
            End Try
        End While
        'api.StartReceiving()
        'Console.WriteLine("bot attivo")
        'Console.ReadKey()
        'api.StopReceiving()
    End Sub

    Sub process_ChosenQuery(chosenquery As ChosenInlineResult)
        Dim resultid = chosenquery.ResultId
        Dim punti As Integer
        Dim query_reason As String = If(chosenquery.Query.Split(":").Length > 1, chosenquery.Query.Split(":")(1).Trim, "")
        Dim query_punti As String = If(chosenquery.Query.Split(":").Length > 1, chosenquery.Query.Split(":")(0).Trim, chosenquery.Query)
        'Integer.TryParse(resultid, punti)
        If resultid = "Aggiunta" Then
            If Not membri.ContainsKey(chosenquery.From.Id) Then
                membri.Add(chosenquery.From.Id, chosenquery.From.FirstName)
                classifica.Add(chosenquery.From.Id, 0)
                Console.WriteLine(chosenquery.From.FirstName & " aggiunto")
            End If
        ElseIf resultid = "Rimozione" Then
            If membri.ContainsKey(chosenquery.From.Id) Then
                membri.Remove(chosenquery.From.Id)
                classifica.Remove(chosenquery.From.Id)
                Console.WriteLine(chosenquery.From.FirstName & " rimosso")
            End If
        ElseIf resultid = "Reset" Then
            If admins.Contains(chosenquery.From.Id) Then
                Dim keys() = classifica.Keys.ToArray
                For Each key In keys
                    classifica.Item(key) = 0
                Next
                Console.WriteLine("Classifica resettata")
            End If
        ElseIf resultid = "class tot" Then
            Console.WriteLine("Classifica completa inviata")
        ElseIf resultid.Contains("classifica") Then
            Console.WriteLine("Punteggio membro inviato")
        ElseIf query_points.Contains(resultid) Then
            'l'id è un punteggio, aggiorno i punti
            Integer.TryParse(resultid, punti)
            Dim member As String = query_punti
            modifica_punti_noreply(punti, member)
            Console.WriteLine(member & " guadagna " & punti)
        ElseIf chosenquery.Query.StartsWith("promuovi") Then
            admins.Add(resultid)
            save_admins()
            Console.WriteLine(resultid & " ora è Admin!")
        ElseIf chosenquery.Query.StartsWith("retrocedi") Then
            admins.Remove(resultid)
            save_admins()
            Console.WriteLine(resultid & " ora è utente!")
        ElseIf classifica.ContainsKey(resultid) Then
            If chosenquery.Query.ToLower = "classifica" Then Exit Sub
            Integer.TryParse(query_punti, punti)
            'l'id è un membro, aggiorno i punti
            modifica_punti_noreply(punti, membri.Item(resultid))
            Console.WriteLine(membri.Item(resultid) & " guadagna " & punti)
        End If
        salva()
    End Sub

    Private Sub api_InlineQueryReceived(sender As Object, e As InlineQueryEventArgs) Handles api.InlineQueryReceived
        process_query(e.InlineQuery)
    End Sub

    Sub process_query(Query As InlineQuery)
        Console.WriteLine("{0} {1} From: {2}-{3} ID:{4} TEXT: {5}", Now.ToShortDateString, Now.ToShortTimeString, Query.From.Id, Query.From.FirstName, Query.Id, Query.Query)
        Dim results As New List(Of InlineQueryResult)
        Dim i As Integer = 1
        Dim classificaBuilder As New StringBuilder
        Dim res As InlineQueryResultArticle
        Dim punti As Integer
        Dim params_list As New List(Of String)
        Dim trovato As Boolean = True
        Dim query_reason As String = If(Query.Query.Split(":").Length > 1, Query.Query.Split(":")(1).Trim, "")
        Dim query_punti As String = If(Query.Query.Split(":").Length > 1, Query.Query.Split(":")(0).Trim, Query.Query)
        Dim sortedList = From pair In classifica
                         Order By pair.Value Descending

        If Not utenti.Contains(Query.From.Id) Then
            IO.File.AppendAllText("users.txt", Query.From.Id & vbNewLine)
            utenti.Add(Query.From.Id)
        End If
        If "classifica".Contains(Query.Query.ToLower) Or Query.Query = "" Then
            If membri.ContainsKey(Query.From.Id) Then
                If Not membri.Item(Query.From.Id) = Query.From.FirstName Then
                    membri.Item(Query.From.Id) = Query.From.FirstName
                    Console.WriteLine("Updating member name from {0} to {1}", membri.Item(Query.From.Id), Query.From.FirstName)
                End If
            End If
            For Each member As KeyValuePair(Of ULong, Integer) In sortedList
                res = New InlineQueryResultArticle
                res.Id = "classifica" + i.ToString
                res.MessageText = i & "° " & membri.Item(member.Key) & ": " & classifica.Item(member.Key) & " punti"
                classificaBuilder.AppendLine(res.MessageText)
                res.Title = res.MessageText
                i += 1
                results.Add(res)
            Next
            res = New InlineQueryResultArticle
            res.Id = "class tot"
            res.MessageText = classificaBuilder.ToString
            res.Title = "Tutta la classifica"
            results.Insert(0, res)

        ElseIf "aggiungi".Contains(Query.Query.ToLower) Then
            If Not membri.ContainsKey(Query.From.Id) Then
                res = New InlineQueryResultArticle
                res.Id = "Aggiunta"
                res.MessageText = Query.From.FirstName & " si è aggiunto a Score Bot"
                res.Title = "Aggiungimi a Score Bot"
                results.Add(res)
            End If
        ElseIf "rimuovi".Contains(Query.Query.ToLower) Then
            If membri.ContainsKey(Query.From.Id) Then
                res = New InlineQueryResultArticle
                res.Id = "Rimozione"
                res.MessageText = Query.From.FirstName & " si è rimosso da Score Bot"
                res.Title = "Rimuovimi da Score Bot"
                results.Add(res)
            End If
        ElseIf admins.Contains(Query.From.Id) AndAlso "reset".Contains(Query.Query.ToLower) Then
            res = New InlineQueryResultArticle
            res.Id = "Reset"
            res.MessageText = "La classifica è stata azzerata"
            res.Title = "Azzera i punteggi"
            results.Add(res)
        ElseIf admins.Contains(Query.From.Id) AndAlso Integer.TryParse(query_punti, punti) Then
            'invio "Aggiungi <punti> a membro1/2/3"
            Dim action As String = If(punti < 0, " perde ", " guadagna ")
            Dim query_action As String = If(punti < 0, " Togli ", " Aggiungi ")
            For Each member As KeyValuePair(Of ULong, String) In membri
                res = New InlineQueryResultArticle
                res.Id = member.Key
                res.MessageText = membri.Item(member.Key) & action & Math.Abs(punti) & " punti"
                If Not query_reason = "" Then
                    res.MessageText += " perché " + query_reason
                    res.Description = "perchè " + query_reason
                End If
                'classificaBuilder.AppendLine(res.MessageText)
                res.Title = query_action & Math.Abs(punti) & " a " & membri.Item(member.Key)
                i += 1
                results.Add(res)
            Next
        ElseIf admins.Contains(Query.From.Id) AndAlso is_member(query_punti) Then
            'invio "aggiungi 5/10/20/50 punti a <membro>"
            Try
                params_list.Add(membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = query_punti.ToLower()).First)
            Catch ex As Exception
                trovato = False
                Console.WriteLine("nessun membro con quel nome")
            End Try

            For Each point As Integer In query_points
                For Each membro In params_list
                    Dim action As String = If(point < 0, " perde ", " guadagna ")
                    Dim query_action As String = If(point < 0, " Togli ", " Aggiungi ")
                    res = New InlineQueryResultArticle
                    res.Id = point.ToString
                    res.MessageText = membro & action & Math.Abs(point) & " punti"
                    If Not query_reason = "" Then
                        res.MessageText += " perché " + query_reason
                        res.Description = "perchè " + query_reason
                    End If
                    res.Title = query_action & Math.Abs(point) & " a " & membro
                    results.Add(res)
                Next
            Next
        ElseIf Query.From.Id = 1265775 AndAlso Query.Query.StartsWith("promuovi") Then
            'Promozione Admin
            If Query.Query.Split(" ").Length > 1 Then
                Dim id As String = Query.Query.Split(" ")(1)
                res = New InlineQueryResultArticle
                res.Id = id
                res.MessageText = id & " ora è admin!"
                res.Title = "Promuovi " & id & " ad admin"
                results.Add(res)
            End If
            For Each user In membri
                res = New InlineQueryResultArticle
                res.Id = user.Key
                res.MessageText = user.Value & " ora è admin!"
                res.Title = "Promuovi " & user.Value & " ad admin"
                results.Add(res)
            Next
        ElseIf Query.From.Id = 1265775 AndAlso Query.Query.StartsWith("retrocedi") Then
            'retrocessione admin
            For Each admin In admins
                res = New InlineQueryResultArticle
                res.Id = admin
                res.MessageText = admin & "ora non è più admin!"
                res.Title = "Retrocedi " & admin & " ad utente"
                results.Add(res)
            Next
        End If

        If results.Count > 0 Then api.AnswerInlineQuery(Query.Id, results.ToArray, 1, True)
    End Sub

    Private Sub api_MessageReceived(sender As Object, e As MessageEventArgs) Handles api.MessageReceived
        Dim message As Message = e.Message
        If admins.Contains(message.From.Id) Then process_Message(message)
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
            If message.Text.ToLower.StartsWith("/aggiungi") Then
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
            ElseIf message.Text.ToLower.StartsWith("/togli") Then
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
            ElseIf message.Text.ToLower.StartsWith("/reset") Then
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

            'Else
            'non è un messaggio di testo, ma di servizio
            'If message.NewChatParticipant IsNot Nothing Then
            '    'nuovo membro
            '    Dim membro = message.NewChatParticipant
            '    membri.Add(membro.Id, membro.FirstName)
            '    classifica.Add(membro.Id, 0)
            'ElseIf message.LeftChatParticipant IsNot Nothing Then
            '    'uscito membro
            '    Dim membro = message.LeftChatParticipant
            '    membri.Remove(membro.Id)
            '    classifica.Remove(membro.Id)
            'End If
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

        'legge da file users e li inserisce nella lista
        Dim file_users As String = "users.txt"
        If Not IO.File.Exists(file_users) Then IO.File.WriteAllText(file_users, "")
        For Each line As String In IO.File.ReadAllLines(file_users)
            utenti.Add(line)
        Next
    End Sub

    Function modifica_punti(punti As Integer, message As Message, nome As String) As String
        Dim action As String = If(punti < 0, " perde ", " guadagna ")
        Dim reply As String = "Membro non trovato"
        If message.ReplyToMessage IsNot Nothing Then
            If classifica.ContainsKey(message.ReplyToMessage.From.Id) Then
                classifica.Item(message.ReplyToMessage.From.Id) += punti
                Return message.ReplyToMessage.From.FirstName & action & Math.Abs(punti) & " punti!"
            End If
        Else
            Return modifica_punti_noreply(punti, nome)
        End If
        Return reply
    End Function

    Function modifica_punti_noreply(punti As Integer, nome As String) As String
        Dim action As String = If(punti < 0, " perde ", " guadagna ")
        If is_member(nome) Then
            Dim membro As ULong
            For Each record As KeyValuePair(Of ULong, String) In membri
                If record.Value.ToLower = nome.ToLower Then membro = record.Key
            Next
            If membro <> 0 Then
                classifica.Item(membro) += punti
                Return nome & action & Math.Abs(punti) & " punti!"
            End If
        End If
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

    Function is_member(member_name) As Boolean
        If membri.Select(Function(x) x.Value).Where(Function(x) x.ToLower() = member_name.tolower()).Count > 0 Then Return True
        Return False
    End Function
End Module
