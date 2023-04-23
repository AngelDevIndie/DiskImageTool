﻿Imports DiskImageTool.DiskImage
Imports DiskImageTool.DiskImage.BootSector
Imports Hb.Windows.Forms

Public Class BootSectorForm
    Private ReadOnly _Disk As DiskImage.Disk
    Private ReadOnly _OEMNameDictionary As Dictionary(Of UInteger, BootstrapLookup)
    Private ReadOnly _HasExtended As Boolean
    Private ReadOnly _HelpProvider1 As HelpProvider
    Private _SuppressEvent As Boolean = True

    Public Sub New(Disk As DiskImage.Disk, OEMNameDictionary As Dictionary(Of UInteger, BootstrapLookup))

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        _Disk = Disk
        _OEMNameDictionary = OEMNameDictionary
        _HelpProvider1 = New HelpProvider

        Dim BootStrapStart = _Disk.BootSector.GetBootStrapOffset
        _HasExtended = BootStrapStart >= BootSectorOffsets.FileSystemType + BootSectorSizes.FileSystemType

        IntitializeHelp()
        PopulateDiskTypes()
        PopulateValues()
        SetCurrentDiskType()

        _SuppressEvent = False
    End Sub

    Private Sub IntitializeHelp()
        Dim Msg As String

        Msg = "OEM Name Identifier" _
            & vbCrLf & vbCrLf & "Can be set by a FAT implementation to any desired value" _
            & vbCrLf & vbCrLf & "Typically this Is some indication of what system formatted the volume."
        SetHelpString(Msg, LblOEMName, CboOEMName, HexOEMName)

        Msg = "Number of bytes in each physical sector" _
            & vbCrLf & vbCrLf & "Allowed Values: 512, 1024, 2048, 4096" _
            & vbCrLf & vbCrLf & "Note: This value should be 512 for all floppy disks."
        SetHelpString(Msg, LblBytesPerSector, CboBytesPerSector)

        Msg = "Number of sectors per cluster" _
            & vbCrLf & vbCrLf & "Allowed Values: 1, 2, 4, 8, 16, 32, 128" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "1" _
            & vbCrLf & "180K Floppy" & vbTab & "1" _
            & vbCrLf & "320K Floppy" & vbTab & "2" _
            & vbCrLf & "360K Floppy" & vbTab & "2" _
            & vbCrLf & "720K Floppy" & vbTab & "2" _
            & vbCrLf & "1.2M Floppy" & vbTab & "1" _
            & vbCrLf & "1.44M Floppy" & vbTab & "1" _
            & vbCrLf & "2.88M Floppy" & vbTab & "2" _
            & vbCrLf & "DMF Floppy" & vbTab & "2 or 4"
        SetHelpString(Msg, LblSectorsPerCluster, CboSectorsPerCluster)

        Msg = "Number of reserved sectors in the reserved region of the volume starting at the first sector of the volume" _
            & vbCrLf & vbCrLf & "Allowed Values: Any non-zero value" _
            & vbCrLf & vbCrLf & "Note: This value should typically be set to 1."
        SetHelpString(Msg, LblReservedSectors, TxtReservedSectors)

        Msg = "Number of File Allocation Table (FAT) copies on the volume" _
            & vbCrLf & vbCrLf & "Allowed Values: Any non-zero value" _
            & vbCrLf & vbCrLf & "Note: This value should typically be set to 2."
        SetHelpString(Msg, LblNumberOfFATS, TxtNumberOfFATs)

        Msg = "Number of entries in the root directory" _
            & vbCrLf & vbCrLf & "Allowed Values: Value multiplied by 32 should be an even multiple of Bytes Per Sector" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "64" _
            & vbCrLf & "180K Floppy" & vbTab & "64" _
            & vbCrLf & "320K Floppy" & vbTab & "112" _
            & vbCrLf & "360K Floppy" & vbTab & "112" _
            & vbCrLf & "720K Floppy" & vbTab & "112" _
            & vbCrLf & "1.2M Floppy" & vbTab & "224" _
            & vbCrLf & "1.44M Floppy" & vbTab & "224" _
            & vbCrLf & "2.88M Floppy" & vbTab & "240" _
            & vbCrLf & "DMF Floppy" & vbTab & "16"
        SetHelpString(Msg, LblRootDirectoryEntries, TxtRootDirectoryEntries)

        Msg = "Total number of sectors in the volume" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "320" _
            & vbCrLf & "180K Floppy" & vbTab & "360" _
            & vbCrLf & "320K Floppy" & vbTab & "640" _
            & vbCrLf & "360K Floppy" & vbTab & "720" _
            & vbCrLf & "720K Floppy" & vbTab & "1440" _
            & vbCrLf & "1.2M Floppy" & vbTab & "2400" _
            & vbCrLf & "1.44M Floppy" & vbTab & "2880" _
            & vbCrLf & "2.88M Floppy" & vbTab & "5760" _
            & vbCrLf & "DMF Floppy" & vbTab & "3360"
        SetHelpString(Msg, LblSectorCountSmall, TxtSectorCountSmall)

        Msg = "Media Descriptor" _
            & vbCrLf & vbCrLf & "Allowed Values:" _
            & vbCrLf & "F0" & vbTab & "1.44M, 2.88M, DMF Floppy" _
            & vbCrLf & "F8" & vbTab & "Fixed Disk" _
            & vbCrLf & "F9" & vbTab & "720K & 1.2M Floppy" _
            & vbCrLf & "FA" & vbTab & "Unused" _
            & vbCrLf & "FB" & vbTab & "Unused" _
            & vbCrLf & "FC" & vbTab & "180K Floppy" _
            & vbCrLf & "FD" & vbTab & "360K Floppy" _
            & vbCrLf & "FE" & vbTab & "160K Floppy" _
            & vbCrLf & "FF" & vbTab & "320K Floppy"
        SetHelpString(Msg, TxtMediaDescriptor, HexMediaDescriptor)

        Msg = "Number of sectors allocated to each copy of the File Allocation Table (FAT)" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "1" _
            & vbCrLf & "180K Floppy" & vbTab & "2" _
            & vbCrLf & "320K Floppy" & vbTab & "1" _
            & vbCrLf & "360K Floppy" & vbTab & "2" _
            & vbCrLf & "720K Floppy" & vbTab & "3" _
            & vbCrLf & "1.2M Floppy" & vbTab & "7" _
            & vbCrLf & "1.44M Floppy" & vbTab & "9" _
            & vbCrLf & "2.88M Floppy" & vbTab & "9" _
            & vbCrLf & "DMF Floppy" & vbTab & "3 or 5"
        SetHelpString(Msg, LblSectorsPerFAT, TxtSectorsPerFAT)

        Msg = "Number of sectors per track on the disk" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "8" _
            & vbCrLf & "180K Floppy" & vbTab & "9" _
            & vbCrLf & "320K Floppy" & vbTab & "8" _
            & vbCrLf & "360K Floppy" & vbTab & "9" _
            & vbCrLf & "720K Floppy" & vbTab & "9" _
            & vbCrLf & "1.2M Floppy" & vbTab & "15" _
            & vbCrLf & "1.44M Floppy" & vbTab & "18" _
            & vbCrLf & "2.88M Floppy" & vbTab & "36" _
            & vbCrLf & "DMF Floppy" & vbTab & "21"
        SetHelpString(Msg, LblSectorsPerTrack, TxtSectorsPerTrack)

        Msg = "Number of physical heads (sides) on the disk" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "160K Floppy" & vbTab & "1" _
            & vbCrLf & "180K Floppy" & vbTab & "1" _
            & vbCrLf & "320K Floppy" & vbTab & "2" _
            & vbCrLf & "360K Floppy" & vbTab & "2" _
            & vbCrLf & "720K Floppy" & vbTab & "2" _
            & vbCrLf & "1.2M Floppy" & vbTab & "2" _
            & vbCrLf & "1.44M Floppy" & vbTab & "2" _
            & vbCrLf & "2.88M Floppy" & vbTab & "2" _
            & vbCrLf & "DMF Floppy" & vbTab & "2"
        SetHelpString(Msg, LblNumberOfHeads, TxtNumberOfHeads)

        Msg = "Number of sectors preceeding the first sector of a partitioned volume" _
             & vbCrLf & vbCrLf & "Note: This value should be 0 for all floppy disks"
        SetHelpString(Msg, LblHiddenSectors, TxtHiddenSectors)

        Msg = "Total number of sectors in a FAT16 volume larger than 65535 sectors" _
             & vbCrLf & vbCrLf & "Note: This value should be 0 for all floppy disks"
        SetHelpString(Msg, LblSectorCountLarge, TxtSectorCountLarge)

        Msg = "Interrupt 13h drive number" _
            & vbCrLf & vbCrLf & "Allowed Values: 0, 128"
        SetHelpString(Msg, LblDriveNumber, TxtDriveNumber)

        Msg = "Extended Boot Signature" _
            & vbCrLf & vbCrLf & "Typical Values:" _
            & vbCrLf & "28h" & vbTab & "Volume Serial Number is present" _
            & vbCrLf & "29h" & vbTab & "Volume Serial Number, Volume Label, and File System ID are present"
        SetHelpString(Msg, lblExtendedBootSignature, HexExtendedBootSignature)

        Msg = "The Volume Serial Number is a 32-bit random number used in conjunction with the Volume Label for removable media tracking" _
            & vbCrLf & vbCrLf & "Note: This id is typically generated by converting the current date and time into a 32-bit value"
        SetHelpString(Msg, LblVolumeSerialNumber, HexVolumeSerialNumber)

        Msg = "This field typically matches the 11-byte volume label in the root directory of the disk or has the value ""NO NAME    "" if the volume label does not exist."
        SetHelpString(Msg, LblVolumeLabel, TxtVolumeLabel, HexVolumeLabel)

        Msg = "The File System ID is informational only" _
            & vbCrLf & vbCrLf & "Typical Values: FAT12, FAT16, FAT"
        SetHelpString(Msg, LblFileSystemType, TxtFileSystemType, HexFileSystemType)

        Msg = "The disk type detected based on the current values in the Boot Record.  Changing this will set the values in the boot record to those of the selected disk type."
        SetHelpString(Msg, LblDiskType, CboDiskType)

        Msg = "Generate a new volume serial number based on a user supplied date and time"
        SetHelpString(Msg, BtnVolumeSerialNumber)

        Msg = "This instruction indicates where the bootstrap code starts." _
            & vbCrLf & vbCrLf & "Allowed Values: EB xx 90, E9 xx xx"
        SetHelpString(Msg, LblJumpInstruction, HexJumpInstruction)

        Msg = "Indicated to the BIOS that the sector is executable." _
            & vbCrLf & vbCrLf & "Allowed Values: AA 55"
        SetHelpString(Msg, LblBootSectorSignature, HexBootSectorSignature)

        Msg = "This is additional data found in the Boot Sector." & vbCrLf & vbCrLf & "Note: Data highlighted in green is the bootstrap code."
        SetHelpString(Msg, HexBox1)
    End Sub

    Private Sub SetHelpString(HelpString As String, ParamArray ControlArray() As Control)
        For Each Control In ControlArray
            _HelpProvider1.SetHelpString(Control, HelpString)
            _HelpProvider1.SetShowHelp(Control, True)
        Next
    End Sub

    Private Function IsByteArrayNull(b() As Byte) As Boolean
        Dim Result As Boolean = True

        For Each value In b
            If value <> 0 Then
                Result = False
                Exit For
            End If
        Next

        Return Result
    End Function

    Private Function SetTagValue(Control As Control, Value As String) As FieldData
        If Control.Tag Is Nothing Then
            Control.Tag = New FieldData(Value)
        Else
            CType(Control.Tag, FieldData).LastValue = Value
        End If

        Return Control.Tag
    End Function

    Private Sub SetValue(Control As Control, Value As String)
        Control.Text = Value
        SetTagValue(Control, Value)
        UpdateColor(Control, False)
    End Sub

    Private Sub SetValue(Control As Control, Value As String, AllowedValues() As String)
        Control.Text = Value
        Dim Data = SetTagValue(Control, Value)
        Data.AllowedValues = AllowedValues
        UpdateColor(Control, False)
    End Sub

    Private Sub SetValue(Control As HexTextBox, Value As String)
        Control.SetHex(Value)
        SetTagValue(Control, Value)
        UpdateColor(Control, False)
    End Sub

    Private Sub SetValue(Control As HexTextBox, Value() As Byte, IsInvalid As Boolean)
        Control.SetHex(Value)
        SetTagValue(Control, BitConverter.ToString(Value).Replace("-", ""))
        UpdateColor(Control, IsInvalid)
    End Sub

    Private Sub UpdateTag(Control As Control)
        CType(Control.Tag, FieldData).LastValue = Control.Text
        UpdateColor(Control, False)
    End Sub

    Private Sub UpdateTag(Control As Control, IsInvalid As Boolean)
        CType(Control.Tag, FieldData).LastValue = Control.Text
        UpdateColor(Control, IsInvalid)
    End Sub

    Private Sub UpdateColor(Control As Control, IsInvalid As Boolean)
        Dim Data = CType(Control.Tag, FieldData)

        If Control.Text <> Data.OriginalValue Then
            Control.ForeColor = Color.Blue
        Else
            Control.ForeColor = SystemColors.WindowText
        End If

        If IsInvalid Then
            Control.BackColor = Color.LightPink
        ElseIf Data.AllowedValues IsNot Nothing Then
            If Data.AllowedValues.Contains(Control.Text) Then
                Control.BackColor = SystemColors.Window
            Else
                Control.BackColor = Color.LightPink
            End If
        ElseIf Not IsDataValid(control) Then
            Control.BackColor = Color.LightPink
        Else
            Control.BackColor = SystemColors.Window
        End If
    End Sub

    Private Sub RevertValue(Control As Control)
        Control.Text = CType(Control.Tag, FieldData).LastValue
        UpdateColor(Control, False)
    End Sub

    Private Function IsDataValid(Control As Control) As Boolean
        If Control Is TxtReservedSectors Then
            Return Control.Text <> "0"
        ElseIf Control Is TxtNumberOfFATs Then
            Return Control.Text <> "0"
        ElseIf Control Is TxtRootDirectoryEntries Then
            Return Control.Text <> "0"
        ElseIf Control Is TxtSectorCountSmall Then
            Return Control.Text <> "0"
        ElseIf Control Is TxtSectorsPerFAT Then
            Return Control.Text <> "0"
        ElseIf Control Is TxtHiddenSectors Then
            Return Control.Text = "0"
        Else
            Return True
        End If
    End Function

    Private Sub PopulateBootRecord(BootSector As BootSector)
        SetValue(CboBytesPerSector, BootSector.BytesPerSector, Array.ConvertAll(BootSector.ValidBytesPerSector, Function(x) x.ToString()))
        SetValue(CboSectorsPerCluster, BootSector.SectorsPerCluster, Array.ConvertAll(BootSector.ValidSectorsPerCluster, Function(x) x.ToString()))
        SetValue(TxtReservedSectors, BootSector.ReservedSectorCount)
        SetValue(TxtNumberOfFATs, BootSector.NumberOfFATs)
        SetValue(TxtRootDirectoryEntries, BootSector.RootEntryCount)
        SetValue(TxtSectorCountSmall, BootSector.SectorCountSmall)
        SetValue(HexMediaDescriptor, BootSector.MediaDescriptor.ToString("X2"), Array.ConvertAll(BootSector.ValidMediaDescriptor, Function(x) x.ToString("X2")))
        SetValue(TxtSectorsPerFAT, BootSector.SectorsPerFAT)
        SetValue(TxtSectorsPerTrack, BootSector.SectorsPerTrack, {"8", "9", "15", "18", "21", "36"})
        SetValue(TxtNumberOfHeads, BootSector.NumberOfHeads, {"1", "2"})
    End Sub

    Private Sub PopulateExtended(BootSector As BootSector)
        SetValue(TxtSectorCountLarge, BootSector.SectorCountLarge)
        SetValue(TxtDriveNumber, BootSector.DriveNumber)
        SetValue(HexExtendedBootSignature, BootSector.ExtendedBootSignature.ToString("X2"))
        SetValue(HexVolumeSerialNumber, BootSector.VolumeSerialNumber.ToString("X8"))
        SetValue(TxtVolumeLabel, BootSector.GetVolumeLabelString.TrimEnd)
        HexVolumeLabel.SetHex(BootSector.VolumeLabel)
        SetValue(TxtFileSystemType, BootSector.GetFileSystemTypeString.TrimEnd)
        HexFileSystemType.SetHex(BootSector.FileSystemType)

        Dim ErrorCount As Integer = 0
        If BootSector.SectorCountLarge <> 0 Then
            ErrorCount += 1
        End If
        If BootSector.ExtendedBootSignature <> 0 And BootSector.ExtendedBootSignature <> &H28 And BootSector.ExtendedBootSignature <> &H29 Then
            ErrorCount += 1
        End If
        If BootSector.DriveNumber <> 0 And BootSector.DriveNumber <> 128 Then
            ErrorCount += 1
        End If
        LblExtendedMsg.Visible = (ErrorCount > 1)
    End Sub

    Private Sub PopulateAdditionalData(BootSector As BootSector)
        Dim DataStart As UShort
        Dim DataLength As UShort = 0
        Dim BootStrapStart = BootSector.GetBootStrapOffset

        If BootSector.HasValidExtendedBootSignature And BootStrapStart >= BootSectorOffsets.FileSystemType + BootSectorSizes.FileSystemType Then
            DataStart = BootSector.BootSectorOffsets.FileSystemType + BootSector.BootSectorSizes.FileSystemType
        Else
            DataStart = BootSector.BootSectorOffsets.HiddenSectors + BootSector.BootSectorSizes.HiddenSectors
            'If BootSector.HiddenSectors >> 4 > 0 Then
            'DataStart -= 2
            'End If
        End If

        If BootSectorOffsets.BootStrapSignature > DataStart Then
            DataLength = BootSectorOffsets.BootStrapSignature - DataStart
        End If

        If DataLength > 0 Then
            Dim Data(DataLength - 1) As Byte
            Array.Copy(BootSector.Data, DataStart, Data, 0, DataLength)

            HexBox1.ByteProvider = New DynamicByteProvider(Data)
            Dim LineCount As Integer = Math.Ceiling(DataLength / HexBox1.BytesPerLine)
            If LineCount = 1 Then
                LineCount = 2
            ElseIf LineCount > 10 Then
                LineCount = 10
                HexBox1.VScrollBarVisible = True
            End If
            HexBox1.Height = LineCount * 13 + 4

            If BootSector.HasValidJumpInstruction(False) Then
                Dim Start = BootStrapStart - DataStart
                HexBox1.Highlight(Start, DataLength - Start, Color.Green, Color.White)
            End If

            GroupBoxAdditionalData.Visible = True
        Else
            GroupBoxAdditionalData.Visible = False
        End If
    End Sub

    Private Sub PopulateValues()
        PopulateOEMName()
        PopulateBytesPerSector()
        PopulateSectorsPerCluster()
        PopulateBootRecord(_Disk.BootSector)

        SetValue(TxtHiddenSectors, _Disk.BootSector.HiddenSectors)
        SetValue(HexJumpInstruction, _Disk.BootSector.JmpBoot, Not _Disk.BootSector.HasValidJumpInstruction(True))
        SetValue(HexBootSectorSignature, _Disk.BootSector.BootStrapSignature.ToString("X4"), {BootSector.ValidBootStrapSignature.ToString("X4")})

        PopulateAdditionalData(_Disk.BootSector)

        If _HasExtended Then
            GroupBoxExtended.Visible = True
            PopulateExtended(_Disk.BootSector)
            SetExtendedState()
        Else
            GroupBoxExtended.Visible = False
        End If
    End Sub

    Private Sub PopulateDiskTypes()
        CboDiskType.Items.Clear()
        CboDiskType.Items.Add(New BootSectorDiskType("160K", FloppyDiskType.Floppy160))
        CboDiskType.Items.Add(New BootSectorDiskType("180K", FloppyDiskType.Floppy180))
        CboDiskType.Items.Add(New BootSectorDiskType("320K", FloppyDiskType.Floppy320))
        CboDiskType.Items.Add(New BootSectorDiskType("360K", FloppyDiskType.Floppy360))
        CboDiskType.Items.Add(New BootSectorDiskType("720K", FloppyDiskType.Floppy720))
        CboDiskType.Items.Add(New BootSectorDiskType("1.2M", FloppyDiskType.Floppy1200))
        CboDiskType.Items.Add(New BootSectorDiskType("1.44M", FloppyDiskType.Floppy1440))
        CboDiskType.Items.Add(New BootSectorDiskType("2.88M", FloppyDiskType.Floppy2880))
        CboDiskType.Items.Add(New BootSectorDiskType("DMF (1024)", FloppyDiskType.FloppyDMF1024))
        CboDiskType.Items.Add(New BootSectorDiskType("DMF (2048)", FloppyDiskType.FloppyDMF2048))
        CboDiskType.Items.Add(New BootSectorDiskType("Custom", FloppyDiskType.FloppyUnknown))

        CboDiskType.SelectedIndex = CboDiskType.Items.Count - 1
    End Sub

    Private Sub PopulateBytesPerSector()
        CboBytesPerSector.Items.Clear()
        For Each Value In BootSector.ValidBytesPerSector
            CboBytesPerSector.Items.Add(Value)
        Next
    End Sub

    Private Sub PopulateSectorsPerCluster()
        CboSectorsPerCluster.Items.Clear()
        For Each Value In BootSector.ValidSectorsPerCluster
            CboSectorsPerCluster.Items.Add(Value)
        Next
    End Sub

    Private Sub PopulateOEMName()
        Dim BootstrapChecksum = Crc32.ComputeChecksum(_Disk.BootSector.BootStrapCode)
        Dim OEMName As New KnownOEMName With {
            .Name = _Disk.BootSector.OEMName
        }

        If _OEMNameDictionary.ContainsKey(BootstrapChecksum) Then
            Dim BootstrapType = _OEMNameDictionary.Item(BootstrapChecksum)
            For Each KnownOEMName In BootstrapType.KnownOEMNames
                If KnownOEMName.Name.Length > 0 Then
                    Dim IsMatch = ByteArrayCompare(KnownOEMName.Name, OEMName.Name)
                    If (KnownOEMName.Suggestion And Not BootstrapType.ExactMatch) Or IsMatch Then
                        Dim Index = CboOEMName.Items.Add(KnownOEMName)
                        If IsMatch Then
                            CboOEMName.SelectedIndex = Index
                        End If
                    End If
                End If
            Next
        End If

        If CboOEMName.SelectedIndex = -1 Then
            If OEMName.Name.Length > 0 Then
                CboOEMName.Items.Add(OEMName)
            End If
        End If

        SetValue(CboOEMName, _Disk.BootSector.GetOEMNameString)
        HexOEMName.SetHex(_Disk.BootSector.OEMName)
    End Sub

    Private Sub RefreshVolumeLabel()
        Dim BootSignature = HexExtendedBootSignature.GetHex(0)
        Dim MaskNulls As Boolean = (BootSignature <> &H28 And BootSignature <> &H29)
        Dim b = HexVolumeLabel.GetHex
        Dim Value As String
        If MaskNulls AndAlso IsByteArrayNull(b) Then
            Value = ""
        Else
            Value = DiskImage.CodePage437ToUnicode(HexVolumeLabel.GetHex).TrimEnd
        End If

        SetValue(TxtVolumeLabel, Value)
    End Sub

    Private Sub RefreshFileSystemType()
        Dim MaskNulls As Boolean = (HexExtendedBootSignature.GetHex(0) <> &H29)
        Dim b = HexFileSystemType.GetHex
        Dim Value As String
        If MaskNulls AndAlso IsByteArrayNull(b) Then
            Value = ""
        Else
            Value = DiskImage.CodePage437ToUnicode(HexFileSystemType.GetHex).TrimEnd
        End If

        SetValue(TxtFileSystemType, Value)
    End Sub

    Private Sub SetCurrentDiskType()
        Dim SelectedItem As BootSectorDiskType = Nothing

        For Each DiskType As BootSectorDiskType In CboDiskType.Items
            If DiskType.Type <> FloppyDiskType.FloppyUnknown Then
                Dim BootSector = BuildBootSectorFromType(DiskType.Type)
                If BootSector.MediaDescriptor = HexMediaDescriptor.GetHex(0) _
                    And BootSector.NumberOfHeads = TxtNumberOfHeads.Text _
                    And BootSector.RootEntryCount = TxtRootDirectoryEntries.Text _
                    And BootSector.SectorCountSmall = TxtSectorCountSmall.Text _
                    And BootSector.SectorsPerCluster = CboSectorsPerCluster.Text _
                    And BootSector.SectorsPerFAT = TxtSectorsPerFAT.Text _
                    And BootSector.SectorsPerTrack = TxtSectorsPerTrack.Text _
                    And BootSector.BytesPerSector = CboBytesPerSector.Text _
                    And BootSector.NumberOfFATs = TxtNumberOfFATs.Text _
                    And BootSector.ReservedSectorCount = TxtReservedSectors.Text Then

                    SelectedItem = DiskType
                    Exit For
                End If
            End If
        Next

        If SelectedItem Is Nothing Then
            CboDiskType.SelectedIndex = CboDiskType.Items.Count - 1
        Else
            CboDiskType.SelectedItem = SelectedItem
        End If
    End Sub

    Private Sub SetExtendedState()
        Dim Enabled As Boolean

        Dim BootSignature = HexExtendedBootSignature.GetHex(0)

        Enabled = (BootSignature = &H28 Or BootSignature = &H29)
        HexVolumeSerialNumber.Enabled = Enabled
        BtnVolumeSerialNumber.Enabled = Enabled

        Enabled = (BootSignature = &H29)
        TxtVolumeLabel.Enabled = Enabled
        HexVolumeLabel.Enabled = Enabled
        TxtFileSystemType.Enabled = Enabled
        HexFileSystemType.Enabled = Enabled

        RefreshVolumeLabel()
        RefreshFileSystemType()
    End Sub

    Private Sub UpdateBootSector()
        _Disk.Data.BatchEditMode = True

        Dim UShortValue As UShort
        Dim UintegerValue As UInteger
        Dim ByteValue As Byte

        Dim OEMNameString As String = Strings.Left(CboOEMName.Text, 8).PadRight(8)
        Dim OEMName = DiskImage.UnicodeToCodePage437(OEMNameString)
        _Disk.BootSector.OEMName = OEMName

        If UShort.TryParse(CboBytesPerSector.Text, UShortValue) Then
            _Disk.BootSector.BytesPerSector = UShortValue
        End If

        If Byte.TryParse(CboSectorsPerCluster.Text, ByteValue) Then
            _Disk.BootSector.SectorsPerCluster = ByteValue
        End If

        If UShort.TryParse(TxtReservedSectors.Text, UShortValue) Then
            _Disk.BootSector.ReservedSectorCount = UShortValue
        End If

        If Byte.TryParse(TxtNumberOfFATs.Text, ByteValue) Then
            _Disk.BootSector.NumberOfFATs = ByteValue
        End If

        If UShort.TryParse(TxtRootDirectoryEntries.Text, UShortValue) Then
            _Disk.BootSector.RootEntryCount = UShortValue
        End If

        If UShort.TryParse(TxtSectorCountSmall.Text, UShortValue) Then
            _Disk.BootSector.SectorCountSmall = UShortValue
        End If

        _Disk.BootSector.MediaDescriptor = HexMediaDescriptor.GetHex(0)

        If UShort.TryParse(TxtSectorsPerFAT.Text, UShortValue) Then
            _Disk.BootSector.SectorsPerFAT = UShortValue
        End If

        If UShort.TryParse(TxtSectorsPerTrack.Text, UShortValue) Then
            _Disk.BootSector.SectorsPerTrack = UShortValue
        End If

        If UShort.TryParse(TxtNumberOfHeads.Text, UShortValue) Then
            _Disk.BootSector.NumberOfHeads = UShortValue
        End If

        If UShort.TryParse(TxtHiddenSectors.Text, UShortValue) Then
            _Disk.BootSector.HiddenSectors = UShortValue
        End If

        _Disk.BootSector.JmpBoot = HexJumpInstruction.GetHex

        Dim BootStrapSignature = HexBootSectorSignature.GetHex
        Array.Reverse(BootStrapSignature)
        _Disk.BootSector.BootStrapSignature = BitConverter.ToUInt16(BootStrapSignature, 0)

        If _HasExtended Then
            If UInteger.TryParse(TxtSectorCountLarge.Text, UintegerValue) Then
                _Disk.BootSector.SectorCountLarge = UintegerValue
            End If

            _Disk.BootSector.ExtendedBootSignature = HexExtendedBootSignature.GetHex(0)

            If Byte.TryParse(TxtDriveNumber.Text, ByteValue) Then
                _Disk.BootSector.DriveNumber = ByteValue
            End If

            Dim VolumeSerial = HexVolumeSerialNumber.GetHex
            Array.Reverse(VolumeSerial)
            _Disk.BootSector.VolumeSerialNumber = BitConverter.ToUInt32(VolumeSerial, 0)

            Dim VolumeLabelString As String = Strings.Left(TxtVolumeLabel.Text, 11).PadRight(11)
            Dim VolumeLabel = DiskImage.UnicodeToCodePage437(VolumeLabelString)
            _Disk.BootSector.VolumeLabel = VolumeLabel

            Dim FileSystemTypeString As String = Strings.Left(TxtFileSystemType.Text, 8).PadRight(8)
            Dim FileSystemType = DiskImage.UnicodeToCodePage437(FileSystemTypeString)
            _Disk.BootSector.FileSystemType = FileSystemType
        End If

        _Disk.Data.BatchEditMode = False
    End Sub

    Private Sub ChangeVolumeSerialNumber()
        Dim VolumeSerialNumberForm As New VolumeSerialNumberForm()

        VolumeSerialNumberForm.ShowDialog()

        Dim Result As Boolean = VolumeSerialNumberForm.DialogResult = DialogResult.OK

        If Result Then
            _SuppressEvent = True
            Dim Value = VolumeSerialNumberForm.GetValue()
            SetValue(HexVolumeSerialNumber, GenerateVolumeSerialNumber(Value).ToString("X8"))
            _SuppressEvent = False
        End If
    End Sub

