Imports System.IO
Imports System.Xml

Public Class Language



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


    Public Shared Sub InitLanguage(ByVal CultureCode As String)
        Try
            culture = System.Globalization.CultureInfo.GetCultureInfo(CultureCode)
            If String.IsNullOrEmpty(culture.Name) Then Throw New ApplicationException("Empty culture")
        Catch ex As Exception
            culture = System.Threading.Thread.CurrentThread.CurrentUICulture
        End Try

        ' Ponemos los ficheros en la carpeta Lang
        Dim LangPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Language")

        If Not System.IO.Directory.Exists(LangPath) Then
            System.IO.Directory.CreateDirectory(LangPath)
        End If
        For Each resname As String In Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames

            If resname.ToUpper.EndsWith("-Language.xml".ToUpper) Then

                Dim file As System.IO.Stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resname)

                If file Is Nothing Then
                    Log.WriteError("Language resource stream could not be opened: " & resname)
                    Continue For
                End If

                Dim xmlIdioma As New XmlDocument
                Try
                    xmlIdioma.Load(file)
                Finally
                    file.Close()
                End Try

                Dim CodigoIdioma As String = xmlIdioma.DocumentElement.Attributes("id").Value
                ' Built-in packs go under Language/Builtin so user customizations are never overwritten
                Dim builtinDir As String = Path.Combine(LangPath, "Builtin")
                If Not Directory.Exists(builtinDir) Then Directory.CreateDirectory(builtinDir)
                Dim XMLFile As String = Path.Combine(builtinDir, CodigoIdioma & ".xml")
                xmlIdioma.Save(XMLFile)

                ' Seed user language file only if missing (do not overwrite custom edits)
                Dim userLangFile As String = Path.Combine(LangPath, CodigoIdioma & ".xml")
                If Not IO.File.Exists(userLangFile) Then
                    xmlIdioma.Save(userLangFile)
                End If

                If CodigoIdioma.ToLowerInvariant = culture.Name.ToLowerInvariant Then
                    LanguageFileInternal = xmlIdioma
                ElseIf CodigoIdioma.ToLowerInvariant.Contains("-") And culture.Name.ToLowerInvariant.Contains("-") AndAlso
                       CodigoIdioma.ToLowerInvariant.Split("-"c)(0) = culture.Name.ToLowerInvariant.Split("-"c)(0) Then
                    If LanguageFileInternal Is Nothing OrElse LanguageFileInternal.DocumentElement.Attributes("id").Value.ToLowerInvariant <> culture.Name.ToLowerInvariant Then
                        LanguageFileInternal = xmlIdioma
                    End If
                ElseIf CodigoIdioma.ToLowerInvariant = "en-us" Then
                    If LanguageFileInternal Is Nothing Then LanguageFileInternal = xmlIdioma
                    LanguageFileEnUs = xmlIdioma
                End If

            End If
        Next
        If LanguageFileInternal Is Nothing Then
            Log.WriteError("Internal error: LanguageFileInternal is nothing")
            Throw New ApplicationException("LanguageFileInternal could not be intialized")
        End If
        If LanguageFileEnUs Is Nothing Then LanguageFileEnUs = LanguageFileInternal
        Log.WriteDebug("LanguageFileInternal loaded: " & LanguageFileInternal.DocumentElement.Attributes("id").Value)


        Dim XMLDiskFile As String = Path.Combine(LangPath, culture.Name & ".xml")
        If IO.File.Exists(XMLDiskFile) Then
            Dim xmlIdioma As New XmlDocument
            xmlIdioma.Load(XMLDiskFile)
            LanguageFileDisk = xmlIdioma

            Log.WriteDebug("LanguageFileDisk loaded from disk: " & LanguageFileDisk.DocumentElement.Attributes("id").Value)
        Else
            LanguageFileDisk = LanguageFileInternal

            Log.WriteDebug("LanguageFileDisk loaded from LanguageFileInternal: " & LanguageFileDisk.DocumentElement.Attributes("id").Value)
        End If

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
        Dim Lista As New Generic.Dictionary(Of String, String)
        Dim LangPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaDownloader/Language")
        If IO.Directory.Exists(LangPath) Then
            For Each File In IO.Directory.GetFiles(LangPath)
                If File.ToLower.EndsWith(".xml") Then
                    Try
                        Dim xmlIdioma As New XmlDocument
                        xmlIdioma.Load(File)
                        Dim CodigoIdioma As String = xmlIdioma.DocumentElement.Attributes("id").Value
                        Dim NombreIdioma As String = xmlIdioma.DocumentElement.Attributes("name").Value
                        If IsValidLanguageCode(CodigoIdioma) AndAlso Not Lista.ContainsKey(CodigoIdioma) Then
                            Lista.Add(CodigoIdioma, NombreIdioma)
                        End If
                    Catch ex As Exception
                    End Try
                End If
            Next
        End If
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
