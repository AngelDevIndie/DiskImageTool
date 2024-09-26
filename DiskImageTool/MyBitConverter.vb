﻿Public Class MyBitConverter
    Public Shared Function BytesToBits(bytes() As Byte) As BitArray
        Dim buffer(bytes.Length - 1) As Byte

        For i As Integer = 0 To bytes.Length - 1
            buffer(i) = ReverseBits(bytes(i))
        Next

        Return New BitArray(buffer)
    End Function

    Public Shared Function BytesToBits(bytes() As Byte, Offset As UInteger, Length As UInteger) As BitArray
        Dim buffer(Length - 1) As Byte

        For i As Integer = 0 To Length - 1
            buffer(i) = ReverseBits(bytes(Offset + i))
        Next

        Return New BitArray(buffer)
    End Function

    Public Shared Function BitsToBytes(Bitstream As BitArray) As Byte()
        Dim Length = Bitstream.Length \ 8
        Dim PaddedLength = Math.Ceiling(Length / 256) * 256
        Dim Diff = (PaddedLength - Length) * 8

        Dim buffer(PaddedLength - 1) As Byte

        If Diff > 0 Then
            Dim Offset = Bitstream.Length
            Bitstream.Length = Bitstream.Length + Diff
            For i = Offset To Offset + Diff - 1
                Bitstream(i) = Bitstream(i - Offset)
            Next
        End If

        Bitstream.CopyTo(buffer, 0)

        For i As Integer = 0 To buffer.Length - 1
            buffer(i) = ReverseBits(buffer(i))
        Next

        Return buffer
    End Function

    Public Shared Function ReverseBits(b As Byte) As Byte
        b = ((b >> 1) And &H55) Or ((b << 1) And &HAA) ' Swap odd and even bits
        b = ((b >> 2) And &H33) Or ((b << 2) And &HCC) ' Swap consecutive pairs
        b = ((b >> 4) And &HF) Or ((b << 4) And &HF0)  ' Swap nibbles

        Return b
    End Function

    Public Shared Function SwapEndian(value As UInt16) As UInt16
        Return CUShort((value >> 8) Or (value << 8))
    End Function

    Public Shared Function SwapEndian(value As UInt32) As UInt32
        ' Manually reorder the bytes to convert to Big Endian
        Dim byte1 As UInt32 = (value And &HFF) << 24        ' Get the lowest byte and shift it to the highest byte position
        Dim byte2 As UInt32 = (value And &HFF00) << 8       ' Get the second lowest byte and shift it
        Dim byte3 As UInt32 = (value And &HFF0000) >> 8     ' Get the second highest byte and shift it
        Dim byte4 As UInt32 = (value And &HFF000000UI) >> 24 ' Get the highest byte and shift it to the lowest byte position

        ' Combine the bytes into the final Big Endian UInt32 value
        Return byte1 Or byte2 Or byte3 Or byte4
    End Function

    Public Shared Function ToInt16(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As Int16
        If bigEndian Then
            Return ToInt16(ToUInt16(value, bigEndian, startIndex))
        Else
            Return BitConverter.ToInt16(value, startIndex)
        End If
    End Function

    Public Shared Function ToInt24(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As Int32
        Return ToInt24(ToUInt24(value, bigEndian, startIndex))
    End Function

    Public Shared Function ToInt32(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As Int32
        If bigEndian Then
            Return ToInt32(ToUInt32(value, bigEndian, startIndex))
        Else
            Return BitConverter.ToInt32(value, startIndex)
        End If
    End Function

    Public Shared Function ToInt64(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As Int64
        If bigEndian Then
            Return ToInt64(ToUInt64(value, bigEndian, startIndex))
        Else
            Return BitConverter.ToInt64(value, startIndex)
        End If
    End Function

    Public Shared Function ToByte(value As SByte) As Byte
        If value < 0 Then
            Return value + (2 ^ 8)
        Else
            Return value
        End If
    End Function

    Public Shared Function ToSByte(value As Byte) As SByte
        If value < (2 ^ 8 / 2) Then
            Return value
        Else
            Return value - (2 ^ 8)
        End If
    End Function

    Public Shared Function ToInt16(value As UInt16) As Int16
        If value < (2 ^ 16 / 2) Then
            Return value
        Else
            Return value - (2 ^ 16)
        End If
    End Function

    Public Shared Function ToInt24(value As UInt32) As Int32
        If value < (2 ^ 24 / 2) Then
            Return value
        Else
            Return value - (2 ^ 24)
        End If
    End Function

    Public Shared Function ToInt32(value As UInt32) As Int32
        If value < (2 ^ 32 / 2) Then
            Return value
        Else
            Return value - (2 ^ 32)
        End If
    End Function

    Public Shared Function ToInt64(value As UInt64) As Int64
        Return BitConverter.ToInt64(BitConverter.GetBytes(value), 0)
    End Function

    Public Shared Function ToUInt16(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As UInt16
        If bigEndian Then
            Return CType(value(startIndex), UInt16) << 8 Or value(startIndex + 1)
        Else
            Return BitConverter.ToUInt16(value, startIndex)
        End If
    End Function

    Public Shared Function ToUInt24(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As UInt32
        If bigEndian Then
            Return CType(value(startIndex), UInt32) << 16 Or CType(value(startIndex + 1), UInt16) << 8 Or value(startIndex + 2)
        Else
            Return CType(value(startIndex + 2), UInt32) << 16 Or CType(value(startIndex + 1), UInt16) << 8 Or value(startIndex)
        End If
    End Function

    Public Shared Function ToUInt32(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As UInt32
        If bigEndian Then
            Return CType(value(startIndex), UInt32) << 24 Or CType(value(startIndex + 1), UInt32) << 16 Or CType(value(startIndex + 2), UInt16) << 8 Or value(startIndex + 3)
        Else
            Return BitConverter.ToUInt32(value, startIndex)
        End If
    End Function

    Public Shared Function ToUInt64(value As Byte(), bigEndian As Boolean, Optional startIndex As Integer = 0) As UInt64
        If bigEndian Then
            Dim num As UInt64 = CType(value(startIndex), UInt32) << 24 Or CType(value(startIndex + 1), UInt32) << 16 Or CType(value(startIndex + 2), UInt16) << 8 Or value(startIndex + 3)
            Dim num2 As UInt32 = CType(value(startIndex + 4), UInt32) << 24 Or CType(value(startIndex + 5), UInt32) << 16 Or CType(value(startIndex + 6), UInt16) << 8 Or value(startIndex + 7)

            Return num2 Or num << 32
        Else
            Return BitConverter.ToUInt64(value, startIndex)
        End If
    End Function
End Class
