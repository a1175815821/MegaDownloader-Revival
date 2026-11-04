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
        ' P-16: the built-in packs only change when the assembly changes. The old
        ' loop serialised all 10 XmlDocuments and read back all 10 Builtin files on
        ' EVERY startup. Gate it on the module MVID (changes on every rebuild, so a
        ' new binary always re-syncs); user-editable packs are still checked each run.
        Dim stampPath As String = Path.Combine(builtinDirectory, ".resource-stamp")
        Dim stamp As String = GetType(Language).Assembly.ManifestModule.ModuleVersionId.ToString("N")
        Dim needBuiltinSync As Boolean = True
        Try
            needBuiltinSync = Not File.Exists(stampPath) OrElse File.ReadAllText(stampPath) <> stamp
        Catch ex As Exception
            needBuiltinSync = True
        End Try
        For Each entry As KeyValuePair(Of String, XmlDocument) In builtin
            If needBuiltinSync Then SaveBuiltinIfChanged(entry.Value, Path.Combine(builtinDirectory, entry.Key & ".xml"))
            Dim userFile As String = Path.Combine(languageDirectory, entry.Key & ".xml")
            If Not File.Exists(userFile) Then entry.Value.Save(userFile)
        Next
        If needBuiltinSync Then
            Try
                File.WriteAllText(stampPath, stamp)
            Catch ex As Exception
                Log.WriteDebug("Language resource stamp could not be written: " & Log.SafeException(ex))
            End Try
        End If

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

    ''' <summary>P-15: GetText used to build an XPath string and scan the whole
    ''' XmlDocument three times per call (12-15 calls per 430ms tick). Cache the
    ''' key/value map per XmlDocument instance instead. Keyed by *reference*, so
    ''' restoring the original LanguageFile* fields (as the regression harness does)
    ''' transparently restores the matching cache — no stale global dictionaries.
    ''' User-edited packs get a fresh XmlDocument on reload, hence a fresh map.</summary>
    Private NotInheritable Class XmlDocumentReferenceComparer
        Implements IEqualityComparer(Of XmlDocument)

        Public Overloads Function Equals(x As XmlDocument, y As XmlDocument) As Boolean Implements IEqualityComparer(Of XmlDocument).Equals
            Return Object.ReferenceEquals(x, y)
        End Function

        Public Overloads Function GetHashCode(obj As XmlDocument) As Integer Implements IEqualityComparer(Of XmlDocument).GetHashCode
            Return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj)
        End Function
    End Class

    Private Shared ReadOnly textMaps As New System.Collections.Concurrent.ConcurrentDictionary(Of XmlDocument, Dictionary(Of String, String))(New XmlDocumentReferenceComparer())

    Private Shared Function BuildTextMap(pack As XmlDocument) As Dictionary(Of String, String)
        Dim map As New Dictionary(Of String, String)(StringComparer.Ordinal)
        If pack Is Nothing OrElse pack.DocumentElement Is Nothing Then Return map
        For Each node As XmlNode In pack.DocumentElement.SelectNodes("Text")
            Dim keyAttr As XmlAttribute = node.Attributes("key")
            If keyAttr Is Nothing OrElse String.IsNullOrEmpty(keyAttr.Value) Then Continue For
            Dim key As String = keyAttr.Value
            ' SelectSingleNode returned the first match; empty text was treated as missing.
            If Not map.ContainsKey(key) AndAlso Not String.IsNullOrEmpty(node.InnerText) Then
                map(key) = node.InnerText
            End If
        Next
        Return map
    End Function

    Private Shared Function GetTextMap(pack As XmlDocument) As Dictionary(Of String, String)
        If pack Is Nothing Then Return Nothing
        Dim map As Dictionary(Of String, String) = Nothing
        If Not textMaps.TryGetValue(pack, map) Then
            map = BuildTextMap(pack)
            map = textMaps.GetOrAdd(pack, map)
        End If
        Return map
    End Function

    Private Shared Function ProcessMsg(ByVal msg As String) As String
