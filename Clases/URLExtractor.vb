Imports System.Text.RegularExpressions

Public Class URLExtractor

    ' Esta clase debe ser igual en MegaDownloader y MegaUploader!!

    'Private Shared ENC_XOR As Byte() = New Byte() {-} ' REMOVED FROM SOURCE CODE, SORRY
    Private Shared ENC_XOR As Byte() = New Byte() {12, 57, 251, 120, 18, 75, 6, 250, 85} ' REMOVED FROM SOURCE CODE, SORRY

    Public Const ENCODE_PASSWORD As String = "k1o6Al-1kz¿!z05y"
    Public Const ENCODE_PASSWORD2 As String = "nYrXa@9Q¿1&hCWM\9(731Bp?t42=!k3."

    Public Const ENCODEDPREFIX As String = "enc?"
    Public Const ENCODEDPREFIX2 As String = "enc2?"
    Public Const FOLDERENCODEDPREFIX As String = "fenc?"
    Public Const FOLDERENCODEDPREFIX2 As String = "fenc2?"

    Public Const SERVERENCODEDPREFIX As String = "elc?"

    ' Host-bounded patterns: only mega.nz / mega.co.nz (optional www.), case-insensitive at match time
    ' Modern folder links may address a subfolder or a single file inside the folder:
    '   https://mega.nz/folder/<rootID>#<key>                     -> whole folder
    '   https://mega.nz/folder/<rootID>#<key>/folder/<subID>      -> subfolder only
    '   https://mega.nz/folder/<rootID>#<key>/file/<fileID>       -> single file only
    ' The SubFolderID/SubFileID groups keep the suffix inside match.Value so link
    ' extraction (clipboard / add-links dialog) does not silently drop it.
    Private Shared ReadOnly patternHTTPURI() As String = _
        {"(?:https?://)?(?:www\.)?mega\.co\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[\w\-#=]+))?", _
         "(?:https?://)?(?:www\.)?mega\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[\w\-#=]+))?", _
         "(?:https?://)?(?:www\.)?mega\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[\w\-]+))?", _
         "(?:https?://)?(?:www\.)?mega\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[\w\-]+))?(?:/folder/(?<SubFolderID>[\w\-]+))?(?:/file/(?<SubFileID>[\w\-]+))?", _
         "(?:https?://)?(?:www\.)?mega\.co\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[\w\-]+))?", _
         "(?:https?://)?(?:www\.)?mega\.co\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[\w\-]+))?(?:/folder/(?<SubFolderID>[\w\-]+))?(?:/file/(?<SubFileID>[\w\-]+))?"}

    Private Shared ReadOnly patternMEGAURI() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<MODE2>#|#F|F|#N|N)!(?<FileID>[^\!]+)(!(?<FileKey>[\w-#=]+))?", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc2(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc2(\?|/\?))(?<ENCODED_FILEID>[\w-#=]+)"}

    Private Shared ReadOnly patternELCUri() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>elc(\?|/\?))(?<ENCODED_FILEID>[\w-]+)"}

    ' 注意:patternElcConfig 必须在下面的 rx 单例之前声明(Shared 字段按文本顺序初始化)。
    Private Shared ReadOnly patternElcConfig() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>configelc(\?|/\?))(?<ENCODED_CONFIG>[^\s]+)"}

    ' B3-⑧:正则单例(Compiled)。此前每次调用 New Regex,粘贴数百链接时
    ' ExtraerURLs×ExtraerFileID/Key×IsMegaFolder 层层回扫(11 pattern 全表扫描),
    ' UI 线程冻结数秒~十数秒。Regex 实例读方法线程安全,可全局共享。
    Private Shared Function BuildRegexes(ByVal ParamArray patterns As String()) As Regex()
        Dim list As New Generic.List(Of Regex)(patterns.Length)
        For Each p As String In patterns
            list.Add(New Regex(p, RegexOptions.IgnoreCase Or RegexOptions.Compiled))
        Next
        Return list.ToArray()
    End Function

    Private Shared ReadOnly rxHTTPURI As Regex() = BuildRegexes(patternHTTPURI)
    Private Shared ReadOnly rxELCUri As Regex() = BuildRegexes(patternELCUri)
    Private Shared ReadOnly rxGetInfoURL As Regex() = BuildRegexes(patternHTTPURI.Concat(patternMEGAURI).Concat(patternELCUri).ToArray())
    Private Shared ReadOnly rxMegaGeneric As New Regex("(?<TAG>mega)(?<MODE>://|:///|:)(?<DATA>[\w-/#!:.?]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxElcConfig As Regex() = BuildRegexes(patternElcConfig)
    Private Shared ReadOnly rxSubFolderSuffix As New Regex("/folder/(?<SubFolderID>[\w\-]+)\s*$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxSubFileSuffix As New Regex("/file/(?<SubFileID>[\w\-]+)\s*$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    Private Shared Function patternGetInfoURL() As String()
        Return patternHTTPURI.Concat(patternMEGAURI).Concat(patternELCUri).ToArray
    End Function


    ''' <summary>
    ''' Checks if the URL is a MEGA folder
    ''' </summary>
    ''' <param name="URI"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Shared Function IsMegaFolder(ByVal URI As String) As Boolean
        Dim Result As Boolean = False
        If String.IsNullOrEmpty(URI) Then Return False
        Dim uriLower As String = URI.ToLowerInvariant()
        Result = uriLower.Contains("mega.co.nz/#f!") OrElse uriLower.Contains("mega.nz/#f!") OrElse
                 uriLower.Contains("mega.nz/folder/") OrElse uriLower.Contains("mega.co.nz/folder/")

        If Not Result Then ' Enlaces mega sin codificar
            For Each regex As Regex In rxGetInfoURL
                If regex.IsMatch(URI) Then
                    Dim match = regex.Match(URI)

                    If Not String.IsNullOrEmpty(match.Groups("MODE2").Value) Then
                        ' Enlace mega: sin codificar (comparación insensible a mayúsculas: #f/#F)
                        Select Case match.Groups("MODE2").Value.ToUpperInvariant()
                            Case "#F", "F"
                                Result = True
                                Exit For
                        End Select

                    ElseIf Not String.IsNullOrEmpty(match.Groups("BASIC_ENCODE").Value) AndAlso match.Groups("BASIC_ENCODE").Value.StartsWith("fenc", StringComparison.OrdinalIgnoreCase) Then
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
        For Each regex As Regex In rxELCUri
            If regex.IsMatch(URI) Then Return True
        Next
        Return False
    End Function


    Public Shared Function ExtraerSoloURLsOficiales(ByVal Texto As String) As Generic.List(Of String)
        Dim links As New Generic.HashSet(Of String) ' Evitamos repetidos

        If Texto Is Nothing Then Return links.ToList

        For Each regx As Regex In rxHTTPURI

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

        Dim matches As MatchCollection

        For Each regx As Regex In rxElcConfig

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
        Dim matches As MatchCollection

        For Each regx As Regex In rxHTTPURI

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
        ' mega://enc?_xlPqemSILarh5VBKbhSTFyQQQ0
        ' mega://senc?_xlPqemSILarh5VBKbhSTFyQQQ0
        matches = rxMegaGeneric.Matches(Texto)
        For Each match As Match In matches

            Dim data As String = match.Groups("DATA").Value
            Dim value As String = match.Value.Trim


            If Not String.IsNullOrEmpty(data) AndAlso data.Length > 5 Then

                If data.ToLower.StartsWith(ENCODEDPREFIX) Or _
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

        ' Encoded links (enc?/fenc?/enc2?/fenc2?): el FileID trae el prefijo y el payload cifrado
        If String.IsNullOrEmpty(FileKey) And Not String.IsNullOrEmpty(FileID) _
                AndAlso (FileID.StartsWith(ENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) Or FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) Or FileID.StartsWith(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) Or FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase)) Then


            ' Encoded string
            ' Generate it: GenerateEncodedURILink
            Dim encodedStr As String
            If FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) Then
                encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) + FOLDERENCODEDPREFIX2.Length)
            ElseIf FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) Then
                encodedStr = FileID.Substring(FileID.IndexOf(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) + FOLDERENCODEDPREFIX.Length)
            ElseIf FileID.StartsWith(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) Then
                encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase) + ENCODEDPREFIX2.Length)
            Else
                encodedStr = FileID.Substring(FileID.IndexOf(ENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) + ENCODEDPREFIX.Length)
            End If

            ' base64url sin padding: longitud % 4 == 1 es inválida (nunca la genera un encoder real).
            ' Antes: "==".Substring(3) lanzaba ArgumentOutOfRangeException. Ahora: error amistoso.
            If (encodedStr.Length Mod 4) = 1 Then
                Throw New ApplicationException("Invalid encoded link.")
            End If
            encodedStr &= "==".Substring((2 - encodedStr.Length * 3) And 3)
            encodedStr = encodedStr.Replace("-", "+").Replace("_", "/").Replace(",", "")

            Dim isFolder As Boolean = FileID.StartsWith(FOLDERENCODEDPREFIX, StringComparison.OrdinalIgnoreCase) Or FileID.StartsWith(FOLDERENCODEDPREFIX2, StringComparison.OrdinalIgnoreCase)

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
            ' 兼容模式：保持旧静态 IV 密文格式，确保旧版本程序仍能解密。
            str = Criptografia.AES_EncryptString(link, ENCODE_PASSWORD, useRandomIV:=False)
        Else
            str = Criptografia.AES_EncryptString(link, getENC2Bytes, System.Text.Encoding.ASCII)
        End If

        ' 加密失败返回 Nothing：显式报错而非生成损坏的编码链接。
        If str Is Nothing Then
            Throw New ApplicationException("Failed to encrypt the encoded link.")
        End If

        str = str.Replace("+", "-").Replace("/", "_").Replace("=", "")

        Return "mega://" & If(MegaFolder, FOLDERENCODEDPREFIX2, ENCODEDPREFIX2) & str
    End Function


