Imports System.Text
Imports System.Globalization

Public Class Utils

    Friend Shared Function FormatSpeedLimit(kilobytesPerSecond As Integer) As String
        Return (kilobytesPerSecond / 1024.0R).ToString("0.####", CultureInfo.CurrentCulture)
    End Function

    Friend Shared Function TryParseSpeedLimit(value As String, ByRef kilobytesPerSecond As Integer) As Boolean
        kilobytesPerSecond = 0
        Dim megabytesPerSecond As Double
        ' B5: the parameterless Double.TryParse implicitly allows group separators.
        ' In de-DE/es-ES "1.5" becomes 15 (a silent 10x speed limit), while in fr-FR
        ' the same input fails outright. Parse with an explicit style that never accepts
        ' thousands separators, first in the UI culture (keeps the local decimal comma)
        ' and then in the invariant culture for unambiguous typed dot decimals.
        Dim styles As NumberStyles = NumberStyles.Float
        Dim culture As CultureInfo = CultureInfo.CurrentCulture
        If Not Double.TryParse(value, styles, culture, megabytesPerSecond) Then
            ' Final fix: the invariant fallback must not reinterpret a locale group
            ' separator as a decimal point. In de-DE/es-ES "1.500" used to mean
            ' 1500 MB/s and would silently become 1.5 MB/s (1000x too small).
            ' Reject the classic group shape (separator + exactly three digits) so
            ' the user gets a clear error instead; a plain dot decimal ("1.5")
            ' and a four-digit fraction ("1.5000") still round-trip correctly.
            Dim groupSeparator As String = culture.NumberFormat.NumberGroupSeparator
            If Not String.IsNullOrEmpty(groupSeparator) AndAlso
               HasGroupedDigits(value, groupSeparator) Then Return False
            If Not Double.TryParse(value, styles, CultureInfo.InvariantCulture, megabytesPerSecond) Then Return False
        End If
        If Double.IsNaN(megabytesPerSecond) OrElse
           Double.IsInfinity(megabytesPerSecond) OrElse megabytesPerSecond <= 0 OrElse
           megabytesPerSecond > Integer.MaxValue / 1024.0R Then Return False
        Dim rounded As Double = Math.Round(megabytesPerSecond * 1024.0R)
        If rounded < 1 Then Return False
        kilobytesPerSecond = CInt(rounded)
        Return True
    End Function

    ''' <summary>True when the text looks like a thousands-grouped number in the
    ''' current culture (for example "1.500" in de-DE or "1,500" in en-US).</summary>
    Private Shared Function HasGroupedDigits(value As String, separator As String) As Boolean
        If String.IsNullOrEmpty(value) Then Return False
        Dim index As Integer = value.IndexOf(separator, StringComparison.Ordinal)
        While index >= 0
            Dim start As Integer = index + separator.Length
            Dim digits As Integer = 0
            While start + digits < value.Length AndAlso digits < 4 AndAlso
                  Char.IsDigit(value(start + digits))
                digits += 1
            End While
            If digits = 3 AndAlso
               (start + 3 = value.Length OrElse Not Char.IsDigit(value(start + 3))) Then Return True
            index = value.IndexOf(separator, start, StringComparison.Ordinal)
        End While
        Return False
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