#If MSD Then
        If String.IsNullOrEmpty(msg) Then Return String.Empty
        Return msg.Replace("MegaDownloader", "MegaSearch Desktop")
#Else
        Return msg
#End If
    End Function


    Public Shared Function GetText(key As String) As String
        If key Is Nothing Then Return String.Empty
        Dim text As String = Nothing
        ' 1) Current language on disk (may be user-customized)
        Dim map As Dictionary(Of String, String) = GetTextMap(LanguageFileDisk)
        If map IsNot Nothing AndAlso map.TryGetValue(key, text) Then Return ProcessMsg(text)

        ' 2) Built-in pack for current language
        map = GetTextMap(LanguageFileInternal)
        If map IsNot Nothing AndAlso map.TryGetValue(key, text) Then Return ProcessMsg(text)

        ' 3) Stable fallback to en-US
        If Not Object.ReferenceEquals(LanguageFileEnUs, LanguageFileInternal) Then
            map = GetTextMap(LanguageFileEnUs)
            If map IsNot Nothing AndAlso map.TryGetValue(key, text) Then Return ProcessMsg(text)
        End If

        Log.WriteDebug("Translation not found: " & key)
        Return ProcessMsg(key)
    End Function

    ' P-16: GetAvailableLanguages used to re-parse every user language XML on each
    ' settings-window open. Cache the result keyed by file path + write time, so new
    ' or edited packs are still picked up without any XML parsing on cache hits.
    Private Shared ReadOnly _availableLanguagesLock As New Object()
    Private Shared _availableLanguagesCache As Generic.Dictionary(Of String, String) = Nothing
    Private Shared _availableLanguagesStamp As String = Nothing

    Private Shared Function GetUserPackStamp(directory As String) As String
        Dim sb As New System.Text.StringBuilder()
        If Not IO.Directory.Exists(directory) Then Return String.Empty
        For Each file As String In IO.Directory.GetFiles(directory, "*.xml").OrderBy(Function(value) value, StringComparer.OrdinalIgnoreCase)
            Try
                sb.Append(IO.Path.GetFileName(file)).Append(":"c).Append(IO.File.GetLastWriteTimeUtc(file).Ticks).Append(";"c)
            Catch ex As Exception
                Return Nothing ' force a full refresh when a file cannot be inspected
            End Try
        Next
        Return sb.ToString()
    End Function

    Public Shared Function GetAvailableLanguages() As Generic.Dictionary(Of String, String)
        SyncLock _availableLanguagesLock
            Dim directory As String = GetLanguageDirectory()
            Dim stamp As String = GetUserPackStamp(directory)
            If stamp IsNot Nothing AndAlso _availableLanguagesCache IsNot Nothing AndAlso
               String.Equals(stamp, _availableLanguagesStamp, StringComparison.Ordinal) Then
                Return New Generic.Dictionary(Of String, String)(_availableLanguagesCache, StringComparer.OrdinalIgnoreCase)
            End If

            Dim Lista As New Generic.Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each entry As KeyValuePair(Of String, XmlDocument) In builtinPacks.Value.OrderBy(Function(value) value.Key, StringComparer.OrdinalIgnoreCase)
                Lista(entry.Key) = entry.Value.DocumentElement.GetAttribute("name")
            Next
            For Each entry As KeyValuePair(Of String, XmlDocument) In LoadUserPacks(directory)
                Dim name As String = entry.Value.DocumentElement.GetAttribute("name")
                Lista(entry.Key) = If(String.IsNullOrWhiteSpace(name), entry.Key, name)
            Next
            _availableLanguagesCache = New Generic.Dictionary(Of String, String)(Lista, StringComparer.OrdinalIgnoreCase)
            _availableLanguagesStamp = stamp
            Return Lista
        End SyncLock
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
