Imports System.Text.RegularExpressions

Public Class URLExtractor

    ' Esta clase debe ser igual en MegaDownloader y MegaUploader!!

    'Private Shared ENC_XOR As Byte() = New Byte() {-} ' REMOVED FROM SOURCE CODE, SORRY
    Private Shared ENC_XOR As Byte() = New Byte() {12, 57, 251, 120, 18, 75, 6, 250, 85} ' REMOVED FROM SOURCE CODE, SORRY

    Public Const ENCODE_PASSWORD As String = "k1o6Al-1kz¿!z05y"
    Public Const ENCODE_PASSWORD2 As String = "nYrXa@9Q¿1&hCWM\9(731Bp?t42=!k3."

    Public Const MEGASEARCHPREFIX As String = "mega-search?"
    Public Const ENCODEDPREFIX As String = "enc?"
    Public Const ENCODEDPREFIX2 As String = "enc2?"
    Public Const FOLDERENCODEDPREFIX As String = "fenc?"
    Public Const FOLDERENCODEDPREFIX2 As String = "fenc2?"

    Public Const SERVERENCODEDPREFIX As String = "elc?"

    Private Shared ReadOnly patternHTTPURI() As String = _
        {"(http://|https://|)mega.co.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!]+)(!(?<FileKey>[\w-#=]+))?", _
         "(http://|https://|)mega.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!]+)(!(?<FileKey>[\w-#=]+))?", _
        "(http://|https://|)mega.nz/file/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?", _
        "(http://|https://|)mega.nz/folder/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?", _
        "(http://|https://|)mega.co.nz/file/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?", _
        "(http://|https://|)mega.co.nz/folder/(?<FileID>[^#/]+)(#(?<FileKey>[\w-]+))?"}

    Private Shared ReadOnly patternMEGAURI() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<MODE2>#|#F|F|#N|N)!(?<FileID>[^\!]+)(!(?<FileKey>[\w-#=]+))?", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<MEGASEARCH>mega-search(\?|/\?))(?<MEGASEARCH_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc2(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc2(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)"}

    Private Shared ReadOnly patternELCUri() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>elc(\?|/\?))(?<ENCODED_FILEID>[\w-]+)"}

    Private Shared Function patternGetInfoURL() As String()
        Return patternHTTPURI.Concat(patternMEGAURI).Concat(patternELCUri).ToArray
    End Function


    Private Shared ReadOnly patternElcConfig() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>configelc(\?|/\?))(?<ENCODED_CONFIG>[^\s]+)"}


    ''' <summary>
    ''' Checks if the URL is a MEGA folder
    ''' </summary>
    ''' <param name="URI"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Shared Function IsMegaFolder(ByVal URI As String) As Boolean
        Dim Result As Boolean = False
        If String.IsNullOrEmpty(URI) Then Return False
        Result = URI.Contains("mega.co.nz/#F!") Or URI.Contains("mega.nz/#F!") Or URI.Contains("mega.nz/folder/") Or URI.Contains("mega.co.nz/folder/") ' Enlaces de mega

        If Not Result Then ' Enlaces mega sin codificar
            For Each pattern As String In patternGetInfoURL()
                Dim regex = New System.Text.RegularExpressions.Regex(pattern)
                If regex.IsMatch(URI) Then
                    Dim match = regex.Match(URI)

                    If Not String.IsNullOrEmpty(match.Groups("MODE2").Value) Then
                        ' Enlace mega: sin codificar
                        Select Case match.Groups("MODE2").Value
                            Case "#F", "F"
                                Result = True
                                Exit For
                        End Select

                    ElseIf Not String.IsNullOrEmpty(match.Groups("BASIC_ENCODE").Value) AndAlso match.Groups("BASIC_ENCODE").Value.StartsWith("fenc") Then
                        ' Enlace codificado fenc
                        Result = True
                        Exit For

                    End If
                End If
            Next
        End If
        Return Result
    End Function


    ''' <summary>
    ''' Checks if the URL is an ELC
    ''' </summary>
    ''' <param name="URI"></param>
    ''' <returns></returns>
    Friend Shared Function IsELC(ByVal URI As String) As Boolean
        For Each pattern As String In patternELCUri
            Dim regex = New System.Text.RegularExpressions.Regex(pattern)
            If regex.IsMatch(URI) Then Return True
        Next
        Return False
    End Function


    Public Shared Function ExtraerSoloURLsOficiales(ByVal Texto As String) As Generic.List(Of String)
        Dim links As New Generic.HashSet(Of String) ' Evitamos repetidos

        If Texto Is Nothing Then Return links.ToList

        For Each pattern As String In patternHTTPURI
            Dim regx As New Regex(pattern, RegexOptions.IgnoreCase)

            ' 1) Detect ONLY valid MEGA http links 
            ' Examples:
            ' https://mega.co.nz/#!abcdef!ghijklmnopqr
            ' https://mega.co.nz/#!123456!789123456789

            Dim matches As MatchCollection = regx.Matches(Texto)

            For Each match As Match In matches
                Dim url As String = match.Value.Trim

                Dim fileid As String = ExtraerFileID(url)
                If Not String.IsNullOrEmpty(fileid) Then
                    links.Add(url)
                End If
            Next
        Next


        Return links.ToList
    End Function


    ' mega://configelc?http%3A%2F%2Ftest.com%2Felc%3Fa%3D1%26b%3D2:User%20Name:Api%20Key:Account%20Alias
    Public Shared Function ExtraerConfiguracionELC(ByVal Texto As String) As Generic.List(Of String)
        Dim conf As New Generic.HashSet(Of String) ' Evitamos repetidos

        If String.IsNullOrEmpty(Texto) Then Return conf.ToList

        Dim regx As Regex
        Dim matches As MatchCollection

        For Each pattern As String In patternElcConfig
            regx = New Regex(pattern, RegexOptions.IgnoreCase)

            matches = regx.Matches(Texto)

            For Each match As Match In matches
                Dim config As String = match.Groups("ENCODED_CONFIG").Value
                ' url : nick : apikey (: alias)
                If Not String.IsNullOrEmpty(config) Then
                    Dim config2 As String = config.Replace("\:", "{TEMP_PUNTOS}")
                    If config2.Split(":"c).Length = 3 Or config2.Split(":"c).Length = 4 Then
                        conf.Add(config)
                    End If
                End If
            Next
        Next
        Return conf.ToList
    End Function

    Public Shared Function ExtraerURLs(ByVal Texto As String) As Generic.List(Of String)
        Dim links As New Generic.HashSet(Of String) ' Evitamos repetidos

        If String.IsNullOrEmpty(Texto) Then Return links.ToList


        ' 1) Detect ONLY valid MEGA http links 
        ' Examples:
        ' https://mega.co.nz/#!abcdef!ghijklmnopqr
        ' https://mega.co.nz/#!123456!789123456789


        'Dim regx As New Regex("(http|https)://([\w+?\.\w+])+([a-zA-Z0-9\~\!\@\#\$\%\^\&amp;\*\(\)_\-\=\+\\\/\?\.\:\;\'\,]*)?", RegexOptions.IgnoreCase)
        Dim regx As Regex
        Dim matches As MatchCollection

        For Each pattern As String In patternHTTPURI
            regx = New Regex(pattern, RegexOptions.IgnoreCase)

            matches = regx.Matches(Texto)

            For Each match As Match In matches
                Dim url As String = match.Value.Trim

                Dim fileid As String = ExtraerFileID(url)
                If Not String.IsNullOrEmpty(fileid) Then
                    links.Add(url)
                ElseIf EsUrlAcortador(url) Then
                    url = Conexion.ObtenerUrlDesdeAcortador(url)
                    fileid = ExtraerFileID(url)
                    If Not String.IsNullOrEmpty(fileid) Then
                        links.Add(url)
                    End If
                End If
            Next
        Next


        ' 2) Detect MEGA uri
        ' Examples:
        ' mega:!ABC!12345678900
        ' mega:#!ABC!12345678900
        ' mega:#F!ABC!12345678900
        ' mega://#!123!456789ABC
        ' mega://#F!123!456789ABC
        ' mega://https://mega.co.nz/#!abcdef!ghijklmnopqr
        ' mega://mega-search?tn
        ' mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
        ' mega://senc?_xlPqemSILarh5VBKbhSTFyQQQ0
        regx = New Regex("(?<TAG>mega)(?<MODE>://|:///|:)(?<DATA>[\w-/#!:.?]+)", RegexOptions.IgnoreCase)
        matches = regx.Matches(Texto)
        For Each match As Match In matches

            Dim data As String = match.Groups("DATA").Value
            Dim value As String = match.Value.Trim


            If Not String.IsNullOrEmpty(data) AndAlso data.Length > 5 Then

                If data.ToLower.StartsWith(MEGASEARCHPREFIX) Or _
                    data.ToLower.StartsWith(ENCODEDPREFIX) Or _
                    data.ToLower.StartsWith(FOLDERENCODEDPREFIX) Or _
                    data.ToLower.StartsWith(ENCODEDPREFIX2) Or _
                    data.ToLower.StartsWith(FOLDERENCODEDPREFIX2) Or _
                    data.ToLower.StartsWith(SERVERENCODEDPREFIX) Then
                    links.Add(value)
                    Continue For
                End If


                Dim fileid As String = ExtraerFileID(value)
                If Not String.IsNullOrEmpty(fileid) Then
                    links.Add(value)
                    Continue For
                End If
                fileid = ExtraerFileID(data)
                If Not String.IsNullOrEmpty(fileid) Then
                    links.Add(value)
                    Continue For
                End If

            End If
        Next

        Return links.ToList
    End Function



    Public Shared Function EsUrlAcortador(ByVal URL As String) As Boolean
        ' goo.gl 短链服务已于 2019 年 3 月停止解析,已无支持的短链服务
        Return False
    End Function


    Public Shared Sub CheckFileIDAndFileKey(ByRef FileID As String, ByRef FileKey As String)

        If Not String.IsNullOrEmpty(FileID) Then FileID = FileID.Replace("/?", "?") ' A veces firefox mete un /? en vez de un ? :/

        ' Mega-search ID
        If String.IsNullOrEmpty(FileKey) And Not String.IsNullOrEmpty(FileID) AndAlso FileID.StartsWith(MEGASEARCHPREFIX) Then
            Dim URL As String = Conexion.ObtenerUrlDesdeAcortador( _
                InternalConfiguration.ObtenerValueFromInternalConfig("MEGA_SEARCH_CURL") & _
                FileID.Substring(FileID.IndexOf(MEGASEARCHPREFIX) + MEGASEARCHPREFIX.Length))

            Dim F As String = ExtraerFileID(URL)
            Dim K As String = ExtraerFileKey(URL)
            If Not String.IsNullOrEmpty(F) Then
                FileID = F
                FileKey = K
            End If
        ElseIf String.IsNullOrEmpty(FileKey) And Not String.IsNullOrEmpty(FileID) _
                AndAlso (FileID.StartsWith(ENCODEDPREFIX) Or FileID.StartsWith(FOLDERENCODEDPREFIX) Or FileID.StartsWith(ENCODEDPREFIX2) Or FileID.StartsWith(FOLDERENCODEDPREFIX2)) Then


            ' Encoded string
            ' Generate it: GenerateEncodedURILink
            Dim encodedStr As String
            If FileID.StartsWith(FOLDERENCODEDPREFIX2) Then
                encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX2) + FOLDERENCODEDPREFIX2.Length)
            ElseIf FileID.StartsWith(FOLDERENCODEDPREFIX) Then
                encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX) + FOLDERENCODEDPREFIX.Length)
            ElseIf FileID.StartsWith(ENCODEDPREFIX2) Then
                encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX2) + ENCODEDPREFIX2.Length)
            Else
                encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX) + ENCODEDPREFIX.Length)
            End If

            encodedStr &= "==".Substring((2 - encodedStr.Length * 3) And 3)
            encodedStr = encodedStr.Replace("-", "+").Replace("_", "/").Replace(",", "")

            Dim isFolder As Boolean = FileID.StartsWith(FOLDERENCODEDPREFIX) Or FileID.StartsWith(FOLDERENCODEDPREFIX2)

            Dim link As String = If(isFolder, "F", "") & Criptografia.AES_DecryptString(encodedStr, ENCODE_PASSWORD)
            If Not link.StartsWith("mega:") Then
                link = "mega://#" & link
            End If

            Dim F As String = ExtraerFileID(link)
            Dim K As String = ExtraerFileKey(link)
            If Not String.IsNullOrEmpty(F) And Not HasNonPrintableCharacters(F) Then
                FileID = F
                FileKey = K
            Else 'Encode V2??
                link = If(isFolder, "F", "") & Criptografia.AES_DecryptString(encodedStr, getENC2Bytes, System.Text.Encoding.ASCII)
                If Not link.StartsWith("mega:") Then
                    link = "mega://#" & link
                End If
                F = ExtraerFileID(link)
                K = ExtraerFileKey(link)
                If Not String.IsNullOrEmpty(F) Then
                    FileID = F
                    FileKey = K
                End If
            End If

        End If
    End Sub

    Private Shared Function HasNonPrintableCharacters(F As String) As Boolean
        Dim nonvalidchars As Generic.List(Of Char) = System.IO.Path.GetInvalidFileNameChars.ToList
        nonvalidchars.Remove("?"c)
        For Each c In F
            If nonvalidchars.Contains(c) Then Return True
        Next
        Return False
    End Function

    Private Shared Function getENC2Bytes() As Byte()
        Dim intLength As Integer
        Dim intRemaining As Integer
        Dim bytDecryptionKey() As Byte

        Dim temp As String = ENCODE_PASSWORD2

        intLength = Len(temp)

        If intLength >= 32 Then
            temp = Strings.Left(temp, 32)
        Else
            intLength = Len(temp)
            intRemaining = 32 - intLength
            temp = temp & Strings.StrDup(intRemaining, "X")
        End If

        bytDecryptionKey = System.Text.Encoding.ASCII.GetBytes(temp.ToCharArray)

        For i As Integer = 0 To 159
            bytDecryptionKey(i Mod bytDecryptionKey.Length) = bytDecryptionKey(i Mod bytDecryptionKey.Length) Xor ENC_XOR(i Mod ENC_XOR.Length)
        Next

        Return bytDecryptionKey
    End Function



    Public Shared Function GenerateEncodedURILink(ByVal FileID As String, ByVal FileKey As String, ByVal MegaFolder As Boolean, ByVal Compatibility As Boolean) As String
        ' !ABC!12345678900

        Dim link As String = "!" & FileID & "!" & FileKey
        Dim str As String = Nothing
        If Compatibility Then
            str = Criptografia.AES_EncryptString(link, ENCODE_PASSWORD)
        Else
            str = Criptografia.AES_EncryptString(link, getENC2Bytes, System.Text.Encoding.ASCII)
        End If

        str = str.Replace("+", "-").Replace("/", "_").Replace("=", "")

        Return "mega://" & If(MegaFolder, FOLDERENCODEDPREFIX2, ENCODEDPREFIX2) & str
    End Function


#Region "Extract FileID/FileKey"

    Public Shared Function ExtraerFileID(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""

        URL = URL.Replace("%21", "!") ' Algunos links estan sin el ! :/
        URL = URL.Replace("%20", "") ' Algunos links tienen espacios en medio :/

        For Each pattern As String In patternGetInfoURL()
            Dim regex = New System.Text.RegularExpressions.Regex(pattern)
            If regex.IsMatch(URL) Then
                Dim match = regex.Match(URL)

                If Not String.IsNullOrEmpty(match.Groups("MEGASEARCH_FILEID").Value) Then
                    Return match.Groups("MEGASEARCH").Value & match.Groups("MEGASEARCH_FILEID").Value.Trim("/"c)
                ElseIf Not String.IsNullOrEmpty(match.Groups("ENCODED_FILEID").Value) Then
                    Return match.Groups("BASIC_ENCODE").Value & match.Groups("ENCODED_FILEID").Value.Trim("/"c)
                Else
                    Dim fileID = match.Groups("FileID").Value & ""

                    If Not String.IsNullOrEmpty(match.Groups("MODE2").Value) Then ' private file from folder
                        Select Case match.Groups("MODE2").Value
                            Case "#N", "N"
                                fileID = "N?" & fileID
                        End Select
                    End If

                    Return fileID
                End If
            End If
        Next

        Return String.Empty
    End Function

    Public Shared Function ExtraerFileKey(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""

        URL = URL.Replace("%21", "!") ' Algunos links estan sin el ! :/
        URL = URL.Replace("%20", "") ' Algunos links tienen espacios en medio :/


        For Each pattern As String In patternGetInfoURL()
            Dim regex = New System.Text.RegularExpressions.Regex(pattern)
            If regex.IsMatch(URL) Then
                Dim match = regex.Match(URL)

                Dim fileKey = match.Groups("FileKey").Value & ""
                If fileKey.Length < 40 And Not IsMegaFolder(URL) Then ' Seguramente esté mal
                    Return String.Empty
                ElseIf fileKey.Contains(" ") Then
                    fileKey = fileKey.Replace(" ", "")
                End If
                Return fileKey
            End If
        Next

        Return String.Empty
    End Function

#End Region

End Class
