''' <summary>
''' Criptografia 回归用例：AES 回环/非法输入契约、base64url 回环、SecureString 回环、DPAPI 回环。
''' MetaMAC 需要真实 MEGA 文件向量，暂不覆盖（后续补 RAR/7Z/ZIP 解压向量时一并加）。
''' </summary>
Public Module CriptografiaTests

    Public Sub Run()
        ' AES 回环：短密钥走 X 填充路径，长密钥走截断路径（默认随机 IV 下仍须回环）
        Dim keyShort As String = "TestKey123"
        Dim keyLong As String = "0123456789ABCDEFGHIJ0123456789ABCDEFGHIJ"
        Dim plain As String = "HelloProxyPassword123"
        Runner.Check(Global.MegaDownloader.Criptografia.AES_DecryptString(Global.MegaDownloader.Criptografia.AES_EncryptString(plain, keyShort), keyShort) = plain, "AES_Roundtrip_ShortKey")
        Runner.Check(Global.MegaDownloader.Criptografia.AES_DecryptString(Global.MegaDownloader.Criptografia.AES_EncryptString(plain, keyLong), keyLong) = plain, "AES_Roundtrip_LongKey")

        ' 非法输入契约：返回空字串，不抛异常（调用方依赖此行为做旧值回退）
        Runner.Check(Global.MegaDownloader.Criptografia.AES_DecryptString("!!!not-base64!!!", keyShort) = String.Empty, "AES_DecryptGarbage_Empty")
        Runner.Check(Global.MegaDownloader.Criptografia.AES_DecryptString("", keyShort) = String.Empty, "AES_DecryptEmpty_Empty")

        ' base64url 回环（含高位字节）
        Dim bytes() As Byte = New Byte() {1, 2, 3, 250, 255}
        Dim enc As String = Global.MegaDownloader.Criptografia.base64urlencode(bytes)
        Runner.Check(Global.MegaDownloader.Criptografia.base64urldecodeBytes(enc).SequenceEqual(bytes), "Base64Url_Roundtrip")

        ' SecureString 回环
        Runner.Check(Global.MegaDownloader.Criptografia.ToInsecureString(Global.MegaDownloader.Criptografia.ToSecureString("abc123")) = "abc123", "SecureString_Roundtrip")

        ' DPAPI 回环（CurrentUser 绑定；非 Windows 平台跳过）
        Try
            Dim cipher As String = Global.MegaDownloader.Criptografia.EncryptString_DPAPI(Global.MegaDownloader.Criptografia.ToSecureString("dpapi-secret"))
            Dim back As String = Global.MegaDownloader.Criptografia.ToInsecureString(Global.MegaDownloader.Criptografia.DecryptString_DPAPI(cipher))
            Runner.Check(back = "dpapi-secret", "DPAPI_Roundtrip")
        Catch ex As PlatformNotSupportedException
            Runner.Skip("DPAPI_Roundtrip", "non-Windows")
        End Try
    End Sub

End Module
