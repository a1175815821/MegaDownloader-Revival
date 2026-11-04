Imports System.Text
Imports System.Globalization

Public Class Utils

    Friend Shared Function TryParseSpeedLimit(value As String, ByRef kilobytesPerSecond As Integer) As Boolean
        kilobytesPerSecond = 0
        Dim megabytesPerSecond As Double
        ' B5: the parameterless Double.TryParse implicitly allows group separators.
        ' In de-DE/es-ES "1.5" becomes 15 (a silent 10x speed limit), while in fr-FR
        ' the same input fails outright. Parse with an explicit style that never accepts
        ' thousands separators, first in the UI culture (matches the "0.####" echo) and
        ' then in the invariant culture (accepts the dot form regardless of locale).
        Dim styles As NumberStyles = NumberStyles.Float
        If Not Double.TryParse(value, styles, CultureInfo.CurrentCulture, megabytesPerSecond) AndAlso
           Not Double.TryParse(value, styles, CultureInfo.InvariantCulture, megabytesPerSecond) Then Return False
        If Double.IsNaN(megabytesPerSecond) OrElse
           Double.IsInfinity(megabytesPerSecond) OrElse megabytesPerSecond <= 0 OrElse
           megabytesPerSecond > Integer.MaxValue / 1024.0R Then Return False
        Dim rounded As Double = Math.Round(megabytesPerSecond * 1024.0R)
        If rounded < 1 Then Return False
        kilobytesPerSecond = CInt(rounded)
        Return True
    End Function

    ' Used only in in-memory save comparisons; randomized ciphertext stays on disk.
    Friend Shared Function SecretFingerprint(value As String) As String
        If String.IsNullOrEmpty(value) Then Return String.Empty
        Using hash As Security.Cryptography.SHA256 = Security.Cryptography.SHA256.Create()
            Return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)))
        End Using
    End Function

    Friend Shared Function RemoveDiacritics(stIn As String) As String

        If String.IsNullOrEmpty(stIn) Then Return stIn

        Dim stFormD As String = stIn.Normalize(NormalizationForm.FormD)
        Dim sb As New StringBuilder()

        For ich As Integer = 0 To stFormD.Length - 1
            Dim uc As UnicodeCategory = CharUnicodeInfo.GetUnicodeCategory(stFormD(ich))
            If uc <> UnicodeCategory.NonSpacingMark Then
                sb.Append(stFormD(ich))
            End If
        Next

        Return (sb.ToString().Normalize(NormalizationForm.FormC))
    End Function


End Class
