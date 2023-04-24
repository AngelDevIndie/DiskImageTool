﻿Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

Module Functions

    Public Function ByteArrayCompare(b1() As Byte, b2() As Byte, Optional IgnoreLength As Boolean = False) As Boolean
        If Not IgnoreLength AndAlso b1.Length <> b2.Length Then
            Return False
        End If

        Dim Length = Math.Min(b1.Length, b2.Length)

        For Counter = Length - 1 To 0 Step -1
            If b1(Counter) <> b2(Counter) Then
                Return False
            End If
        Next

        Return True
    End Function

    Public Function ConvertHexToBytes(Hex As String) As Byte()
        If String.IsNullOrEmpty(Hex) Then
            Return Nothing
        End If

        Hex = Hex.Trim()
        Hex = Hex.Replace(" ", "")
        Hex = Hex.Replace(Chr(13), "")
        Hex = Hex.Replace(Chr(10), "")
        Hex = Hex.Replace(Chr(9), "")

        Dim regex = New Regex("^[0-9A-F]*$", RegexOptions.IgnoreCase)

        Dim HexArray As String()
        If regex.IsMatch(Hex) Then
            If Hex.Length Mod 2 = 1 Then
                Hex = "0" & Hex
            End If
            HexArray = New String(Hex.Length / 2 - 1) {}
            For i As Integer = 0 To Hex.Length / 2 - 1
                HexArray(i) = Hex.Substring(i * 2, 2)
            Next
        Else
            Return Nothing
        End If

        Dim ByteArray = New Byte(HexArray.Length - 1) {}
        Dim b As Byte = Nothing
        For j = 0 To HexArray.Length - 1
            Dim HexValue = HexArray(j)

            If Not ConvertHexToByte(HexValue, b) Then
                Return Nothing
            End If

            ByteArray(j) = b
        Next

        Return ByteArray
    End Function

    Public Function DuplicateHashTable(Table As Hashtable) As Hashtable
        Dim NewTable As New Hashtable
        For Each Key In Table.Keys
            NewTable.Item(Key) = Table.Item(Key)
        Next

        Return NewTable
    End Function

    Public Function GetVersionString() As String
        Dim Version = FileVersionInfo.GetVersionInfo(Application.ExecutablePath)
        Return Version.FileMajorPart & "." & Version.FileMinorPart & "." & Version.FilePrivatePart
    End Function

    Public Function HexStringToBytes(ByVal HexString As String) As Byte()
        Dim b(HexString.Length / 2 - 1) As Byte

        For i As Integer = 0 To HexString.Length - 1 Step 2
            b(i / 2) = Convert.ToByte(HexString.Substring(i, 2), 16)
        Next

        Return b
    End Function

    Public Sub ListViewAddColumn(LV As ListView, Name As String, Text As String, Width As Integer, Index As Integer)
        Dim Column As New ColumnHeader With {
            .Name = Name,
            .Width = Width,
            .Text = Text
        }
        LV.Columns.Insert(Index, Column)
    End Sub

    Public Sub ListViewDoubleBuffer(lv As ListView)
        lv.GetType() _
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance Or System.Reflection.BindingFlags.NonPublic) _
            .SetValue(lv, True, Nothing)
    End Sub

    Public Function MD5Hash(Data() As Byte) As String
        Using hasher As MD5 = MD5.Create()
            Dim dbytes As Byte() = hasher.ComputeHash(Data)
            Dim sBuilder As New StringBuilder()

            For n As Integer = 0 To dbytes.Length - 1
                sBuilder.Append(dbytes(n).ToString("X2"))
            Next n

            Return sBuilder.ToString
        End Using
    End Function

    Public Function PathAddBackslash(Path As String) As String
        If Len(Path) > 0 Then
            If Not Path.EndsWith("\") Then
                Path &= "\"
            End If
        End If
        Return Path
    End Function

    Public Function SHA1Hash(Data() As Byte) As String
        Using hasher As SHA1 = SHA1.Create()
            Dim dbytes As Byte() = hasher.ComputeHash(Data)
            Dim sBuilder As New StringBuilder()

            For n As Integer = 0 To dbytes.Length - 1
                sBuilder.Append(dbytes(n).ToString("X2"))
            Next n

            Return sBuilder.ToString
        End Using
    End Function

    Private Function ConvertHexToByte(Hex As String, <Out> ByRef b As Byte) As Boolean
        Return Byte.TryParse(Hex, NumberStyles.HexNumber, Thread.CurrentThread.CurrentCulture, b)
    End Function

    <DllImport("shell32.dll")>
    Private Function SHGetKnownFolderPath(<MarshalAs(UnmanagedType.LPStruct)> ByVal rfid As Guid,
        ByVal dwFlags As UInt32,
        ByVal hToken As IntPtr,
        ByRef pszPath As IntPtr) As Int32
    End Function

    Public Function GetDownloadsFolder() As String

        Dim Result As String = ""
        Dim ppszPath As IntPtr
        Dim rfid = New Guid("{374DE290-123F-4565-9164-39C4925E467B}")

        If SHGetKnownFolderPath(rfid, 0, 0, ppszPath) = 0 Then
            Result = Marshal.PtrToStringUni(ppszPath)
            Marshal.FreeCoTaskMem(ppszPath)
        End If

        Return Result
    End Function

End Module