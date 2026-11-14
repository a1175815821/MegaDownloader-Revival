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
    ' 字符类必须显式写 ASCII:.NET 的 \w 匹配 Unicode 字母,链接后紧跟中文/俄文/假名时
    ' (QQ/微信/论坛复制的常态)会被并进 key,解密必败。MEGA 的 ID/key 都是 base64url。
    ' 主机左边界必须显式写出:否则 "notmega.nz/folder/" 之类的仿冒域名会从 "mega.nz" 处
    ' 起匹配成功,被当成官方文件夹链接送去解密。前一字符是字母/数字/点/连字符时不算主机开头。
    Private Shared ReadOnly patternHTTPURI() As String = _
        {"(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", _
         "(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/(?<MODE2>#|#F|#N)!(?<FileID>[^\!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", _
         "(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?", _
         "(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?(?:/folder/(?<SubFolderID>[A-Za-z0-9_\-]+))?(?:/file/(?<SubFileID>[A-Za-z0-9_\-]+))?", _
         "(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/file/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?", _
         "(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.co\.nz/folder/(?<FileID>[^#/\s]+)(#(?<FileKey>[A-Za-z0-9_\-]+))?(?:/folder/(?<SubFolderID>[A-Za-z0-9_\-]+))?(?:/file/(?<SubFileID>[A-Za-z0-9_\-]+))?"}

    Private Shared ReadOnly patternMEGAURI() As String = _
        {"(?<TAG>mega)(?<MODE1>:///|://|:)(?<MODE2>#F|F|#N|N|#|)!(?<FileID>[^!\s]+)(!(?<FileKey>[A-Za-z0-9_\-#=]+))?", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>enc2(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)", _
        "(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>fenc2(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-#=]+)"}

    Private Shared ReadOnly patternELCUri() As String = _
        {"(?<TAG>mega)(?<MODE1>://|:///|:)(?<BASIC_ENCODE>elc(\?|/\?))(?<ENCODED_FILEID>[A-Za-z0-9_\-]+)"}

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
    Private Shared ReadOnly rxMegaGeneric As New Regex("(?<TAG>mega)(?<MODE>://|:///|:)(?<DATA>[A-Za-z0-9_\-/#!:.?]+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxElcConfig As Regex() = BuildRegexes(patternElcConfig)
    Private Shared ReadOnly rxSubFolderSuffix As New Regex("/folder/(?<SubFolderID>[A-Za-z0-9_\-]+)\s*$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxSubFileSuffix As New Regex("/file/(?<SubFileID>[A-Za-z0-9_\-]+)\s*$", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxFolderHost As New Regex("(?<![A-Za-z0-9.\-])(?:https?://)?(?:www\.)?mega\.(?:co\.)?nz/(?:#f!|folder/)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    Private Shared ReadOnly rxMegaPlainAt As New Regex("\G" & patternMEGAURI(0), RegexOptions.IgnoreCase Or RegexOptions.Compiled)
    Private Shared ReadOnly rxFolderSelection As New Regex("\G(?:/folder/[A-Za-z0-9_\-]+)?(?:/file/[A-Za-z0-9_\-]+)?",
                                                             RegexOptions.IgnoreCase Or RegexOptions.Compiled)


    ''' <summary>
    ''' Checks if the URL is a MEGA folder
    ''' </summary>
    ''' <param name="URI"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Shared Function IsMegaFolder(ByVal URI As String) As Boolean
        Dim Result As Boolean = False
        If String.IsNullOrEmpty(URI) Then Return False
        Result = rxFolderHost.IsMatch(URI)

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


    ''' <summary>Repairs a short key from complete base64url fragments. Stop at the key boundary
    ''' so ordinary prose after the final fragment remains outside the extracted link.</summary>
    Private Shared Function RepairSplitKey(text As String, m As Match) As String
        Dim key As Group = m.Groups("FileKey")
        Dim url As String = m.Value.Trim()
        Dim isFolder As Boolean = rxFolderHost.IsMatch(url) OrElse
                                  String.Equals(m.Groups("MODE2").Value.TrimStart("#"c), "F", StringComparison.OrdinalIgnoreCase)
        Dim expected As Integer = If(isFolder, 22, 43)
        If Not key.Success OrElse key.Length >= expected OrElse key.Index + key.Length <> m.Index + m.Length Then Return url

        Dim index As Integer = key.Index + key.Length
        Dim limit As Integer = index + Math.Min(text.Length - index, 128)
        Dim remaining As Integer = expected - key.Length
        Dim tail As New System.Text.StringBuilder(remaining)
        While remaining > 0
            If index >= limit OrElse Not Char.IsWhiteSpace(text(index)) Then Return url
            While index < limit AndAlso Char.IsWhiteSpace(text(index))
                index += 1
            End While
            Dim start As Integer = index
            While index < limit AndAlso IsKeyCharacter(text(index))
                index += 1
            End While
            Dim length As Integer = index - start
            If length = 0 OrElse length > remaining OrElse
               (index = limit AndAlso index < text.Length AndAlso IsKeyCharacter(text(index))) Then Return url
            tail.Append(text, start, length)
            remaining -= length
        End While

        If isFolder Then
            Dim suffix As Match = rxFolderSelection.Match(text, index)
            If suffix.Success Then tail.Append(suffix.Value)
        End If
        Return url & tail.ToString()
    End Function

    Private Shared Function IsKeyCharacter(c As Char) As Boolean
        Return (c >= "A"c AndAlso c <= "Z"c) OrElse (c >= "a"c AndAlso c <= "z"c) OrElse
               (c >= "0"c AndAlso c <= "9"c) OrElse c = "_"c OrElse c = "-"c
    End Function

    Public Shared Function ExtraerSoloURLsOficiales(ByVal Texto As String) As Generic.List(Of String)
        Dim links As New Generic.HashSet(Of String)
        If Not String.IsNullOrEmpty(Texto) Then AddOfficialLinks(Texto, links)
        Return links.ToList()
    End Function

    Private Shared Sub AddOfficialLinks(text As String, links As Generic.HashSet(Of String))
        For Each regex As Regex In rxHTTPURI
            For Each m As Match In regex.Matches(text)
                If m.Groups("FileID").Length > 0 Then links.Add(RepairSplitKey(text, m))
            Next
        Next
    End Sub

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


        AddOfficialLinks(Texto, links)

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
        Dim matches As MatchCollection = rxMegaGeneric.Matches(Texto)
        For Each match As Match In matches

            Dim data As String = match.Groups("DATA").Value
            Dim value As String = match.Value.Trim()
            Dim plain As Match = rxMegaPlainAt.Match(Texto, match.Index)
            If plain.Success AndAlso plain.Length = value.Length Then value = RepairSplitKey(Texto, plain)

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

        ' 不能在无 HTTP 匹配时提前 Return:旧式 token(mega://#F!根!key/folder/子ID,
        ' ELC 解码产物)不含 mega.nz 主机,提前返回会让下面的后缀兜底变成死代码,
        ' 用户只要一个子文件夹却把整个分享展开。
        Dim subFolderID As String = ""
        If m IsNot Nothing AndAlso m.Success Then subFolderID = m.Groups("SubFolderID").Value & ""
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

        ' 同 ExtraerSubFolderID:无 HTTP 匹配时也要走后缀兜底,否则单文件范围丢失。
        Dim subFileID As String = ""
        If m IsNot Nothing AndAlso m.Success Then subFileID = m.Groups("SubFileID").Value & ""
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