#Region "Extract FileID/FileKey"

    ''' <summary>
    ''' Extracts the subfolder handle from a modern folder link
    ''' (mega.nz/folder/ID#KEY/folder/SUBID). Empty for whole-folder links.
    ''' </summary>
    Public Shared Function ExtraerSubFolderID(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""

        Dim m As System.Text.RegularExpressions.Match = Nothing
        For Each regex As System.Text.RegularExpressions.Regex In rxHTTPURI
            If regex.IsMatch(URL) Then
                m = regex.Match(URL)
                Exit For
            End If
        Next

        If m Is Nothing OrElse Not m.Success Then Return ""
        Dim subFolderID As String = m.Groups("SubFolderID").Value & ""
        If String.IsNullOrEmpty(subFolderID) Then
            ' 回退:旧式 token (mega://#F!根!key/folder/子ID,ELC 解码产物)后缀解析
            Dim suffix As System.Text.RegularExpressions.Match = rxSubFolderSuffix.Match(URL)
            If suffix.Success Then subFolderID = suffix.Groups("SubFolderID").Value
        End If
        Return subFolderID.Trim()
    End Function

    ''' <summary>
    ''' Extracts the single-file handle from a modern folder link
    ''' (mega.nz/folder/ID#KEY/file/FILEID). Empty for whole-folder links.
    ''' </summary>
    Public Shared Function ExtraerSubFileID(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""

        Dim m As System.Text.RegularExpressions.Match = Nothing
        For Each regex As System.Text.RegularExpressions.Regex In rxHTTPURI
            If regex.IsMatch(URL) Then
                m = regex.Match(URL)
                Exit For
            End If
        Next

        If m Is Nothing OrElse Not m.Success Then Return ""
        Dim subFileID As String = m.Groups("SubFileID").Value & ""
        If String.IsNullOrEmpty(subFileID) Then
            ' 回退:旧式 token (mega://#F!根!key/file/文件ID,ELC 解码产物)后缀解析
            Dim suffix As System.Text.RegularExpressions.Match = rxSubFileSuffix.Match(URL)
            If suffix.Success Then subFileID = suffix.Groups("SubFileID").Value
        End If
        Return subFileID.Trim()
    End Function

    ' B3-⑨:论坛/聊天软件会把 fragment 转义(%23=#,%3D== 等,=###n= 恰含 #/=)。
    ' 旧代码只洗 %21/%20,转义过的 key 原样进 B64Decode 必败,报永久性"无法解密"。
    ' 这里做一次性 URL 解码(非法 % 序列回退原串)+去空白;长度判定必须在清洗后做。
    Private Shared Function NormalizeLinkForExtraction(ByVal URL As String) As String
        URL = URL.Replace("%21", "!") ' Algunos links estan sin el ! :/
        URL = URL.Replace("%20", "") ' Algunos links tienen espacios en medio :/
        Try
            Dim unescaped As String = Uri.UnescapeDataString(URL)
            If Not String.IsNullOrEmpty(unescaped) Then URL = unescaped
        Catch
            ' 非法 % 序列(如裸 %/截断 %2)保持原串,后续正则照常处理
        End Try
        URL = URL.Replace(" ", "").Replace(vbTab, "").Replace(vbCr, "").Replace(vbLf, "")
        Return URL
    End Function

    Public Shared Function ExtraerFileID(ByVal URL As String) As String
        If String.IsNullOrEmpty(URL) Then Return ""

        URL = NormalizeLinkForExtraction(URL)

        For Each regex As Regex In rxGetInfoURL
            If regex.IsMatch(URL) Then
                Dim match = regex.Match(URL)

                If Not String.IsNullOrEmpty(match.Groups("ENCODED_FILEID").Value) Then
                    Return match.Groups("BASIC_ENCODE").Value & match.Groups("ENCODED_FILEID").Value.Trim("/"c)
                Else
                    Dim fileID = match.Groups("FileID").Value & ""

                    If Not String.IsNullOrEmpty(match.Groups("MODE2").Value) Then ' private file from folder
                        Select Case match.Groups("MODE2").Value.ToUpperInvariant()
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

        URL = NormalizeLinkForExtraction(URL)


        For Each regex As Regex In rxGetInfoURL
            If regex.IsMatch(URL) Then
                Dim match = regex.Match(URL)

                Dim fileKey = match.Groups("FileKey").Value & ""
                ' B3-⑨:空白已在 NormalizeLinkForExtraction 去除(旧 Contains(" ") 分支是死代码:
                ' FileKey 组字符集 [\w\-#=] 根本吃不进空格),此处直接判长。
                If fileKey.Length < 40 And Not IsMegaFolder(URL) Then ' Seguramente esté mal
                    Return String.Empty
                End If
                Return fileKey
            End If
        Next

        Return String.Empty
    End Function

#End Region

End Class
