Imports Microsoft.Win32
Imports System.Security.Principal
Imports System.IO

Public Class MegaURIProtocol

    ' http://www.openwinforms.com/run_exe_from_javascript.html


    Public Const UrlProtocol As String = "mega"
    Public Shared Function RegisterUrlProtocol() As Exception
        Dim rKey As RegistryKey = Nothing
        Try
            rKey = Registry.ClassesRoot.OpenSubKey(UrlProtocol, False)

            If rKey Is Nothing Then
                rKey = Registry.ClassesRoot.CreateSubKey(UrlProtocol)
                rKey.SetValue("", "URL: mega Protocol")
                rKey.SetValue("URL Protocol", "")

                'Dim rKeyIcon As RegistryKey = rKey.CreateSubKey("DefaultIcon")
                'rKeyIcon.SetValue("", """" & iconFilePath & """")
                If rKey IsNot Nothing Then rKey.Close()

                rKey = Registry.ClassesRoot.CreateSubKey(UrlProtocol & "\shell\open\command")
                rKey.SetValue("", """" & Application.ExecutablePath & """ %1")
                Log.WriteError("MEGA URI protocol added to registry.")
            Else
                Dim val As String = """" & Application.ExecutablePath & """ %1"

                Dim subKey As RegistryKey = rKey.OpenSubKey("shell\open\command", False)
                If subKey Is Nothing Then
                    subKey = Registry.ClassesRoot.CreateSubKey(UrlProtocol & "\shell\open\command")
                End If
                If subKey Is Nothing OrElse subKey.GetValue("") Is Nothing OrElse CStr(subKey.GetValue("")) <> val Then
                    If subKey IsNot Nothing Then subKey.Close()
                    subKey = Registry.ClassesRoot.OpenSubKey(UrlProtocol & "\shell\open\command", True)
                    If subKey IsNot Nothing Then subKey.SetValue("", val)
                End If
                If subKey IsNot Nothing Then subKey.Close()
            End If
            Return Nothing
        Catch ex As UnauthorizedAccessException
            Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. MEGA URI protocol could not be entered. Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.")
            Return ex

        Catch ex As Security.SecurityException
            Log.WriteError("SECURITY ERROR: Not enough privileges to access the registry. MEGA URI protocol could not be entered. Execute the application with administrator privileges (at least one time) in order to access the registry. Please note that if you move the application, you will have to execute it again with administrator privileges.")
            Return ex

        Catch ex As Exception
            Log.WriteError("Error accessing the registry for registering MEGA URI protocol. Error: " & ex.ToString)
            Return ex

        Finally
            If rKey IsNot Nothing Then rKey.Close()
        End Try
    End Function

    Public Function IsAdmin() As Boolean
        Dim id As WindowsIdentity = WindowsIdentity.GetCurrent()
        Dim p As WindowsPrincipal = New WindowsPrincipal(id)
        Return p.IsInRole(WindowsBuiltInRole.Administrator)
    End Function


End Class
