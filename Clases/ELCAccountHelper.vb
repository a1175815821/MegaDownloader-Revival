Imports System.Security

Public Class ELCAccountHelper
    Implements IDisposable

    Private _AccountList As Generic.List(Of Account)

    Public Class Account
        Public User As SecureString
        Public Key As SecureString
        Public [Alias] As String
        Public URL As String
        Public DefaultAccount As Boolean
    End Class

    Public Sub New(ByRef Config As Configuracion)
        Me._AccountList = New Generic.List(Of Account)
        For Each c As Account In Config.ELCAccounts
            Dim newAccount As New Account
            newAccount.User = c.User.Copy
            newAccount.Key = c.Key.Copy
            newAccount.Alias = c.Alias
            newAccount.URL = c.URL
            newAccount.DefaultAccount = c.DefaultAccount
            Me._AccountList.Add(newAccount)
        Next
    End Sub


    Public Function GetAccounts() As Generic.List(Of Account)
        Return Me._AccountList
    End Function

    Public Function GetDefaultAccount() As Account
        Return GetAccounts.FirstOrDefault(Function(c As Account) c.DefaultAccount)
    End Function


    Public Function GetAccountDetailsByURL(ByVal URL As String) As Account
        Return GetAccounts.FirstOrDefault(Function(c As Account) c.URL.ToLower.Trim = URL.ToLower.Trim)
    End Function
    Public Function GetAccountDetailsByAlias(ByVal [Alias] As String) As Account
        Return GetAccounts.FirstOrDefault(Function(c As Account) c.Alias.ToLower.Trim = [Alias].ToLower.Trim)
    End Function

    Public Function ModifyAccountDetails(ByVal URLPreviousAccount As String, ByVal NewAccount As Account) As Boolean
        Dim c As Account = GetAccountDetailsByURL(URLPreviousAccount)
        If c Is Nothing Then Return False

        ' Check that the new email is not used by another account
        Dim c2 As Account = GetAccountDetailsByURL(NewAccount.URL)
        If c2 IsNot Nothing AndAlso Not c2.Equals(c) Then Return False
        ' Check that the new alias is not used by another account
        c2 = GetAccountDetailsByAlias(NewAccount.Alias)
        If c2 IsNot Nothing AndAlso Not c2.Equals(c) Then Return False


        c.User.Dispose()
        c.Key.Dispose()
        c.URL = NewAccount.URL
        c.Alias = NewAccount.Alias
        c.User = NewAccount.User
        c.Key = NewAccount.Key
        c.DefaultAccount = NewAccount.DefaultAccount
        ReAssignDefault(c)
        Return True
    End Function

    Public Function AddNewAccount(ByVal NewAccount As Account) As Boolean
        Dim c As Account

        ' Check alias and email
        c = GetAccountDetailsByAlias(NewAccount.Alias)
        If c IsNot Nothing Then Return False
        c = GetAccountDetailsByURL(NewAccount.URL)
        If c IsNot Nothing Then Return False

        _AccountList.Add(NewAccount)
        ReAssignDefault(NewAccount)
        Return True
    End Function

    Public Function DeleteAccount(ByVal URL As String) As Boolean
        Dim c As Account = GetAccountDetailsByURL(URL)
        If c Is Nothing Then Return False
        DeleteAccount = _AccountList.Remove(c)
        c.User.Dispose()
        c.Key.Dispose()
        ReAssignDefault(Nothing)
    End Function

    Private Sub ReAssignDefault(ByRef c As Account)
        If c IsNot Nothing AndAlso c.DefaultAccount Then
            For Each a As Account In Me._AccountList
                If Not c.Equals(a) Then a.DefaultAccount = False
            Next
        Else
            Dim ThereIsDefault As Boolean = False
            For Each a As Account In Me._AccountList
                If a.DefaultAccount Then ThereIsDefault = True
            Next
            If Not ThereIsDefault Then
                Dim l = From a As Account In Me._AccountList Order By a.Alias
                If l.Count > 0 Then
                    l(0).DefaultAccount = True
                End If
            End If
        End If
    End Sub

    Public Sub SaveToConfig(ByRef Config As Configuracion)
        For Each c As Account In Config.ELCAccounts
            c.User.Dispose()
            c.Key.Dispose()
        Next
        Config.ELCAccounts.Clear()

        For Each c As Account In _AccountList
            Dim newAccount As New Account
            newAccount.User = c.User.Copy
            newAccount.Key = c.Key.Copy
            newAccount.DefaultAccount = c.DefaultAccount
            newAccount.URL = c.URL
            newAccount.Alias = c.Alias
            Config.ELCAccounts.Add(newAccount)
        Next
    End Sub
    
    Public function ImportConfig(ByVal ConfigList As Generic.List(Of String), byref MainForm As Main) As Boolean
    	If ConfigList Is Nothing OrElse ConfigList.Count = 0 Then Return False
    	
    	Dim TempAccount As New Generic.List(Of Account)
    	Dim Description As String = ""
    	
    	For Each Conf As String In ConfigList
    		Dim conf2 As String = Conf.Replace("\:", "{TEMP_PUNTOS}")
    		' url : nick : apikey (: alias)
    		If conf2.Split(":"c).Length = 3 Or conf2.Split(":"c).Length = 4 Then
    			Dim index As Integer = 0
    			
    			Dim URL As String = ""
    			Dim Nick As String = ""
    			Dim ApiKey As String = ""
    			Dim [Alias] as String = ""
    			For Each token As String In conf2.Split(":"c)
    				Dim t As String = System.Web.HttpUtility.UrlDecode(token.Replace("{TEMP_PUNTOS}", ":"))
    				Select Case index
    					Case 0
    						URL = t
    					Case 1
    						Nick = t
    					Case 2
    						ApiKey = t
    					Case 3
    						[Alias] = t
    				End Select
    				index += 1
    			Next
    			Dim A As New Account
    			A.URL = URL
    			A.User = Criptografia.ToSecureString(Nick)
    			A.Key = Criptografia.ToSecureString(ApiKey)
    			A.Alias = [Alias]
    			If String.IsNullOrEmpty(A.Alias) Then A.Alias = URL
    			TempAccount.Add(A)
    			If Description.Length > 0 Then Description &= ", "
    			Description &= URL
    		End If
    	Next
    	
    	If TempAccount.Count = 0 Then Return False
    	
    	MainForm.Activate
        If MessageBox.Show(Language.GetText("Do you want to import the configuration for the following ELC accounts?") & vbNewLine & vbNewLine & Description, _
                           Language.GetText("Confirmation"), MessageBoxButtons.YesNo) = DialogResult.No Then
            Return False
        End If
    	
    	For Each Acc As Account In TempAccount
    		If Not ModifyAccountDetails(Acc.URL, Acc) Then
    			AddNewAccount(Acc)
    		End If
    	Next
    	
    	MessageBox.Show(Language.GetText("Data saved successfully"), Language.GetText("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    	
    	Return True
    End Function


#Region "IDisposable Support"
    Private disposedValue As Boolean ' To detect redundant calls

    ' IDisposable
    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not Me.disposedValue Then
            If disposing Then
                
            End If

            For Each c As Account In _AccountList
                c.User.Dispose()
                c.Key.Dispose()
            Next
            _AccountList.Clear()
        End If
        Me.disposedValue = True
    End Sub



    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class
