﻿Imports DiskImageTool.Bitstream
Imports DiskImageTool.Bitstream.IBM_MFM
Imports DiskImageTool.DiskImage.FloppyDiskFunctions

Namespace ImageFormats
    Module ImageConversion
        Private Class DriveSpeed
            Private _RPM As UShort
            Private _BitRate As UShort

            Public Sub New()
                _RPM = 0
                _BitRate = 0
            End Sub

            Public Sub SetValue(RPM As UShort, BitRate As UShort)
                _RPM = RPM
                _BitRate = BitRate
            End Sub

            Public Property RPM As UShort
                Get
                    Return _RPM
                End Get
                Set
                    _RPM = Value
                End Set
            End Property

            Public Property BitRate As UShort
                Get
                    Return _BitRate
                End Get
                Set
                    _BitRate = Value
                End Set
            End Property
        End Class

        Public Function BasicSectorToPSIImage(Data() As Byte, DiskFormat As FloppyDiskFormat) As PSI.PSISectorImage
            Dim Params = GetFloppyDiskParams(DiskFormat)
            Dim CylinderCount = Params.SectorCountSmall \ Params.SectorsPerTrack \ Params.NumberOfHeads

            Dim PSI = New PSI.PSISectorImage
            PSI.Header.FormatVersion = 0
            PSI.Header.DefaultSectorFormat = GetPSISectorFormat(DiskFormat)
            PSI.Comment = My.Application.Info.ProductName & " v" & GetVersionString()

            For Cylinder As UShort = 0 To CylinderCount - 1
                For Head = 0 To Params.NumberOfHeads - 1
                    Dim TrackOffset As UInteger = MFM_GAP4A_SIZE + MFM_SYNC_SIZE + MFM_ADDRESS_MARK_SIZE + MFM_GAP1_SIZE
                    For SectorId = 1 To Params.SectorsPerTrack
                        TrackOffset += MFM_SYNC_SIZE + MFM_ADDRESS_MARK_SIZE
                        Dim ImageOffset = GetImageOffset(Params, Cylinder, Head, SectorId)
                        Dim Size = Math.Min(Params.BytesPerSector, Data.Length - ImageOffset)
                        Dim Buffer = New Byte(Size - 1) {}
                        Array.Copy(Data, ImageOffset, Buffer, 0, Size)

                        Dim PSISector = New PSI.PSISector With {
                                .HasDataCRCError = False,
                                .IsAlternateSector = False,
                                .Cylinder = Cylinder,
                                .Head = Head,
                                .Sector = SectorId,
                                .Data = Buffer,
                                .Offset = TrackOffset * 8
                            }

                        TrackOffset += MFM_IDAM_SIZE + MFM_GAP2_SIZE + MFM_SYNC_SIZE + MFM_ADDRESS_MARK_SIZE + Size + 2 + MFM_GAP3_SIZE

                        PSI.Sectors.Add(PSISector)
                    Next
                Next
            Next

            Return PSI
        End Function

        Public Function BasicSectorToMFMImage(Data() As Byte, DiskFormat As FloppyDiskFormat) As MFM.MFMImage
            Dim Params = GetFloppyDiskParams(DiskFormat)
            Dim DriveSpeed = GetDriveSpeed(DiskFormat)
            Dim TrackCount = Params.SectorCountSmall \ Params.SectorsPerTrack \ Params.NumberOfHeads

            Dim MFM = New MFM.MFMImage()
            MFM.Initialize(TrackCount, Params.NumberOfHeads, 1)

            For Track As UShort = 0 To TrackCount - 1
                For Side As UShort = 0 To Params.NumberOfHeads - 1
                    Dim MFMTrack = New MFM.MFMTrack(Track, Side)

                    Dim MFMBitstream = MFMBitstreamFromSectorImage(Data, Params, Track, Side, DriveSpeed.RPM, DriveSpeed.BitRate)

                    MFMTrack.Bitstream = MFMBitstream.Bitstream

                    MFM.SetTrack(Track, Side, MFMTrack)
                Next
            Next

            Return MFM
        End Function

        Public Function BasicSectorToTranscopyImage(Data() As Byte, DiskFormat As FloppyDiskFormat) As TC.TransCopyImage
            Dim Params = GetFloppyDiskParams(DiskFormat)
            Dim DriveSpeed = GetDriveSpeed(DiskFormat)
            Dim CylinderCount = Params.SectorCountSmall \ Params.SectorsPerTrack \ Params.NumberOfHeads

            Dim Transcopy = New TC.TransCopyImage With {
                .DiskType = GetTranscopyDiskType(DiskFormat),
                .Comment = My.Application.Info.ProductName & " v" & GetVersionString()
            }
            Transcopy.Initialize(CylinderCount, Params.NumberOfHeads, 1)

            For Cylinder As UShort = 0 To CylinderCount - 1
                For Head As UShort = 0 To Params.NumberOfHeads - 1
                    Dim TransCopyCylinder = New TC.TransCopyCylinder(Cylinder, Head) With {
                        .TrackType = GetTranscopyDiskType(DiskFormat),
                        .CopyAcrossIndex = True
                    }

                    Dim MFMBitstream = MFMBitstreamFromSectorImage(Data, Params, Cylinder, Head, DriveSpeed.RPM, DriveSpeed.BitRate)

                    TransCopyCylinder.Bitstream = MFMBitstream.Bitstream
                    TransCopyCylinder.SetTimings(MFMBitstream.AddressMarkIndexes)

                    Transcopy.SetCylinder(Cylinder, Head, TransCopyCylinder)
                Next
            Next

            Return Transcopy
        End Function

        Public Function BitstreamToMFMImage(Image As IBitstreamImage) As MFM.MFMImage
            Dim BitstreamTrack As IBitstreamTrack
            Dim DiskSpeed As New DriveSpeed

            Image.UpdateBitstream()

            Dim MFM = New MFM.MFMImage()
            MFM.Initialize(Image.TrackCount, Image.SideCount, Image.TrackStep)

            For Track = 0 To Image.TrackCount - 1 Step Image.TrackStep
                For Side = 0 To Image.SideCount - 1
                    BitstreamTrack = Image.GetTrack(Track, Side)
                    If BitstreamTrack.Decoded Then
                        Dim DriveSpeed = GetDriveSpeed(BitstreamTrack.MFMData.GetTrackFormat)
                        If DiskSpeed.BitRate = 0 And DriveSpeed.BitRate > 0 Then
                            DiskSpeed = DriveSpeed
                        End If
                    End If

                    Dim MFMTrack = New MFM.MFMTrack(Track, Side) With {
                            .Bitstream = BitstreamTrack.Bitstream
                        }


                    MFM.SetTrack(Track \ Image.TrackStep, Side, MFMTrack)
                Next
            Next

            MFM.BitRate = DiskSpeed.BitRate
            MFM.RPM = DiskSpeed.RPM

            Return MFM
        End Function

        Public Function BitstreamToPSIImage(Image As IBitstreamImage) As PSI.PSISectorImage
            Dim PSI = New PSI.PSISectorImage
            Dim BitstreamTrack As IBitstreamTrack
            Dim DiskFormat As MFMTrackFormat = MFMTrackFormat.TrackFormatUnknown
            Dim TrackCount As UShort

            TrackCount = Image.TrackCount
            If TrackCount > 80 Then
                TrackCount = 80
            ElseIf TrackCount > 40 Then
                TrackCount = 40
            End If

            PSI.Header.FormatVersion = 0
            PSI.Comment = My.Application.Info.ProductName & " v" & GetVersionString()

            For Track = 0 To TrackCount - 1 Step Image.TrackStep
                For Side = 0 To Image.SideCount - 1
                    BitstreamTrack = Image.GetTrack(Track, Side)

                    If BitstreamTrack.Decoded Then
                        Dim TrackFormat = BitstreamTrack.MFMData.GetTrackFormat()

                        If DiskFormat = MFMTrackFormat.TrackFormatUnknown And TrackFormat <> MFMTrackFormat.TrackFormatUnknown Then
                            DiskFormat = TrackFormat
                        End If

                        For Each Sector In BitstreamTrack.MFMData.Sectors
                            Dim PSISector = PSISectorFromMFMSector(Sector)

                            PSI.Sectors.Add(PSISector)
                        Next
                    End If
                Next
            Next

            PSI.Header.DefaultSectorFormat = GetPSISectorFormat(DiskFormat)

            Return PSI
        End Function

        Public Function BitstreamToTranscopyImage(Image As IBitstreamImage) As TC.TransCopyImage
            Dim BitstreamTrack As IBitstreamTrack
            Dim DiskType As TC.TransCopyDiskType = TC.TransCopyDiskType.Unknown

            Image.UpdateBitstream()

            Dim Transcopy = New TC.TransCopyImage With {
                .Comment = My.Application.Info.ProductName & " v" & GetVersionString()
            }
            Transcopy.Initialize(Image.TrackCount \ Image.TrackStep, Image.SideCount, 1)

            For Track = 0 To Image.TrackCount - 1 Step Image.TrackStep
                For Side = 0 To Image.SideCount - 1
                    BitstreamTrack = Image.GetTrack(Track, Side)

                    Dim TrackType As TC.TransCopyDiskType

                    If BitstreamTrack.Decoded Then
                        TrackType = GetTranscopyDiskType(BitstreamTrack.MFMData.GetTrackFormat)
                        If DiskType = TC.TransCopyDiskType.Unknown And TrackType <> TC.TransCopyDiskType.Unknown Then
                            DiskType = TrackType
                        End If
                    Else
                        TrackType = TC.TransCopyDiskType.FMSingleDensity
                    End If

                    Dim TransCopyCylinder = New TC.TransCopyCylinder(Track, Side) With {
                            .TrackType = TrackType,
                            .CopyAcrossIndex = True,
                            .Bitstream = BitstreamTrack.Bitstream
                        }
                    TransCopyCylinder.SetTimings(BitstreamTrack.MFMData.AddressMarkIndexes)
                    Transcopy.SetCylinder(Track \ Image.TrackStep, Side, TransCopyCylinder)
                Next
            Next

            Transcopy.DiskType = DiskType

            Return Transcopy
        End Function

        Private Function GetImageOffset(Params As FloppyDiskParams, Track As UShort, Side As UShort, SectorId As UShort) As UInteger
            Return (Track * Params.NumberOfHeads * Params.SectorsPerTrack + Params.SectorsPerTrack * Side + (SectorId - 1)) * Params.BytesPerSector
        End Function

        Private Function GetPSISectorFormat(TrackFormat As MFMTrackFormat) As PSI.DefaultSectorFormat
            Select Case TrackFormat
                Case MFMTrackFormat.TrackFormatDD
                    Return PSI.DefaultSectorFormat.IBM_MFM_DD
                Case MFMTrackFormat.TrackFormatHD
                    Return PSI.DefaultSectorFormat.IBM_MFM_HD
                Case MFMTrackFormat.TrackFormatHD1200
                    Return PSI.DefaultSectorFormat.IBM_MFM_HD
                Case MFMTrackFormat.TrackFormatED
                    Return PSI.DefaultSectorFormat.IBM_MFM_ED
                Case Else
                    Return 0
            End Select
        End Function

        Private Function GetPSISectorFormat(DiskFormat As FloppyDiskFormat) As PSI.DefaultSectorFormat
            Select Case DiskFormat
                Case FloppyDiskFormat.Floppy1200
                    Return PSI.DefaultSectorFormat.IBM_MFM_HD
                Case FloppyDiskFormat.Floppy1440
                    Return PSI.DefaultSectorFormat.IBM_MFM_HD
                Case FloppyDiskFormat.Floppy2880
                    Return PSI.DefaultSectorFormat.IBM_MFM_ED
                Case Else
                    Return PSI.DefaultSectorFormat.IBM_MFM_DD
            End Select
        End Function

        Private Function GetDriveSpeed(TrackFormat As MFMTrackFormat) As DriveSpeed
            Dim DriveSpeed As New DriveSpeed

            Select Case TrackFormat
                Case MFMTrackFormat.TrackFormatDD
                    DriveSpeed.SetValue(300, 250)
                Case MFMTrackFormat.TrackFormatHD
                    DriveSpeed.SetValue(300, 500)
                Case MFMTrackFormat.TrackFormatHD1200
                    DriveSpeed.SetValue(360, 500)
                Case MFMTrackFormat.TrackFormatED
                    DriveSpeed.SetValue(300, 1000)
            End Select

            Return DriveSpeed
        End Function

        Private Function GetDriveSpeed(DiskFormat As FloppyDiskFormat) As DriveSpeed
            Dim DriveSpeed As New DriveSpeed

            Select Case DiskFormat
                Case FloppyDiskFormat.Floppy1200
                    DriveSpeed.SetValue(360, 500)
                Case FloppyDiskFormat.Floppy1440
                    DriveSpeed.SetValue(360, 500)
                Case FloppyDiskFormat.Floppy2880
                    DriveSpeed.SetValue(300, 1000)
                Case FloppyDiskFormat.FloppyDMF1024
                    DriveSpeed.SetValue(360, 500)
                Case FloppyDiskFormat.FloppyDMF2048
                    DriveSpeed.SetValue(360, 500)
                Case FloppyDiskFormat.FloppyXDF525
                    DriveSpeed.SetValue(360, 500)
                Case FloppyDiskFormat.FloppyXDF35
                    DriveSpeed.SetValue(360, 500)
                Case Else
                    DriveSpeed.SetValue(300, 250)
            End Select

            Return DriveSpeed
        End Function

        Private Function GetTranscopyDiskType(TrackFormat As MFMTrackFormat) As TC.TransCopyDiskType
            Select Case TrackFormat
                Case MFMTrackFormat.TrackFormatDD
                    Return TC.TransCopyDiskType.MFMDoubleDensity
                Case MFMTrackFormat.TrackFormatHD
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case MFMTrackFormat.TrackFormatHD1200
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case MFMTrackFormat.TrackFormatED
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case Else
                    Return TC.TransCopyDiskType.Unknown
            End Select
        End Function

        Private Function GetTranscopyDiskType(DiskFormat As FloppyDiskFormat) As TC.TransCopyDiskType
            Select Case DiskFormat
                Case FloppyDiskFormat.Floppy1200
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case FloppyDiskFormat.Floppy1440
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case FloppyDiskFormat.Floppy2880
                    Return TC.TransCopyDiskType.MFMHighDensity
                Case Else
                    Return TC.TransCopyDiskType.MFMDoubleDensity
            End Select
        End Function

        Private Function MFMBitstreamFromSectorImage(Data() As Byte, Params As FloppyDiskParams, Track As UShort, Side As Byte, RPM As UShort, BitRate As UShort) As IBM_MFM_Bitstream
            Dim MFMBitstream = New IBM_MFM_Bitstream(MFM_GAP4A_SIZE, MFM_GAP1_SIZE)

            For SectorId = 1 To Params.SectorsPerTrack
                Dim ImageOffset = GetImageOffset(Params, Track, Side, SectorId)
                Dim Size = Math.Min(Params.BytesPerSector, Data.Length - ImageOffset)
                Dim Buffer = New Byte(Size - 1) {}
                Array.Copy(Data, ImageOffset, Buffer, 0, Size)

                MFMBitstream.AddSectorId(Track, Side, SectorId, IBM_MFM_Bitstream.MFMSectorSize.SectorSize_512)
                MFMBitstream.AddData(Buffer, MFM_GAP3_SIZE)
            Next

            MFMBitstream.Finish(RPM, BitRate)

            Return MFMBitstream
        End Function

        Private Function PSISectorFromMFMSector(Sector As IBM_MFM_Sector) As PSI.PSISector
            If Sector.IsValid Then
                Sector.UpdateChecksum()
            End If

            Dim DataChecksumValid = Sector.DataChecksum = Sector.CalculateDataChecksum

            Dim PSISector = New PSI.PSISector With {
                   .HasDataCRCError = Not DataChecksumValid,
                   .IsAlternateSector = False,
                   .Cylinder = Sector.Track,
                   .Head = Sector.Side,
                   .Sector = Sector.SectorId,
                   .Data = Sector.Data,
                   .Offset = (Sector.Offset + 64) / 2
                }

            Return PSISector
        End Function
    End Module
End Namespace