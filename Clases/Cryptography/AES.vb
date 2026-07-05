Imports System.Security.Cryptography
Imports System.IO
Imports System.Text

Namespace Cryptography
    Public Class AES

        Private Const SALT_SIZE As Integer = 4

        Private Const SALT_ITERATIONS As Integer = 25997  ' Chosen because it is prime :P

        Private Shared SALT_BYTES As Byte() = New Byte() {CByte(236), CByte(148), CByte(113), CByte(216), _
                                                            CByte(75), CByte(18), CByte(129), CByte(6), _
                                                            CByte(250), CByte(85), CByte(24), CByte(243)}



        Public Function AES_Encrypt(bytesToBeEncrypted As Byte(), passwordBytes As Byte()) As Byte()
            Dim encryptedBytes As Byte() = Nothing

         

            Using ms As New MemoryStream()
                Using AES As New RijndaelManaged()
                    AES.KeySize = 256
                    AES.BlockSize = 128

                    Dim key = New Rfc2898DeriveBytes(passwordBytes, SALT_BYTES, SALT_ITERATIONS)
                    AES.Key = key.GetBytes(CInt(AES.KeySize / 8))
                    AES.IV = GetRandomBytes(CInt(AES.BlockSize / 8))

                    AES.Mode = CipherMode.CBC

                    Using cs = New CryptoStream(ms, AES.CreateEncryptor(), CryptoStreamMode.Write)
                        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length)
                        cs.Close()
                    End Using
                    encryptedBytes = AES.IV.Concat(ms.ToArray).ToArray
                End Using
            End Using

            Return encryptedBytes
        End Function

        Public Function AES_Decrypt(bytesToBeDecrypted As Byte(), passwordBytes As Byte()) As Byte()
            Dim decryptedBytes As Byte() = Nothing


            Using ms As New MemoryStream()
                Using AES As New RijndaelManaged()
                    AES.KeySize = 256
                    AES.BlockSize = 128

                    Dim key = New Rfc2898DeriveBytes(passwordBytes, SALT_BYTES, SALT_ITERATIONS)
                    AES.Key = key.GetBytes(CInt(AES.KeySize / 8))
                    AES.IV = bytesToBeDecrypted.Take(CInt(AES.BlockSize / 8)).ToArray

                    AES.Mode = CipherMode.CBC

                    Dim bytes As Byte() = bytesToBeDecrypted.Skip(CInt(AES.BlockSize / 8)).ToArray

                    Using cs = New CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write)
                        cs.Write(bytes, 0, bytes.Length)
                        cs.Close()
                    End Using
                    decryptedBytes = ms.ToArray()
                End Using
            End Using

            Return decryptedBytes
        End Function

        Public Function Encrypt(text As String, pwd As String) As String
            Dim originalBytes As Byte() = Encoding.UTF8.GetBytes(text)
            Dim encryptedBytes As Byte() = Nothing
            Dim passwordBytes As Byte() = Encoding.UTF8.GetBytes(pwd)

            passwordBytes = SHA256.Create().ComputeHash(passwordBytes)
            Dim saltBytes As Byte() = GetRandomBytes(SALT_SIZE)

            Dim bytesToBeEncrypted As Byte() = saltBytes.Concat(originalBytes).ToArray

            encryptedBytes = AES_Encrypt(bytesToBeEncrypted, passwordBytes)

            Return Convert.ToBase64String(encryptedBytes)
        End Function

        Public Function Decrypt(decryptedText As String, pwd As String) As String
            Dim bytesToBeDecrypted As Byte() = Convert.FromBase64String(decryptedText)
            Dim passwordBytes As Byte() = Encoding.UTF8.GetBytes(pwd)

            passwordBytes = SHA256.Create().ComputeHash(passwordBytes)

            Dim decryptedBytes As Byte() = AES_Decrypt(bytesToBeDecrypted, passwordBytes)

            Return Encoding.UTF8.GetString(decryptedBytes.Skip(SALT_SIZE).ToArray)
        End Function

        Public Function GetRandomBytes(numberOfBytes As Integer) As Byte()

            Dim ba As Byte() = New Byte(numberOfBytes - 1) {}
            RNGCryptoServiceProvider.Create().GetBytes(ba)
            Return ba
        End Function
    End Class
End Namespace