#Region "Events"

    Private Sub BtnUpdate_Click(sender As Object, e As EventArgs) Handles BtnUpdate.Click
        UpdateBootSector()
    End Sub

    Private Sub CboOEMName_TextChanged(sender As Object, e As EventArgs) Handles CboOEMName.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim OEMName() As Byte

        If CboOEMName.SelectedIndex = -1 Then
            Dim OEMNameString As String = Strings.Left(CboOEMName.Text, 8).PadRight(8)
            OEMName = DiskImage.UnicodeToCodePage437(OEMNameString)
        Else
            OEMName = CType(CboOEMName.SelectedItem, KnownOEMName).Name
        End If

        HexOEMName.SetHex(OEMName)
    End Sub

    Private Sub HexOEMName_TextChanged(sender As Object, e As EventArgs) Handles HexOEMName.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        SetValue(CboOEMName, DiskImage.CodePage437ToUnicode(HexOEMName.GetHex).TrimEnd)
        _SuppressEvent = False
    End Sub

    Private Sub OEMNameForm_Load(sender As Object, e As EventArgs) Handles Me.Load
        ActiveControl = BtnCancel
    End Sub

    Private Sub TxtVolumeLabel_TextChanged(sender As Object, e As EventArgs) Handles TxtVolumeLabel.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim VolumeLabelString As String = Strings.Left(TxtVolumeLabel.Text, 11).PadRight(11)
        Dim VolumeLabel() As Byte = DiskImage.UnicodeToCodePage437(VolumeLabelString)
        HexVolumeLabel.SetHex(VolumeLabel)
    End Sub

    Private Sub HexVolumeLabel_TextChanged(sender As Object, e As EventArgs) Handles HexVolumeLabel.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        RefreshVolumeLabel()
        _SuppressEvent = False
    End Sub

    Private Sub TxtFileSystemType_TextChanged(sender As Object, e As EventArgs)
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim FileSystemTypeString As String = Strings.Left(TxtFileSystemType.Text, 8).PadRight(8)
        Dim FileSystemType() As Byte = DiskImage.UnicodeToCodePage437(FileSystemTypeString)
        HexVolumeLabel.SetHex(FileSystemType)
    End Sub

    Private Sub HexFileSystemType_TextChanged(sender As Object, e As EventArgs) Handles HexFileSystemType.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        RefreshFileSystemType()
        _SuppressEvent = False
    End Sub

    Private Sub TextBoxNumeric_KeyPress(sender As Object, e As KeyPressEventArgs) Handles CboBytesPerSector.KeyPress, CboSectorsPerCluster.KeyPress,
                                                                                    TxtReservedSectors.KeyPress, TxtNumberOfFATs.KeyPress,
                                                                                    TxtRootDirectoryEntries.KeyPress, TxtSectorCountSmall.KeyPress,
                                                                                    TxtSectorsPerFAT.KeyPress, TxtSectorsPerTrack.KeyPress,
                                                                                    TxtNumberOfHeads.KeyPress, TxtHiddenSectors.KeyPress,
                                                                                    TxtSectorCountLarge.KeyPress, TxtDriveNumber.KeyPress

        If Not Char.IsNumber(e.KeyChar) AndAlso Not Char.IsControl(e.KeyChar) Then
            e.Handled = True
        End If
    End Sub

    Private Sub TextBoxNumeric_LostFocus(sender As Object, e As EventArgs) Handles TxtReservedSectors.LostFocus, TxtNumberOfFATs.LostFocus,
                                                                            TxtRootDirectoryEntries.LostFocus, TxtSectorCountSmall.LostFocus,
                                                                            TxtSectorsPerFAT.LostFocus, TxtSectorsPerTrack.LostFocus,
                                                                            TxtNumberOfHeads.LostFocus, TxtHiddenSectors.LostFocus,
                                                                            TxtSectorCountLarge.LostFocus, TxtDriveNumber.LostFocus
        Dim TB As TextBox = sender
        Dim Result As UInteger
        If Not UInt32.TryParse(TB.Text, Result) Then
            RevertValue(TB)
        Else
            _SuppressEvent = True
            If TB.MaxLength = 3 And Result > 255 Then
                TB.Text = 255
            ElseIf TB.MaxLength = 5 And Result > 65535 Then
                TB.Text = 65535
            Else
                TB.Text = CUInt(TB.Text)
            End If
            If TB.Text <> CType(TB.Tag, FieldData).LastValue Then
                UpdateTag(TB)
                SetCurrentDiskType()
            End If
            _SuppressEvent = False
        End If
    End Sub

    Private Sub TextBoxText_LostFocus(sender As Object, e As EventArgs) Handles TxtVolumeLabel.LostFocus, TxtFileSystemType.LostFocus
        Dim TB = CType(sender, TextBox)
        TB.Text = TB.Text.TrimEnd
        UpdateTag(TB)
    End Sub

    Private Sub ComboBoxNumeric_LostFocus(sender As Object, e As EventArgs) Handles CboBytesPerSector.LostFocus, CboSectorsPerCluster.LostFocus
        Dim CB As ComboBox = sender
        Dim Result As UInteger
        If Not UInt32.TryParse(CB.Text, Result) Then
            RevertValue(CB)
        Else
            _SuppressEvent = True
            If CB.MaxLength = 3 And Result > 255 Then
                CB.Text = 255
            ElseIf CB.MaxLength = 5 And Result > 65535 Then
                CB.Text = 65535
            Else
                CB.Text = CUInt(CB.Text)
            End If
            If CB.Text <> CType(CB.Tag, FieldData).LastValue Then
                UpdateTag(CB)
                SetCurrentDiskType()
            End If
            _SuppressEvent = False
        End If
    End Sub

    Private Sub ComboBoxNumeric_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CboBytesPerSector.SelectedIndexChanged, CboSectorsPerCluster.SelectedIndexChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim CB As ComboBox = sender
        _SuppressEvent = True
        If CB.Text <> CType(CB.Tag, FieldData).LastValue Then
            UpdateTag(CB)
            SetCurrentDiskType()
        End If
        _SuppressEvent = False
    End Sub

    Private Sub ComboBoxText_LostFocus(sender As Object, e As EventArgs) Handles CboOEMName.LostFocus
        UpdateTag(CType(sender, ComboBox))
    End Sub

    Private Sub CboDiskType_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CboDiskType.SelectedIndexChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        Dim DiskType As BootSectorDiskType = CboDiskType.SelectedItem
        If DiskType.Type <> FloppyDiskType.FloppyUnknown Then
            Dim BootSector = BuildBootSectorFromType(DiskType.Type)
            PopulateBootRecord(BootSector)
        End If
        _SuppressEvent = False
    End Sub

    Private Sub HexMediaDescriptor_TextChanged(sender As Object, e As EventArgs) Handles HexMediaDescriptor.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        If HexMediaDescriptor.Text <> CType(HexMediaDescriptor.Tag, FieldData).LastValue Then
            UpdateTag(HexMediaDescriptor)
            SetCurrentDiskType()
        End If
        _SuppressEvent = False
    End Sub

    Private Sub CboDiskType_LostFocus(sender As Object, e As EventArgs) Handles CboDiskType.LostFocus
        _SuppressEvent = True
        Dim DiskType As BootSectorDiskType = CboDiskType.SelectedItem
        If DiskType.Type = FloppyDiskType.FloppyUnknown Then
            SetCurrentDiskType()
        End If
        _SuppressEvent = False
    End Sub

    Private Sub HexExtendedBootSignature_TextChanged(sender As Object, e As EventArgs) Handles HexExtendedBootSignature.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        _SuppressEvent = True
        Dim HB As HexTextBox = sender
        If HB.Text <> CType(HB.Tag, FieldData).LastValue Then
            UpdateTag(HB)
            SetExtendedState()
        End If
        _SuppressEvent = False
    End Sub

    Private Sub HexTextBox_TextChanged(sender As Object, e As EventArgs) Handles HexVolumeSerialNumber.TextChanged, HexBootSectorSignature.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim HB As HexTextBox = sender
        If HB.Text <> CType(HB.Tag, FieldData).LastValue Then
            UpdateTag(HB)
        End If
    End Sub

    Private Sub HexJumpInstruction_TextChanged(sender As Object, e As EventArgs) Handles HexJumpInstruction.TextChanged
        If _SuppressEvent Then
            Exit Sub
        End If

        Dim HB As HexTextBox = sender
        If HB.Text <> CType(HB.Tag, FieldData).LastValue Then
            Dim IsInvalid = Not BootSector.CheckJumpInstruction(HB.GetHex, True)
            UpdateTag(HB, IsInvalid)
        End If
    End Sub

