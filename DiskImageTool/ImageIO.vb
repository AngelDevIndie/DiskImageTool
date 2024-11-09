﻿Imports System.IO
Imports DiskImageTool.Bitstream
Imports DiskImageTool.DiskImage

Module ImageIO
    Public ReadOnly AllFileExtensions As New List(Of String)
    Public ReadOnly BasicSectorFileExtensions As New List(Of String) From {".ima", ".img", ".imz", ".vfd", ".flp"}
    Public ReadOnly ArchiveFileExtensions As New List(Of String) From {".zip"}
    Public ReadOnly BitstreamFileExtensions As New List(Of String) From {".86f", ".hfe", ".mfm", ".pri", ".tc"}
    Public ReadOnly AdvancedSectorFileExtensions As New List(Of String) From {".imd", ".psi"}

    Private Enum FloppyImageGroup
        BasicSectorImage
        AdvancedSectorImage
        BitstreamImage
    End Enum

    Public Enum SaveImageResponse
        Success
        Failed
        Unsupported
        Unknown
        Cancelled
    End Enum

    Public Function CreateBackup(FilePath As String) As Boolean
        Try
            Dim BackupPath As String = FilePath & ".bak"
            IO.File.Copy(FilePath, BackupPath, True)
        Catch ex As Exception
            DebugException(ex)
            Return False
        End Try

        Return True
    End Function

    Public Sub DirectoryEntryExport(FileData As FileData)
        Dim DirectoryEntry = FileData.DirectoryEntry

        Dim Dialog = New SaveFileDialog With {
            .FileName = CleanFileName(DirectoryEntry.GetFullFileName)
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

            D = DirectoryEntry.GetCreationDate
            If D.IsValidDate Then
                IO.File.SetCreationTime(FilePath, D.DateObject)
            End If

            D = DirectoryEntry.GetLastAccessDate
            If D.IsValidDate Then
                IO.File.SetLastAccessTime(FilePath, D.DateObject)
            End If
        Catch ex As Exception
            DebugException(ex)
            Return False
        End Try

        Return True
    End Function

    Public Function DiskImageLoad(ImageData As ImageData, Optional SetChecksum As Boolean = False) As DiskImage.Disk
        Dim Data() As Byte
        Dim LastChecksum As UInteger
        Dim FloppyImage As IFloppyImage = Nothing

        ImageData.InvalidImage = False

        If FloppyImage Is Nothing AndAlso IO.File.Exists(ImageData.SourceFile) Then
            Try
                If ImageData.Compressed Then
                    ImageData.ReadOnly = True
                    Data = OpenFileFromZIP(ImageData.SourceFile, ImageData.CompressedFile)
                Else
                    ImageData.ReadOnly = IsFileReadOnly(ImageData.SourceFile)
                    Data = IO.File.ReadAllBytes(ImageData.SourceFile)
                End If

                Dim FloppyImageType = GetImageTypeFromHeader(Data)
                'Dim FloppyImageType = GetImageTypeFromFileName(ImageData.FileName)

                LastChecksum = ImageData.Checksum
                If SetChecksum Then
                    ImageData.Checksum = CRC32.ComputeChecksum(Data)
                End If

                If FloppyImageType = FloppyImageType.TranscopyImage Then
                    Dim TCImage = ImageFormats.TC.ImageLoad(Data)
                    If TCImage IsNot Nothing Then
                        FloppyImage = TCImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.PSIImage Then
                    Dim PSIImage = ImageFormats.PSI.ImageLoad(Data)
                    If PSIImage IsNot Nothing Then
                        FloppyImage = PSIImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.PRIImage Then
                    Dim PRIImage = ImageFormats.PRI.ImageLoad(Data)
                    If PRIImage IsNot Nothing Then
                        FloppyImage = PRIImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.IMDImage Then
                    Dim IMDImage = ImageFormats.IMD.ImageLoad(Data)
                    If IMDImage IsNot Nothing Then
                        FloppyImage = IMDImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.MFMImage Then
                    Dim MFMImage = ImageFormats.MFM.ImageLoad(Data)
                    If MFMImage IsNot Nothing Then
                        FloppyImage = MFMImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.D86FImage Then
                    Dim D86FImage = ImageFormats.D86F.ImageLoad(Data)
                    If D86FImage IsNot Nothing Then
                        FloppyImage = D86FImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                ElseIf FloppyImageType = FloppyImageType.HFEImage Then
                    Dim HFEImage = ImageFormats.HFE.ImageLoad(Data)
                    If HFEImage IsNot Nothing Then
                        FloppyImage = HFEImage
                    Else
                        ImageData.InvalidImage = True
                    End If

                Else
                    FloppyImage = New BasicSectorImage(Data)
                End If
                'If ImageData.XDFMiniDisk Then
                '    ImageData.ReadOnly = True
                '    Dim NewData(ImageData.XDFLength - 1) As Byte
                '    Array.Copy(Data, ImageData.XDFOffset, NewData, 0, ImageData.XDFLength)
                '    Data = NewData
                'End If
            Catch ex As Exception
                DebugException(ex)
                FloppyImage = Nothing
            End Try
        End If

        If FloppyImage Is Nothing Then
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

            Dim Disk = New DiskImage.Disk(FloppyImage, ImageData.FATIndex, ImageData.Modifications)
            ImageData.Modifications = Disk.Image.History.Changes

            Return Disk
        End If
    End Function

    Public Sub EmptyTempPath()
        Dim TempPath = IO.Path.Combine(IO.Path.GetTempPath(), "DiskImageTool")

        If IO.Directory.Exists(TempPath) Then
            Try
                For Each File In IO.Directory.EnumerateFiles(TempPath)
                    Try
                        IO.File.Delete(File)
                    Catch ex As Exception
                        DebugException(ex)
                    End Try
                Next
            Catch ex As Exception
                DebugException(ex)
            End Try
        End If
    End Sub

    Public Function GetLoadDialogFilters() As String
        Dim FileFilter As String
        Dim ExtensionList As List(Of String)

        FileFilter = FileDialogGetFilter("All Floppy Disk Images", AllFileExtensions)

        FileFilter = FileDialogAppendFilter(FileFilter, "Basic Sector Image", BasicSectorFileExtensions)

        FileFilter = FileDialogAppendFilter(FileFilter, "Advanced Sector Image", AdvancedSectorFileExtensions)

        FileFilter = FileDialogAppendFilter(FileFilter, "Bitstream Image", BitstreamFileExtensions)

        ExtensionList = New List(Of String) From {".imz"}
        FileFilter = FileDialogAppendFilter(FileFilter, "WinImage Compressed Disk Image", ExtensionList)

        ExtensionList = New List(Of String) From {".vfd", ".flp"}
        FileFilter = FileDialogAppendFilter(FileFilter, "Virtual Floppy Disk", ExtensionList)

        ExtensionList = New List(Of String) From {".imd"}
        FileFilter = FileDialogAppendFilter(FileFilter, "ImageDisk Sector Image", ExtensionList)

        ExtensionList = New List(Of String) From {".pri"}
        FileFilter = FileDialogAppendFilter(FileFilter, "PCE Bitstream Image", ExtensionList)

        ExtensionList = New List(Of String) From {".psi"}
        FileFilter = FileDialogAppendFilter(FileFilter, "PCE Sector Image", ExtensionList)

        ExtensionList = New List(Of String) From {".86f"}
        FileFilter = FileDialogAppendFilter(FileFilter, "86Box 86F Image", ExtensionList)

        ExtensionList = New List(Of String) From {".hfe"}
        FileFilter = FileDialogAppendFilter(FileFilter, "HxC HFE (v1) Image", ExtensionList)

        ExtensionList = New List(Of String) From {".mfm"}
        FileFilter = FileDialogAppendFilter(FileFilter, "HxC MFM Image", ExtensionList)

        ExtensionList = New List(Of String) From {".tc"}
        FileFilter = FileDialogAppendFilter(FileFilter, "Transcopy Image", ExtensionList)

        FileFilter = FileDialogAppendFilter(FileFilter, "Zip Archive", ".zip")
        FileFilter = FileDialogAppendFilter(FileFilter, "All files", ".*")

        Return FileFilter
    End Function

    Public Function GetSaveDialogFilters(DiskFormat As FloppyDiskFormat, ImageType As FloppyImageType, FileExt As String) As SaveDialogFilter
        Dim Response As SaveDialogFilter
        Dim CurrentIndex As Integer = 1
        Dim Extension As String
        Dim ExtensionList As List(Of String)
        Dim ImageGroup = GetImageGroup(ImageType)

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

        If ImageType = FloppyImageType.IMDImage Or ImageGroup <> FloppyImageGroup.AdvancedSectorImage Then
            ExtensionList = New List(Of String) From {".imd"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "ImageDisk Sector Image", ExtensionList)
            CurrentIndex += 1
        End If

        If ImageType = FloppyImageType.PSIImage Or ImageGroup <> FloppyImageGroup.AdvancedSectorImage Then
            ExtensionList = New List(Of String) From {".psi"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "PCE Sector Image", ExtensionList)
            CurrentIndex += 1
        End If

        If ImageGroup <> FloppyImageGroup.AdvancedSectorImage Then
            ExtensionList = New List(Of String) From {".86f"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "86Box 86F Image", ExtensionList)
            CurrentIndex += 1

            ExtensionList = New List(Of String) From {".hfe"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "HxC HFE (v1) Image", ExtensionList)
            CurrentIndex += 1

            ExtensionList = New List(Of String) From {".mfm"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "HxC MFM Image", ExtensionList)
            CurrentIndex += 1

            ExtensionList = New List(Of String) From {".pri"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "PCE Bitstream Image", ExtensionList)
            CurrentIndex += 1

            ExtensionList = New List(Of String) From {".tc"}
            For Each Extension In ExtensionList
                If FileExt.Equals(Extension, StringComparison.OrdinalIgnoreCase) Then
                    Response.FilterIndex = CurrentIndex
                End If
            Next
            Response.Filter = FileDialogAppendFilter(Response.Filter, "Transcopy Image", ExtensionList)
            CurrentIndex += 1
        End If

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

        If IO.File.Exists(FilePath) Then
            Try
                Data = IO.File.ReadAllBytes(FilePath)
            Catch ex As Exception
                DebugException(ex)
                Data = Nothing
            End Try
        End If

        Return Data
    End Function

    Public Function ImageSaveToTemp(Data() As Byte, DisplayPath As String) As String
        Dim TempPath = IO.Path.Combine(IO.Path.GetTempPath(), "DiskImageTool")
        Dim TempFileName = HashFunctions.SHA1Hash(System.Text.Encoding.Unicode.GetBytes(DisplayPath)) & ".tmp"

        Try
            If Not IO.Directory.Exists(TempPath) Then
                IO.Directory.CreateDirectory(TempPath)
            End If
            TempPath = IO.Path.Combine(TempPath, TempFileName)

            IO.File.WriteAllBytes(TempPath, Data)
        Catch ex As Exception
            DebugException(ex)
            TempPath = ""
        End Try

        Return TempPath
    End Function

    Public Sub InitAllFileExtensions()
        Dim Items = System.Enum.GetValues(GetType(FloppyDiskFormat))
        For Each Item As Integer In Items
            Dim FileExt = GetImageFileExtensionByFormat(Item)
            If FileExt <> "" Then
                If Not BasicSectorFileExtensions.Contains(FileExt) Then
                    BasicSectorFileExtensions.Add(FileExt)
                End If
            End If
        Next

        For Each Item In BasicSectorFileExtensions
            If Not AllFileExtensions.Contains(Item) Then
                AllFileExtensions.Add(Item)
            End If
        Next

        For Each Item In AdvancedSectorFileExtensions
            If Not AllFileExtensions.Contains(Item) Then
                AllFileExtensions.Add(Item)
            End If
        Next

        For Each Item In BitstreamFileExtensions
            If Not AllFileExtensions.Contains(Item) Then
                AllFileExtensions.Add(Item)
            End If
        Next
    End Sub

    Public Function OpenFileFromZIP(ZipFileName As String, FileName As String) As Byte()
        Dim Data As New IO.MemoryStream()
        Dim Archive As IO.Compression.ZipArchive = IO.Compression.ZipFile.OpenRead(ZipFileName)
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
        Dim DiskImageType = Disk.Image.ImageType
        Dim Response As SaveImageResponse = SaveImageResponse.Failed
        Dim Result As Boolean = False

        If Not CheckCompatibility(FileImageType, Disk.Image) Then
            Return SaveImageResponse.Cancelled
        End If

        If FileImageType = DiskImageType Then
            Result = Disk.Image.SaveToFile(FilePath)

        ElseIf FileImageType = FloppyImageType.BasicSectorImage Then
            Dim Data = Disk.Image.GetBytes()
            Result = SaveByteArrayToFile(FilePath, Data)

        ElseIf DiskImageType = FloppyImageType.BasicSectorImage Then
            If IsDiskFormatValidForRead(Disk.DiskFormat) Then
                Dim Data = Disk.Image.GetBytes()

                If FileImageType = FloppyImageType.TranscopyImage Then
                    Dim Transcopy = ImageFormats.BasicSectorToTranscopyImage(Data, Disk.DiskFormat)
                    Result = Transcopy.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.PSIImage Then
                    Dim PSI = ImageFormats.BasicSectorToPSIImage(Data, Disk.DiskFormat)
                    Result = PSI.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.PRIImage Then
                    Dim PRI = ImageFormats.BasicSectorToPRIImage(Data, Disk.DiskFormat)
                    Result = PRI.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.MFMImage Then
                    Dim MFM = ImageFormats.BasicSectorToMFMImage(Data, Disk.DiskFormat)
                    Result = MFM.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.HFEImage Then
                    Dim HFE = ImageFormats.BasicSectorToHFEImage(Data, Disk.DiskFormat)
                    Result = HFE.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.D86FImage Then
                    Dim D86F = ImageFormats.BasicSectorTo86FImage(Data, Disk.DiskFormat)
                    Result = D86F.Export(FilePath)
                ElseIf FileImageType = FloppyImageType.IMDImage Then
                    Dim IMD = ImageFormats.BasicSectorToIMDImage(Data, Disk.DiskFormat)
                    Result = IMD.Export(FilePath)
                Else
                    Return SaveImageResponse.Unsupported
                End If
            Else
                Return SaveImageResponse.Unknown
            End If

        ElseIf FileImageType = FloppyImageType.TranscopyImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim Transcopy = ImageFormats.BitstreamToTranscopyImage(Image)
                Result = Transcopy.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.MFMImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim MFM = ImageFormats.BitstreamToMFMImage(Image)
                Result = MFM.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.HFEImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim HFE = ImageFormats.BitstreamToHFEImage(Image)
                Result = HFE.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.PSIImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim PSI = ImageFormats.BitstreamToPSIImage(Image)
                Result = PSI.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.PRIImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim PRI = ImageFormats.BitstreamToPRIImage(Image)
                Result = PRI.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.IMDImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim IMD = ImageFormats.BitstreamToIMDImage(Image)
                Result = IMD.Export(FilePath)
            Else
                Return SaveImageResponse.Unsupported
            End If

        ElseIf FileImageType = FloppyImageType.D86FImage Then
            Dim Image As IBitstreamImage = Disk.Image.BitstreamImage
            If Image IsNot Nothing Then
                Dim D86FImage = ImageFormats.BitstreamTo86FImage(Image)
                Result = D86FImage.Export(FilePath)
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

    Private Function CheckCompatibility(FileImageType As FloppyImageType, FloppyImage As IFloppyImage) As Boolean
        Dim Msg As String

        If FileImageType = FloppyImage.ImageType Then
            Return True
        End If

        If FileImageType = FloppyImageType.IMDImage Then
            If TypeOf FloppyImage Is MappedFloppyImage Then
                Dim BitstreamData = DirectCast(FloppyImage, MappedFloppyImage)
                Dim CompatibleSectors As Boolean = True
                For i = 0 To BitstreamData.TrackCount - 1
                    For j = 0 To BitstreamData.SideCount - 1
                        Dim TrackData = BitstreamData.GetTrack(i, j)
                        If TrackData IsNot Nothing Then
                            If TrackData.FirstSector > -1 Then
                                If TrackData.SectorSize = -1 Or TrackData.OverlappingSectors Or TrackData.DuplicateSectors Then
                                    CompatibleSectors = False
                                    Exit For
                                End If
                            End If
                        End If
                    Next
                    If Not CompatibleSectors Then
                        Exit For
                    End If
                Next
                If Not CompatibleSectors Then
                    Msg = "This image has one or more tracks that are not compatible with this image type."
                    MsgBox(Msg, MsgBoxStyle.Exclamation)
                    Return False
                End If
            End If
        End If

        If FileImageType = FloppyImageType.BasicSectorImage Then
            If FloppyImage.NonStandardTracks.Count > 0 Then
                Msg = "This image has one or more tracks with a non-standard sector layout." _
                    & vbCrLf & vbCrLf & "Data loss will occur when saving to this image type." _
                    & vbCrLf & vbCrLf & "Do you wish to continue?"
                If MsgBox(Msg, MsgBoxStyle.Exclamation + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.No Then
                    Return False
                End If
            End If

        ElseIf FloppyImage.HasWeakBitsSupport Then
            If Not HasWeakBitsSupport(FileImageType) Then
                If FloppyImage.NonStandardTracks.Count > 0 Then
                    If FloppyImage.HasWeakBits Then
                        Msg = "This image contains additional surface data which may be required for copy protection." _
                        & vbCrLf & vbCrLf & "This data will be lost when saving to this image type." _
                        & vbCrLf & vbCrLf & "Do you wish to continue?"
                        If MsgBox(Msg, MsgBoxStyle.Exclamation + MsgBoxStyle.YesNo + MsgBoxStyle.DefaultButton2) = MsgBoxResult.No Then
                            Return False
                        End If
                    End If
                End If
            End If
        End If

        Return True
    End Function

    Private Function GetImageGroup(ImageType As FloppyImageType) As FloppyImageGroup
        Select Case ImageType
            Case FloppyImageType.HFEImage, FloppyImageType.MFMImage, FloppyImageType.TranscopyImage, FloppyImageType.D86FImage, FloppyImageType.PRIImage
                Return FloppyImageGroup.BitstreamImage
            Case FloppyImageType.IMDImage, FloppyImageType.PSIImage
                Return FloppyImageGroup.AdvancedSectorImage
            Case Else
                Return FloppyImageGroup.BasicSectorImage
        End Select
    End Function

    Private Function HasWeakBitsSupport(ImageType As FloppyImageType) As Boolean
        Return (ImageType = FloppyImageType.PSIImage Or ImageType = FloppyImageType.PRIImage Or ImageType = FloppyImageType.D86FImage)
    End Function
End Module