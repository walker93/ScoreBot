Imports Telegram.Bot
Imports Telegram.Bot.Types

Module Module1
    Dim WithEvents api As Api
    Sub Main()
        api = New Api("217500308:AAG_EiUHU4pOLUi74jxDn-YUbuEQCbcWXJ8")
        Console.WriteLine(api.GetMe.Result.Username)
        api.StartReceiving()
        Console.WriteLine("bot attivo")
        Console.ReadKey()
    End Sub

    Private Sub api_InlineQueryReceived(sender As Object, e As InlineQueryEventArgs) Handles api.InlineQueryReceived
        Dim query As InlineQuery = e.InlineQuery
        Console.WriteLine(Now.ToShortDateString + " " + query.Id + ": " + query.Query)
        Dim results() As InlineQueryResult
        Dim res As New InlineQueryResult()
        res.Id = 0
        res.Title = "+" + query.Query
        res.MessageText = res.Title + "per l'utente"
    End Sub

End Module
