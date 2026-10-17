Imports Microsoft.Win32
Imports System.Security.Principal
Imports System.IO

Public Class MegaURIProtocol

    ' http://www.openwinforms.com/run_exe_from_javascript.html


    Public Const UrlProtocol As String = "mega"
    Public Shared Function RegisterUrlProtocol() As Exception
        Dim rootKey As RegistryKey = Nothing
        Dim rKey As RegistryKey = Nothing
        Try
            ' P1-1:写 HKCU\Software\Classes 而不是 HKCR。HKCR 的写入落到 HKLM 需要
            ' 提权,而 manifest 是 asInvoker——标准用户(以及 -silent 开机自启)下必然抛
            ' UnauthorizedAccessException,导致每次启动弹框、静默模式更会以无属主模态框
            ' 阻塞 Main_Load。HKCU 每用户注册无需提权,且优先级高于 HKLM 旧项,
            ' 便携版挪位置后也不再需要管理员重注册。
            rootKey = Registry.CurrentUser.CreateSubKey("Software\Classes")
            rKey = rootKey.OpenSubKey(UrlProtocol, False)

            If rKey Is Nothing Then
                rKey = rootKey.CreateSubKey(UrlProtocol)
                rKey.SetValue("", "URL: mega Protocol")
                rKey.SetValue("URL Protocol", "")

                'Dim rKeyIcon As RegistryKey = rKey.CreateSubKey("DefaultIcon")
                'rKeyIcon.SetValue("", """" & iconFilePath & """")
                If rKey IsNot Nothing Then rKey.Close()

                rKey = rootKey.CreateSubKey(UrlProtocol & "\shell\open\command")
                rKey.SetValue("", """" & Application.ExecutablePath & """ %1")
                Log.WriteInfo("MEGA URI protocol added to registry (HKCU\Software\Classes).")
            Else
                rKey.Close()
                Dim val As String = """" & Application.ExecutablePath & """ %1"

                Dim subKey As RegistryKey = rootKey.OpenSubKey(UrlProtocol & "\shell\open\command", False)
                If subKey Is Nothing Then
                    subKey = rootKey.CreateSubKey(UrlProtocol & "\shell\open\command")
                End If
                If subKey Is Nothing OrElse subKey.GetValue("") Is Nothing OrElse CStr(subKey.GetValue("")) <> val Then
                    If subKey IsNot Nothing Then subKey.Close()
                    subKey = rootKey.OpenSubKey(UrlProtocol & "\shell\open\command", True)
                    If subKey IsNot Nothing Then subKey.SetValue("", val)
                End If
                If subKey IsNot Nothing Then subKey.Close()
            End If
            Return Nothing
        Catch ex As UnauthorizedAccessException
            Log.WriteError("SECURITY ERROR: could not write HKCU\Software\Classes for the MEGA URI protocol. Check group policy / registry restrictions. Error: " & Log.SafeException(ex))
            Return ex

        Catch ex As Security.SecurityException
            Log.WriteError("SECURITY ERROR: could not write HKCU\Software\Classes for the MEGA URI protocol. Check group policy / registry restrictions. Error: " & Log.SafeException(ex))
            Return ex

        Catch ex As Exception
            Log.WriteError("Error accessing the registry for registering MEGA URI protocol. Error: " & ex.ToString)
            Return ex

        Finally
            If rKey IsNot Nothing Then rKey.Close()
            If rootKey IsNot Nothing Then rootKey.Close()
        End Try
    End Function

    Public Function IsAdmin() As Boolean
        Dim id As WindowsIdentity = WindowsIdentity.GetCurrent()
        Dim p As WindowsPrincipal = New WindowsPrincipal(id)
        Return p.IsInRole(WindowsBuiltInRole.Administrator)
    End Function


End Class