#End Region
    Private Class FieldData
        Public Sub New(Value As String)
            _OriginalValue = Value
            _LastValue = Value
            _AllowedValues = Nothing
        End Sub
        Public Property OriginalValue As String
        Public Property LastValue As String
        Public Property AllowedValues As String()
    End Class

    Private Class BootSectorDiskType
        Public Sub New(Name As String, Type As FloppyDiskType)
            _Name = Name
            _Type = Type
        End Sub

        Public Property Name As String
        Public Property Type As FloppyDiskType

        Public Overrides Function ToString() As String
            Return _Name
        End Function
    End Class

    Private Sub ComboBox_DrawItem(sender As Object, e As DrawItemEventArgs) Handles CboBytesPerSector.DrawItem, CboSectorsPerCluster.DrawItem
        Dim CB As ComboBox = sender

        e.DrawBackground()

        If e.Index >= 0 Then
            Dim Brush As Brush
            Dim tBrush As Brush

            If e.State And DrawItemState.Selected Then
                Brush = New SolidBrush(SystemColors.Highlight)
                tBrush = New SolidBrush(SystemColors.HighlightText)
            Else
                Brush = New SolidBrush(SystemColors.Window)
                tBrush = New SolidBrush(SystemColors.WindowText)
            End If

            e.Graphics.FillRectangle(Brush, e.Bounds)
            e.Graphics.DrawString(CB.Items(e.Index).ToString, e.Font, tBrush, e.Bounds, StringFormat.GenericDefault)

            tBrush.Dispose()
            Brush.Dispose()
        End If

        e.DrawFocusRectangle()
    End Sub

    Private Sub BtnVolumeSerialNumber_Click(sender As Object, e As EventArgs) Handles BtnVolumeSerialNumber.Click
        ChangeVolumeSerialNumber()
    End Sub
End Class