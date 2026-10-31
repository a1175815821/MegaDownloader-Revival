Imports System.IO
Imports System.Xml
Imports System.Globalization
Imports System.Collections.Generic

Public Class Language
    Public Const FollowSystemLanguageCode As String = "auto"
    Private Const EnglishLanguageCode As String = "en-US"
    Private Shared ReadOnly builtinPacks As New Lazy(Of Dictionary(Of String, XmlDocument))(AddressOf LoadBuiltinPacks)



    Private Shared culture As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture


    Private Shared LanguageFileDisk As XmlDocument = Nothing
    Private Shared LanguageFileInternal As XmlDocument = Nothing
    Private Shared LanguageFileEnUs As XmlDocument = Nothing

    Public Shared Function GetCurrentLanguageCode() As String
        Return culture.Name
    End Function

    Public Shared Function IsValidLanguageCode(ByVal CultureCode As String) As Boolean
        Try
            If String.IsNullOrEmpty(System.Globalization.CultureInfo.GetCultureInfo(CultureCode).Name) Then Return False
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function


    Public Shared Function NormalizeLanguagePreference(code As String) As String
        If String.IsNullOrWhiteSpace(code) OrElse String.Equals(code.Trim(), FollowSystemLanguageCode, StringComparison.OrdinalIgnoreCase) Then
            Return FollowSystemLanguageCode
        End If
        Try
            Dim name As String = CultureInfo.GetCultureInfo(code.Trim()).Name
            Return If(name.Length = 0, FollowSystemLanguageCode, name)
        Catch ex As Exception
            Return FollowSystemLanguageCode
        End Try
    End Function

    Public Shared Sub InitLanguage(ByVal CultureCode As String)
        InitializeLanguage(CultureCode, CultureInfo.CurrentUICulture, GetLanguageDirectory())
    End Sub

    Private Shared Function GetLanguageDirectory() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Language")
    End Function

    Private Shared Function LoadBuiltinPacks() As Dictionary(Of String, XmlDocument)
        Dim packs As New Dictionary(Of String, XmlDocument)(StringComparer.OrdinalIgnoreCase)
        For Each resource As String In GetType(Language).Assembly.GetManifestResourceNames()
            If Not resource.EndsWith("-Language.xml", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim pack As New XmlDocument()
            Using stream As Stream = GetType(Language).Assembly.GetManifestResourceStream(resource)
                If stream Is Nothing Then Throw New InvalidDataException("Language resource could not be opened: " & resource)
                pack.Load(stream)
            End Using
            Dim code As String = pack.DocumentElement.GetAttribute("id")
            If pack.DocumentElement.Name <> "Language" OrElse Not IsValidLanguageCode(code) Then
                Throw New InvalidDataException("Invalid built-in language pack: " & resource)
            End If
            packs(CultureInfo.GetCultureInfo(code).Name) = pack
        Next
        If Not packs.ContainsKey(EnglishLanguageCode) Then Throw New InvalidDataException("The English language pack is missing.")
        Return packs
    End Function

    Private Shared Function LoadUserPacks(directory As String) As Dictionary(Of String, XmlDocument)
        Dim packs As New Dictionary(Of String, XmlDocument)(StringComparer.OrdinalIgnoreCase)
        If Not IO.Directory.Exists(directory) Then Return packs
        For Each file As String In IO.Directory.GetFiles(directory, "*.xml").OrderBy(Function(value) value, StringComparer.OrdinalIgnoreCase)
            Try
                Dim pack As New XmlDocument()
                pack.Load(file)
                If pack.DocumentElement Is Nothing OrElse pack.DocumentElement.Name <> "Language" Then Continue For
                Dim code As String = pack.DocumentElement.GetAttribute("id")
                If IsValidLanguageCode(code) AndAlso Not String.Equals(code, FollowSystemLanguageCode, StringComparison.OrdinalIgnoreCase) Then
                    packs(CultureInfo.GetCultureInfo(code).Name) = pack
                End If
            Catch ex As Exception
                Log.WriteError("User language file could not be loaded: " & file & " (" & Log.SafeException(ex) & ")")
            End Try
        Next
        Return packs
    End Function

    Private Shared Function GetLanguageFamily(value As CultureInfo) As String
        Dim parent As CultureInfo = value
        While Not parent.IsNeutralCulture AndAlso parent.Parent.Name.Length > 0
            parent = parent.Parent
        End While
        ' Preserve script distinctions, including the older .NET Chinese aliases.
        If String.Equals(parent.Name, "zh-CHS", StringComparison.OrdinalIgnoreCase) Then Return "zh-Hans"
        If String.Equals(parent.Name, "zh-CHT", StringComparison.OrdinalIgnoreCase) Then Return "zh-Hant"
        Return parent.Name
    End Function

    Friend Shared Function ResolveLanguageCode(preference As String, systemLanguage As CultureInfo, availableCodes As IEnumerable(Of String)) As String
        Dim normalized As String = NormalizeLanguagePreference(preference)
        Dim preferred As CultureInfo = If(normalized = FollowSystemLanguageCode,
                                         If(systemLanguage, CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo(normalized))
        If preferred.Name.Length = 0 Then Return EnglishLanguageCode
        Dim available As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each code As String In availableCodes
            If IsValidLanguageCode(code) Then
                Dim canonical As String = CultureInfo.GetCultureInfo(code).Name
                available(canonical) = canonical
            End If
        Next
        Dim exact As String = Nothing
        If available.TryGetValue(preferred.Name, exact) Then Return exact

        Dim family As String = GetLanguageFamily(preferred)
        Dim genericNeutral As Boolean = preferred.IsNeutralCulture AndAlso preferred.Name.IndexOf("-"c) < 0
        Dim related As String = available.Keys.Where(
            Function(code)
                Dim candidate As CultureInfo = CultureInfo.GetCultureInfo(code)
                Return If(genericNeutral,
                          String.Equals(candidate.TwoLetterISOLanguageName, preferred.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase),
                          String.Equals(GetLanguageFamily(candidate), family, StringComparison.OrdinalIgnoreCase))
            End Function).OrderBy(Function(code) If(builtinPacks.Value.ContainsKey(code), 0, 1)).
            ThenBy(Function(code) code, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
        Return If(related, EnglishLanguageCode)
    End Function

    ' Exercise this same initialization path with a temporary directory in tests.
    Private Shared Sub InitializeLanguage(preference As String, systemLanguage As CultureInfo, languageDirectory As String)
        Dim builtin As Dictionary(Of String, XmlDocument) = builtinPacks.Value
        Dim builtinDirectory As String = Path.Combine(languageDirectory, "Builtin")
        Directory.CreateDirectory(builtinDirectory)
        For Each entry As KeyValuePair(Of String, XmlDocument) In builtin
            SaveBuiltinIfChanged(entry.Value, Path.Combine(builtinDirectory, entry.Key & ".xml"))
            Dim userFile As String = Path.Combine(languageDirectory, entry.Key & ".xml")
            If Not File.Exists(userFile) Then entry.Value.Save(userFile)
        Next

        Dim users As Dictionary(Of String, XmlDocument) = LoadUserPacks(languageDirectory)
        Dim selected As String = ResolveLanguageCode(preference, systemLanguage, builtin.Keys.Concat(users.Keys))
        Dim internalPack As XmlDocument = builtin(ResolveLanguageCode(selected, systemLanguage, builtin.Keys))
        Dim diskPack As XmlDocument = Nothing
        If Not users.TryGetValue(selected, diskPack) Then diskPack = internalPack
        LanguageFileEnUs = builtin(EnglishLanguageCode)
        LanguageFileInternal = internalPack
        LanguageFileDisk = diskPack
        culture = CultureInfo.GetCultureInfo(selected)
        Log.WriteDebug("Language selected: " & selected & " (preference: " & NormalizeLanguagePreference(preference) & ")")
    End Sub


    Private Shared Sub SaveBuiltinIfChanged(xml As XmlDocument, filePath As String)
        Using serialized As New MemoryStream()
            xml.Save(serialized)
            Dim expected As Byte() = serialized.ToArray()
            If IO.File.Exists(filePath) AndAlso IO.File.ReadAllBytes(filePath).SequenceEqual(expected) Then Return
            IO.File.WriteAllBytes(filePath, expected)
        End Using
    End Sub

    Private Shared Function ProcessMsg(ByVal msg As String) As String
#If MSD Then
        If String.IsNullOrEmpty(msg) Then Return String.Empty
        Return msg.Replace("MegaDownloader", "MegaSearch Desktop")
#Else
        Return msg
#End If
    End Function


    Public Shared Function GetText(key As String) As String
        ' 1) Current language on disk (may be user-customized)
        Dim nodo As XmlNode = Nothing
        If LanguageFileDisk IsNot Nothing Then
            nodo = LanguageFileDisk.DocumentElement.SelectSingleNode("Text[@key='" & key & "']")
            If nodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(nodo.InnerText) Then Return ProcessMsg(nodo.InnerText)
        End If

        Log.WriteDebug("Translation not found on disk: " & key)

        ' 2) Built-in pack for current language
        If LanguageFileInternal IsNot Nothing Then
            nodo = LanguageFileInternal.DocumentElement.SelectSingleNode("Text[@key='" & key & "']")
            If nodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(nodo.InnerText) Then Return ProcessMsg(nodo.InnerText)
        End If

        ' 3) Stable fallback to en-US
        If LanguageFileEnUs IsNot Nothing AndAlso Not Object.ReferenceEquals(LanguageFileEnUs, LanguageFileInternal) Then
            nodo = LanguageFileEnUs.DocumentElement.SelectSingleNode("Text[@key='" & key & "']")
            If nodo IsNot Nothing AndAlso Not String.IsNullOrEmpty(nodo.InnerText) Then Return ProcessMsg(nodo.InnerText)
        End If

        Log.WriteDebug("Translation not found on disk and internal lang file: " & key)
        Return ProcessMsg(key)
    End Function

    Public Shared Function GetAvailableLanguages() As Generic.Dictionary(Of String, String)
        Dim Lista As New Generic.Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each entry As KeyValuePair(Of String, XmlDocument) In builtinPacks.Value.OrderBy(Function(value) value.Key, StringComparer.OrdinalIgnoreCase)
            Lista(entry.Key) = entry.Value.DocumentElement.GetAttribute("name")
        Next
        For Each entry As KeyValuePair(Of String, XmlDocument) In LoadUserPacks(GetLanguageDirectory())
            Dim name As String = entry.Value.DocumentElement.GetAttribute("name")
            Lista(entry.Key) = If(String.IsNullOrWhiteSpace(name), entry.Key, name)
        Next
        Return Lista
    End Function





    Public Shared Sub SaveTranslationReport()
        Dim str As New System.Text.StringBuilder


        Dim htBase As New Generic.Dictionary(Of String, String)
        Dim BaseLanguage As String = "en-US"


        ' First we retrieve the base language
        For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames
            If resname.ToUpper.EndsWith("-Language.xml".ToUpper) Then
                Dim file As System.IO.Stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname)

                If file Is Nothing Then Continue For

                Dim xmlIdioma As New XmlDocument
                Try
                    xmlIdioma.Load(file)
                Finally
                    file.Close()
                End Try

                Dim CodigoIdioma As String = xmlIdioma.DocumentElement.Attributes("id").Value
                If CodigoIdioma = BaseLanguage Then
                    For Each textNode As XmlNode In xmlIdioma.DocumentElement.SelectNodes("Text")
                        Dim KEY As String = textNode.Attributes("key").Value
                        Dim VALUE As String = textNode.InnerText
                        htBase(KEY) = VALUE
                    Next
                End If
            End If
        Next

        ' Then we compare with the rest of languages
        For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames
            If resname.ToUpper.EndsWith("-Language.xml".ToUpper) Then
                Dim file As System.IO.Stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname)

                If file Is Nothing Then Continue For

                Dim strIdi As New System.Text.StringBuilder

                Dim xmlIdioma As New XmlDocument
                Try
                    xmlIdioma.Load(file)
                Finally
                    file.Close()
                End Try

                Dim htIdioma As New Generic.Dictionary(Of String, String)

                Dim CodigoIdioma As String = xmlIdioma.DocumentElement.Attributes("id").Value
                For Each textNode As XmlNode In xmlIdioma.DocumentElement.SelectNodes("Text")
                    Dim KEY As String = textNode.Attributes("key").Value
                    Dim VALUE As String = textNode.InnerText
                    htIdioma(KEY) = VALUE
                Next

                For Each key As String In htBase.Keys
                    If Not htIdioma.ContainsKey(key) Then
                        If strIdi.Length = 0 Then
                            strIdi.AppendLine(vbNewLine)
                            strIdi.AppendLine("Missing text in " & CodigoIdioma & " that is present in " & BaseLanguage & ": " & vbNewLine)
                        End If
                        strIdi.AppendLine("<Text key=""" & key.Replace("&", "&amp;") & """><![CDATA[" & htBase(key) & "]]></Text>")
                    End If
                Next
                If strIdi.Length > 0 Then
                    str.AppendLine(strIdi.ToString)
                End If
            End If
        Next


        If str.Length > 0 Then
            Log.WriteError(vbNewLine & "TRANSLATION REPORT" & vbNewLine & str.ToString)
        End If
    End Sub


End Class
