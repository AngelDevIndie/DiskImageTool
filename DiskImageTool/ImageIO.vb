﻿Imports System.IO
Imports System.IO.Compression
Imports DiskImageTool.DiskImage

Module ImageIO
    Public ReadOnly AllFileExtensions As New List(Of String) From {".ima", ".img", ".imz", ".vfd", ".flp"}
    Public ReadOnly ArchiveFileExtensions As New List(Of String) From {".zip"}
    Public ReadOnly BitstreamFileExtensions As New List(Of String) From {".tc", ".psi", ".mfm"}

    Public Enum SaveImageResponse
        Success
        Failed
        Unsupported
        Unknown
    End Enum

    Public Function CreateBackup(FilePath As String) As Boolean
        Try
            Dim BackupPath As String = FilePath & ".bak"
            IO.File.Copy(FilePath, BackupPath, True)
        Catch ex As Exception
            Debug.Print("Caught Exception: CreateBackup")
            Return False
        End Try

        Return True
    End Function

    Public Sub DirectoryEntryExport(FileData As FileData)
        Dim DirectoryEntry = FileData.DirectoryEntry

        Dim Dialog = New SaveFileDialog With {
            .FileName = DirectoryEntry.GetFullFileName
        }
        If Dialog.ShowDialog <> DialogResult.OK Then
            Exit Sub
        End If

        Dim Result = DirectoryEntrySaveToFile(Dialog.FileName, DirectoryEntry)

        If Not Result Then
            Dim Msg As String = $"Error saving file '{IO.Path.GetFileName(Dialog.FileName)}'."
            MsgBox(Msg, MsgBoxStyle.Critical + MsgBoxStyle.OkOnly)
        End If
    End Sub

    Public Function DirectoryEntrySaveToFile(FilePath As String, DirectoryEntry As DiskImage.DirectoryEntry) As Boolean
        Try
            IO.File.WriteAllBytes(FilePath, DirectoryEntry.GetContent)
            Dim D = DirectoryEntry.GetLastWriteDate
            If D.IsValidDate Then
                IO.File.SetLastWriteTime(FilePath, D.DateObject)
            End If
        Catch ex As Exception
            Debug.Print("Caught Exception: DirectoryEntrySaveToFile")
            Return False
        End Try

        Return True
    End Function

    Public Function DiskImageLoad(ImageData As LoadedImageData, Optional SetChecksum As Boolean = False) As DiskImage.Disk
        Dim Data() As Byte
        Dim LastChecksum As UInteger
        Dim Image As IByteArray = Nothing

        ImageData.InvalidImage = False

        If ImageData.TempPath <> "" Then
            Data = ImageLoadFromTemp(ImageData.TempPath)
            Image = New ByteArray(Data)
        End If

        If Image Is Nothing AndAlso File.Exists(ImageData.SourceFile) Then
            Try
                If ImageData.Compressed Then
                    ImageData.ReadOnly = True
                    Data = OpenFileFromZIP(ImageData.SourceFile, ImageData.CompressedFile)
                Else
                    ImageData.ReadOnly = IsFileReadOnly(ImageData.SourceFile)
                    Data = IO.File.ReadAllBytes(ImageData.SourceFile)
                End If

                Dim FloppyImageType = GetImageTypeFromFileName(ImageData.FileName)

                LastChecksum = ImageData.Checksum
                If SetChecksum Then
                    ImageData.Checksum = Crc32.ComputeChecksum(Data)
                End If

                If FloppyImageType = FloppyImageType.TranscopyImage Then
                    Dim TCImage = ImageFormats.TC.TranscopyImageLoad(Data)
                    If TCImage IsNot Nothing Then
                        Image = TCImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.PSIImage Then
                    Dim PSIImage = ImageFormats.PSI.PSIImageLoad(Data)
                    If PSIImage IsNot Nothing Then
                        Image = PSIImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.MFMImage Then
                    Dim MFMImage = ImageFormats.MFM.MFMImageLoad(Data)
                    If MFMImage IsNot Nothing Then
                        Image = MFMImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                Else
                    Image = New ByteArray(Data)
                End If
                'If ImageData.XDFMiniDisk Then
                '    ImageData.ReadOnly = True
                '    Dim NewData(ImageData.XDFLength - 1) As Byte
                '    Array.Copy(Data, ImageData.XDFOffset, NewData, 0, ImageData.XDFLength)
                '    Data = NewData
                'End If
            Catch ex As Exception
                Debug.Print("Caught Exception: Functions.DiskImageLoad")
                Image = Nothing
            End Try
        End If

        If Image Is Nothing Then
            Return Nothing
        Else
            If ImageData.Loaded AndAlso ImageData.Checksum <> LastChecksum Then
                If ImageData.Modifications IsNot Nothing AndAlso ImageData.Modifications.Count > 0 Then
                    ImageData.Modifications.Clear()
                    ImageData.ExternalModified = True
                ElseIf SetChecksum Then
                    ImageData.ExternalModified = False
                End If
            End If
            If SetChecksum Then
                ImageData.Loaded = True
            End If

            Dim Disk = New DiskImage.Disk(Image, ImageData.FATIndex, ImageData.Modifications)
            ImageData.Modifications = Disk.Image.Changes

            Return Disk
        End If
    End Function

    Public Sub EmptyTempPath()
        Dim TempPath = Path.Combine(Path.GetTempPath(), "DiskImageTool")

        If Directory.Exists(TempPath) Then
            Try
                For Each File In Directory.EnumerateFiles(TempPath)
                    Try
                        IO.File.Delete(File)
                    Catch ex As Exception
                        Debug.Print("Caught Exception: EmptyTempPath")
                    End Try
                Next
            Catch ex As Exception
                Debug.Print("Caught Exception: EmptyTempPath")
            End Try
        End If
    End Sub

    Public Function GetLoadDialogFilters() As String
        Dim FileFilter As String
        Dim ExtensionList As List(Of String)


        FileFilter = FileDialogGetFilter("All Floppy Disk Images", AllFileExtensions)

        ExtensionList = New List(Of String) From {".imz"}
        FileFilter = FileDialogAppendFilter(FileFilter, "Compressed Disk Image", ExtensionList)

        ExtensionList = New List(Of String) From {".vfd", ".flp"}
        FileFilter = FileDialogAppendFilter(FileFilter, "Virtual Floppy Disk", ExtensionList)

        ExtensionList = New List(Of String) From {".tc"}
        FileFilter = FileDialogAppendFilter(FileFilter, "Transcopy Image", ExtensionList)

        ExtensionList = New List(Of String) From {".psi"}
        FileFilter = FileDialogAppendFilter(FileFilter, "PCE sector image", ExtensionList)

        ExtensionList = New List(Of String) From {".mfm"}
        FileFilter = FileDialogAppendFilter(FileFilter, "HXC MFM image", ExtensionList)

        FileFilter = FileDialogAppendFilter(FileFilter, "Zip Archive", ".zip")
        FileFilter = FileDialogAppendFilter(FileFilter, "All files", ".*")

        Return FileFilter
    End Function

    Public Function GetSaveDialogFilters(DiskFormat As FloppyDiskFormat, ImageType As FloppyImageType, FileExt As String) As SaveDialogFilter
        Dim Response As SaveDialogFilter
        Dim CurrentIndex As Integer = 1
        Dim Extension As String
        Dim ExtensionList As List(Of String)

        Response.FilterIndex = 0

        ExtensionList = New List(Of String) From {".ima", ".img"}
        For Each Extension In ExtensionList
            If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                Response.FilterIndex = CurrentIndex
            End If
        Next
        Response.Filter = FileDialogGetFilter("Floppy Disk Image", ExtensionList)
        CurrentIndex += 1

        ExtensionList = New List(Of String) From {".vfd", ".flp"}
        For Each Extension In ExtensionList
            If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                Response.FilterIndex = CurrentIndex
            End If
        Next
        Response.Filter = FileDialogAppendFilter(Response.Filter, "Virtual Floppy Disk", ExtensionList)
        CurrentIndex += 1

        ExtensionList = New List(Of String) From {".psi"}
        For Each Extension In ExtensionList
            If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                Response.FilterIndex = CurrentIndex
            End If
        Next
        Response.Filter = FileDialogAppendFilter(Response.Filter, "PCE Sector Image", ExtensionList)
        CurrentIndex += 1

        If ImageType <> FloppyImageType.PSIImage Then
            ExtensionList = New List(Of String) From {".tc"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "Transcopy Image", ExtensionList)
            CurrentIndex += 1
        End If

        ExtensionList = New List(Of String) From {".mfm"}
        For Each Extension In ExtensionList
            If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                Response.FilterIndex = CurrentIndex
            End If
        Next
        Response.Filter = FileDialogAppendFilter(Response.Filter, "HXC MFM Image", ExtensionList)
        CurrentIndex += 1


        Dim Items = System.Enum.GetValues(GetType(FloppyDiskFormat))
        For Each Item As Integer In Items
            Extension = GetImageFileExtensionByFormat(Item)
            If Extension <> "" Then
                Dim Description = GetFileFilterDescriptionByFormat(Item)
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.Filter = FileDialogAppendFilter(Response.Filter, Description, Extension)
                    Response.FilterIndex = CurrentIndex
                    CurrentIndex += 1
                ElseIf Item = DiskFormat Then
                    Response.Filter = FileDialogAppendFilter(Response.Filter, Description, Extension)
                    CurrentIndex += 1
                End If
            End If
        Next

        Response.Filter = FileDialogAppendFilter(Response.Filter, "All files", ".*")
        If Response.FilterIndex = 0 Then
            Response.FilterIndex = CurrentIndex
        End If

        Return Response
    End Function

    Public Function ImageLoadFromTemp(FilePath As String) As Byte()
        Dim Data() As Byte = Nothing

        If File.Exists(FilePath) Then
            Try
                Data = IO.File.ReadAllBytes(FilePath)
            Catch ex As Exception
                Debug.Print("Caught Exception: ImageLoadFromTemp")
                Data = Nothing
            End Try
        End If

        Return Data
    End Function

    Public Function ImageSaveToTemp(Data() As Byte, DisplayPath As String) As String
        Dim TempPath = Path.Combine(Path.GetTempPath(), "DiskImageTool")
        Dim TempFileName = HashFunctions.SHA1Hash(System.Text.Encoding.Unicode.GetBytes(DisplayPath)) & ".tmp"

        Try
            If Not Directory.Exists(TempPath) Then
                Directory.CreateDirectory(TempPath)
            End If
            TempPath = Path.Combine(TempPath, TempFileName)

            IO.File.WriteAllBytes(TempPath, Data)
        Catch ex As Exception
            Debug.Print("Caught Exception: ImageSaveToTemp")
            TempPath = ""
        End Try

        Return TempPath
    End Function

    Public Sub InitAllFileExtensions()
        Dim Items = System.Enum.GetValues(GetType(FloppyDiskFormat))
        For Each Item As Integer In Items
            Dim FileExt = GetImageFileExtensionByFormat(Item)
            If FileExt <> "" Then
                If Not AllFileExtensions.Contains(FileExt) Then
                    AllFileExtensions.Add(FileExt)
                End If
            End If
        Next
        For Each Item In BitstreamFileExtensions
            If Not AllFileExtensions.Contains(Item) Then
                AllFileExtensions.Add(Item)
            End If
        Next
    End Sub

    Public Function OpenFileFromZIP(ZipFileName As String, FileName As String) As Byte()
        Dim Data As New MemoryStream()
        Dim Archive As ZipArchive = ZipFile.OpenRead(ZipFileName)
        Dim Entry = Archive.GetEntry(FileName)
        If Entry IsNot Nothing Then
            Entry.Open.CopyTo(Data)
            Return Data.ToArray
        Else
            Return Nothing
        End If
    End Function

    Public Function SaveDiskImageToFile(Disk As DiskImage.Disk, FilePath As String) As SaveImageResponse
        Dim FileImageType = GetImageTypeFromFileName(FilePath)
        Dim DiskImageType = Disk.Image.Data.ImageType
        Dim Response As SaveImageResponse = SaveImageResponse.Failed
        Dim Result As Boolean = False

        If FileImageType = DiskImageType Then
            Result = Disk.Image.Data.SaveToFile(FilePath)

        ElseIf FileImageType = FloppyImageType.BasicSectorImage Then
            Dim Data = Disk.Image.Data.GetBytes()
            Result = SaveByteArrayToFile(FilePath, Data)

        ElseIf DiskImageType = FloppyImageType.BasicSectorImage Then
            If IsDiskFormatValidForRead(Disk.DiskFormat) Then
                Dim Data = Disk.Image.Data.GetBytes()

                If FileImageType = FloppyImageType.TranscopyImage Then
                    Dim Transcopy = ImageFormats.BasicSectorToTranscopyImage(Data, Disk.DiskFormat)
                    Result = Transcopy.Export(FilePath, False)
                ElseIf FileImageType = FloppyImageType.PSIImage Then
                    Dim PSI = ImageFormats.BasicSectorToPSIImage(Data, Disk.DiskFormat)
                    Result = PSI.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.MFMImage Then
                    Dim MFM = ImageFormats.BasicSectorToMFMImage(Data, Disk.DiskFormat)
                    Result = MFM.Export(FilePath, False)
                Else
                    Return SaveImageResponse.Unsupported
                End If
            Else
                Return SaveImageResponse.Unknown
            End If

        ElseIf FileImageType = FloppyImageType.TranscopyImage Then
            If DiskImageType = FloppyImageType.MFMImage Then
                Dim MFMByteArray = DirectCast(Disk.Image.Data, ImageFormats.MFM.MFMByteArray)
                Dim Transcopy = ImageFormats.BitstreamToTranscopyImage(MFMByteArray.Image)
                Result = Transcopy.Export(FilePath, False)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.MFMImage Then
            If DiskImageType = FloppyImageType.TranscopyImage Then
                Dim TranscopyByteArray = DirectCast(Disk.Image.Data, ImageFormats.TC.TranscopyByteArray)
                Dim MFM = ImageFormats.BitstreamToMFMImage(TranscopyByteArray.Image)
                Result = MFM.Export(FilePath, False)
            Else
                Return SaveImageResponse.Unsupported
            End If


        ElseIf FileImageType = FloppyImageType.PSIImage Then
            If DiskImageType = FloppyImageType.TranscopyImage Then
                Dim TranscopyByteArray = DirectCast(Disk.Image.Data, ImageFormats.TC.TranscopyByteArray)
                Dim PSI = ImageFormats.BitstreamToPSIImage(TranscopyByteArray.Image)
                Result = PSI.Export(FilePath)

            ElseIf DiskImageType = FloppyImageType.MFMImage Then
                Dim MFMByteArray = DirectCast(Disk.Image.Data, ImageFormats.MFM.MFMByteArray)
                Dim PSI = ImageFormats.BitstreamToPSIImage(MFMByteArray.Image)
                Result = PSI.Export(FilePath)

            Else
                Return SaveImageResponse.Unsupported
            End If

        Else
            Return SaveImageResponse.Unsupported
        End If

        If Result Then
            Disk.ClearChanges()
            Response = SaveImageResponse.Success
        End If

        Return Response
    End Function
End Module
