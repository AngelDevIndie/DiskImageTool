﻿Imports System.IO
Imports System.Text

Namespace DiskImage
    Module Functions
        Public Function CalcXDFChecksum(Data() As Byte, SectorsPerFAT As UInteger) As UInteger
            Dim Checksum As UInteger = &H12345678

            Checksum = (Checksum << 1) + CalcXDFChecksumBlock(Data, 1, &HA00)
            Checksum = (Checksum << 1) + CalcXDFChecksumBlock(Data, (SectorsPerFAT << 1) + 1, &HA00)
            Checksum = (Checksum << 1) + CalcXDFChecksumBlock(Data, (SectorsPerFAT << 1) + 6, &HA00)

            For i = 0 To &HB6 - 1
                Dim Start = ((SectorsPerFAT << 1) + &H15) + Data(&H80 + i) + (Checksum And &H7FF)
                Checksum = (Checksum << 1) + CalcXDFChecksumBlock(Data, Start, &H200)
            Next

            Return Checksum
        End Function

        Public Function ClusterListToSectorList(BPB As BiosParameterBlock, ClusterList As List(Of UShort)) As List(Of UInteger)
            Dim SectorList As New List(Of UInteger)

            For Each Cluster In ClusterList
                Dim Sector = BPB.ClusterToSector(Cluster)
                For Index = 0 To BPB.SectorsPerCluster - 1
                    SectorList.Add(Sector + Index)
                Next
            Next

            Return SectorList
        End Function

        Public Function ClusterToSectorList(BPB As BiosParameterBlock, Cluster As UShort) As List(Of UInteger)
            Dim SectorList As New List(Of UInteger)

            Dim Sector = BPB.ClusterToSector(Cluster)
            For Index = 0 To BPB.SectorsPerCluster - 1
                SectorList.Add(Sector + Index)
            Next

            Return SectorList
        End Function

        Public Function CombineFileParts(Filename As String, Extension As String) As String
            Return Filename & If(Extension.Length > 0, ".", "") & Extension
        End Function

        Public Function DateToFATDate(D As Date) As UShort
            Dim FATDate As UShort = D.Year - 1980

            FATDate <<= 4
            FATDate += D.Month
            FATDate <<= 5
            FATDate += D.Day

            Return FATDate
        End Function

        Public Function DateToFATMilliseconds(D As Date) As Byte
            Return D.Millisecond \ 10 + (D.Second Mod 2) * 100
        End Function

        Public Function DateToFATTime(D As Date) As UShort
            Dim DTTime As UShort = D.Hour

            DTTime <<= 6
            DTTime += D.Minute
            DTTime <<= 5
            DTTime += D.Second \ 2

            Return DTTime
        End Function

        Public Function DirectoryEntryHasData(FloppyImage As IFloppyImage, Offset As UInteger) As Boolean
            Dim Result As Boolean = False

            If FloppyImage.GetByte(Offset) = &HE5 Then
                For Offset2 As UInteger = Offset + 1 To Offset + DirectoryEntry.DIRECTORY_ENTRY_SIZE - 1
                    If FloppyImage.GetByte(Offset2) <> 0 Then
                        Result = True
                        Exit For
                    End If
                Next
            ElseIf FloppyImage.GetByte(Offset) <> 0 Then
                Result = True
            Else
                Dim HexF6Count As UInteger = 0
                For Offset2 As UInteger = Offset + 1 To Offset + DirectoryEntry.DIRECTORY_ENTRY_SIZE - 1
                    If FloppyImage.GetByte(Offset2) = &HF6 Then
                        HexF6Count += 1
                    ElseIf FloppyImage.GetByte(Offset2) <> 0 Then
                        Result = True
                        Exit For
                    End If
                Next
                If Not Result Then
                    If HexF6Count > 0 And HexF6Count < DirectoryEntry.DIRECTORY_ENTRY_SIZE - 2 Then
                        Result = True
                    End If
                End If
            End If

            Return Result
        End Function

        Public Function DirectoryExpand(Disk As Disk, Directory As SubDirectory, FreeClusters As SortedSet(Of UShort)) As Boolean
            Dim Cluster = Disk.FAT.GetNextFreeCluster(FreeClusters, True)

            If Cluster = 0 Then
                Return False
            End If

            Directory.ExpandDirectorySize(Cluster)

            Return True
        End Function

        Public Function DOSCleanFileName(FileName As String, Optional MaxLength As Integer = -1) As String
            FileName = RemoveDiacritics(FileName)

            Dim FileBytes = Encoding.UTF8.GetBytes(FileName).ToList

            For i = FileBytes.Count - 1 To 0 Step -1
                If FileBytes(i) = &H20 Then 'Remove spaces
                    FileBytes.RemoveAt(i)
                ElseIf FileBytes(i) = &H2E Then 'Remove periods
                    FileBytes.RemoveAt(i)
                ElseIf DirectoryEntry.InvalidFileChars.Contains(FileBytes(i)) Then 'Replace invalid characters with underscores
                    FileBytes(i) = &H95
                ElseIf FileBytes(i) > 96 And FileBytes(i) < 123 Then 'Convert lowercase to uppercase
                    FileBytes(i) = FileBytes(i) - 32
                End If
            Next

            If MaxLength > -1 And FileBytes.Count > MaxLength Then
                Return Encoding.UTF8.GetString(FileBytes.ToArray, 0, MaxLength)
            Else
                Return Encoding.UTF8.GetString(FileBytes.ToArray)
            End If
        End Function

        Public Function GetBadSectors(BPB As BiosParameterBlock, BadClusters As List(Of UShort)) As HashSet(Of UInteger)
            Dim BadSectors As New HashSet(Of UInteger)

            For Each Cluster In BadClusters
                Dim Sector = BPB.ClusterToSector(Cluster)
                For Index = 0 To BPB.SectorsPerCluster - 1
                    BadSectors.Add(Sector + Index)
                Next
            Next

            Return BadSectors
        End Function

        Public Function GetDataFromChain(FloppyImage As IFloppyImage, SectorChain As List(Of UInteger)) As Byte()
            Dim SectorSize As UInteger = Disk.BYTES_PER_SECTOR
            Dim Content(SectorChain.Count * SectorSize - 1) As Byte
            Dim ContentOffset As UInteger = 0

            For Each Sector In SectorChain
                Dim Offset As UInteger = Disk.SectorToBytes(Sector)
                If FloppyImage.Length < Offset + SectorSize Then
                    SectorSize = Math.Max(FloppyImage.Length - Offset, 0)
                End If
                If SectorSize > 0 Then
                    FloppyImage.CopyTo(Offset, Content, ContentOffset, SectorSize)
                    ContentOffset += SectorSize
                Else
                    Exit For
                End If
            Next

            Return Content
        End Function

        Public Function GetLFNDirectoryEntries(FileName As String, ShortName As String) As List(Of Byte())
            Dim Entries = New List(Of Byte())

            If FileName = ShortName Then
                Return Entries
            End If

            FileName = Left(FileName, 255)

            Dim Buffer() As Byte
            Dim LFNBuffer() As Byte

            Dim FileBytes = System.Text.Encoding.Unicode.GetBytes(FileName)
            Dim Count = Math.Ceiling(FileBytes.Length / 26)

            For i = 0 To Count - 1
                Dim Offset As Long = i * 26
                Dim Length As Long = Math.Min(FileBytes.Length - Offset, 26)

                Buffer = New Byte(25) {}
                For j = 0 To Buffer.Length - 1
                    Buffer(j) = &HFF
                Next
                Array.Copy(FileBytes, Offset, Buffer, 0, Length)
                If Length < 26 Then
                    Buffer(Length) = 0
                    Buffer(Length + 1) = 0
                End If

                LFNBuffer = New Byte(31) {}
                If i = Count - 1 Then
                    LFNBuffer(0) = (i + 1) Or &H40
                Else
                    LFNBuffer(0) = i + 1
                End If
                Array.Copy(Buffer, 0, LFNBuffer, 1, 10)
                LFNBuffer(11) = &HF
                LFNBuffer(12) = &H0
                LFNBuffer(13) = 0
                Array.Copy(Buffer, 10, LFNBuffer, 14, 12)
                LFNBuffer(26) = 0
                LFNBuffer(27) = 0
                Array.Copy(Buffer, 22, LFNBuffer, 28, 4)

                Entries.Add(LFNBuffer)
            Next

            Entries.Reverse()

            Return Entries
        End Function

        Public Function GetShortFileChecksum(Filename As String) As UShort
            Dim Checksum As UShort = 0

            For i As Integer = 0 To Filename.Length - 1
                Checksum = (Checksum * &H25 + AscW(Filename(i))) And &HFFFF&
            Next

            Dim temp As UInteger = CLng(Checksum) * 314159269 And &HFFFFFFFF&

            Dim temp2 As Integer

            If temp > Integer.MaxValue Then
                temp2 = (UInteger.MaxValue - temp + 1)
            Else
                temp2 = temp
            End If

            temp2 -= (CType((CLng(temp2) * 1152921497) >> 60, ULong) * 1000000007)

            Checksum = temp2 And &HFFFF&

            ' Reverse nibble order
            Checksum = CUShort(
                ((Checksum And &HF000) >> 12) Or
                ((Checksum And &HF00) >> 4) Or
                ((Checksum And &HF0) << 4) Or
                ((Checksum And &HF) << 12)
            )

            Return Checksum
        End Function

        Public Function GetShortFileChecksumString(Filename As String) As String
            Return GetShortFileChecksum(Filename).ToString("X4")
        End Function

        Public Function InitializeAddDirectory(Directory As DirectoryBase, Options As AddFileOptions, LFNFileName As String, Index As Integer) As AddDirectoryData
            Dim ClustersRequired As UShort = 1

            Dim AddDirectoryData As AddDirectoryData

            AddDirectoryData.Entry = Nothing
            AddDirectoryData.Index = Index
            AddDirectoryData.Options = Options
            If Options.LFN Then
                AddDirectoryData.ShortFileName = Directory.GetAvailableFileName(LFNFileName, Options.NTExtensions)
                AddDirectoryData.LFNEntries = GetLFNDirectoryEntries(LFNFileName, AddDirectoryData.ShortFileName)
                AddDirectoryData.EntriesNeeded = AddDirectoryData.LFNEntries.Count + 1
            Else
                AddDirectoryData.ShortFileName = ""
                AddDirectoryData.LFNEntries = Nothing
                AddDirectoryData.EntriesNeeded = 1
            End If


            AddDirectoryData.RequiresExpansion = Directory.Data.AvailableEntryCount < AddDirectoryData.EntriesNeeded
            If AddDirectoryData.RequiresExpansion Then
                ClustersRequired += 1
            End If

            If AddDirectoryData.RequiresExpansion And Directory.IsRootDirectory Then
                AddDirectoryData.ClusterList = Nothing
            Else
                AddDirectoryData.ClusterList = Directory.Disk.FAT.GetFreeClusters(ClustersRequired)
            End If

            Return AddDirectoryData
        End Function

        Public Function InitializeAddFile(Directory As DirectoryBase, FileInfo As FileInfo, Options As AddFileOptions, Index As Integer) As AddFileData
            Dim ClustersRequired As UShort = Math.Ceiling(FileInfo.Length / Directory.Disk.BPB.BytesPerCluster)

            Dim AddFileData As AddFileData

            AddFileData.FilePath = FileInfo.FullName
            AddFileData.Options = Options
            AddFileData.Index = Index
            AddFileData.ShortFileName = Directory.GetAvailableFileName(FileInfo.Name, Options.NTExtensions)

            If Options.LFN Then
                AddFileData.LFNEntries = GetLFNDirectoryEntries(FileInfo.Name, AddFileData.ShortFileName)
                AddFileData.EntriesNeeded = AddFileData.LFNEntries.Count + 1
            Else
                AddFileData.LFNEntries = Nothing
                AddFileData.EntriesNeeded = 1
            End If

            AddFileData.RequiresExpansion = Directory.Data.AvailableEntryCount < AddFileData.EntriesNeeded
            If AddFileData.RequiresExpansion Then
                ClustersRequired += 1
            End If

            If AddFileData.RequiresExpansion And Directory.IsRootDirectory Then
                AddFileData.ClusterList = Nothing
            Else
                AddFileData.ClusterList = Directory.Disk.FAT.GetFreeClusters(ClustersRequired)
            End If

            Return AddFileData
        End Function
        Public Function InitializeUpdateLFN(Directory As DirectoryBase, FileName As String, Index As Integer, UseNTExtensions As Boolean) As UpdateLFNData
            Dim UpdateLFNData As UpdateLFNData

            UpdateLFNData.RequiresExpansion = False
            UpdateLFNData.DirectoryEntry = Directory.DirectoryEntries.Item(Index)
            UpdateLFNData.CurrentLFNIndex = Directory.AdjustIndexForLFN(Index)
            UpdateLFNData.ShortFileName = Directory.GetAvailableFileName(FileName, UseNTExtensions, Index)

            If FileName.ToUpper <> UpdateLFNData.ShortFileName Then
                UseNTExtensions = False
            End If

            If UseNTExtensions Then
                Dim ShortFileParts = SplitFilename(UpdateLFNData.ShortFileName)
                Dim LongFileParts = SplitFilename(FileName)

                UpdateLFNData.NTLowerCaseFileName = ShortFileParts.Name.ToLower = LongFileParts.Name
                UpdateLFNData.NTLowerCaseExtension = ShortFileParts.Extension.ToLower = LongFileParts.Extension
                UseNTExtensions = UpdateLFNData.NTLowerCaseFileName Or UpdateLFNData.NTLowerCaseExtension
            Else
                UpdateLFNData.NTLowerCaseFileName = False
                UpdateLFNData.NTLowerCaseExtension = False
            End If

            If UseNTExtensions Then
                UpdateLFNData.LFNEntries = New List(Of Byte())
            Else
                UpdateLFNData.LFNEntries = GetLFNDirectoryEntries(FileName, UpdateLFNData.ShortFileName)
            End If

            Dim CurrentLFNEntryCount = Index - UpdateLFNData.CurrentLFNIndex
            UpdateLFNData.EntriesNeeded = UpdateLFNData.LFNEntries.Count - CurrentLFNEntryCount

            If UpdateLFNData.EntriesNeeded > 0 Then
                If Directory.Data.AvailableEntryCount < UpdateLFNData.EntriesNeeded Then
                    UpdateLFNData.RequiresExpansion = True
                End If
            End If

            Return UpdateLFNData
        End Function

        Public Function ProcessAddDirectory(Directory As DirectoryBase, DirectoryData() As Byte, Data As AddDirectoryData) As DirectoryEntry
            Dim EntryCount = Directory.DirectoryEntries.Count - Directory.Data.AvailableEntryCount
            Dim Entries As List(Of DirectoryEntry)

            If Data.Index > -1 Then
                Data.Index = Directory.AdjustIndexForLFN(Data.Index)
                Directory.ShiftEntries(Data.Index, EntryCount, Data.EntriesNeeded)
                Entries = Directory.GetEntries(Data.Index, Data.EntriesNeeded)
            Else
                Entries = Directory.GetEntries(EntryCount, Data.EntriesNeeded)
            End If

            Dim Cluster = Directory.Disk.FAT.GetNextFreeCluster(Data.ClusterList, True)
            Directory.Disk.FATTables.UpdateTableEntry(Cluster, FAT12.FAT_LAST_CLUSTER_END)

            Dim Entry = New DirectoryEntryBase(DirectoryData) With {
                .StartingCluster = Cluster
            }
            If Data.Options.LFN Then
                Entry.SetFileName(Data.ShortFileName)
            End If

            Dim DirectoryEntry = Entries(Entries.Count - 1)
            DirectoryEntry.Data = Entry.Data
            DirectoryEntry.InitFatChain()
            DirectoryEntry.InitSubDirectory()

            If Data.Options.LFN Then
                ProcessLFNEntries(Entries, Data.LFNEntries)
            End If

            DirectoryEntry.SubDirectory.Initialize()

            Directory.UpdateEntryCounts()

            Return DirectoryEntry
        End Function

        Public Sub ProcessAddFile(Directory As DirectoryBase, Data As AddFileData)
            Dim EntryCount = Directory.DirectoryEntries.Count - Directory.Data.AvailableEntryCount
            Dim Entries As List(Of DirectoryEntry)

            If Data.Index > -1 Then
                Data.Index = Directory.AdjustIndexForLFN(Data.Index)
                Directory.ShiftEntries(Data.Index, EntryCount, Data.EntriesNeeded)
                Entries = Directory.GetEntries(Data.Index, Data.EntriesNeeded)
            Else
                Entries = Directory.GetEntries(EntryCount, Data.EntriesNeeded)
            End If

            Dim DirectoryEntry = Entries(Entries.Count - 1)
            DirectoryEntry.AddFile(Data.FilePath, Data.ShortFileName, Data.Options.CreatedDate, Data.Options.LastAccessedDate, Data.ClusterList)

            If Data.Options.LFN Then
                ProcessLFNEntries(Entries, Data.LFNEntries)
            End If

            Directory.UpdateEntryCounts()
        End Sub

        Public Sub ProcessLFNEntries(DirectoryEntries As List(Of DirectoryEntry), LFNEntries As List(Of Byte()))
            Dim DirectoryEntry = DirectoryEntries(DirectoryEntries.Count - 1)
            Dim Checksum = DirectoryEntry.CalculateLFNChecksum
            For Counter = 0 To LFNEntries.Count - 1
                Dim Buffer = LFNEntries(Counter)
                Buffer(13) = Checksum
                DirectoryEntries(Counter).Data = Buffer
            Next
        End Sub

        Public Sub ProcessUpdateLFN(Directory As DirectoryBase, Data As UpdateLFNData)
            If Data.EntriesNeeded <> 0 Then
                Dim EntryCount = Directory.DirectoryEntries.Count - Directory.Data.AvailableEntryCount
                Directory.ShiftEntries(Data.CurrentLFNIndex, EntryCount, Data.EntriesNeeded)
            End If

            Dim NewEntry = Data.DirectoryEntry.Clone
            NewEntry.SetFileName(Data.ShortFileName)
            NewEntry.HasNTLowerCaseFileName = Data.NTLowerCaseFileName
            NewEntry.HasNTLowerCaseExtension = Data.NTLowerCaseExtension

            Data.DirectoryEntry.Data = NewEntry.Data

            If Data.LFNEntries.Count > 0 Then
                Dim Entries = Directory.GetEntries(Data.CurrentLFNIndex, Data.LFNEntries.Count + 1)
                ProcessLFNEntries(Entries, Data.LFNEntries)
            End If

            Directory.UpdateEntryCounts()
        End Sub

        Public Function ReadFileIntoBuffer(FileInfo As IO.FileInfo, FileSize As UInteger, FillChar As Byte) As Byte()
            Dim FileBuffer(FileSize - 1) As Byte
            Dim n As Integer
            Using fs = FileInfo.OpenRead()
                n = fs.Read(FileBuffer, 0, Math.Min(FileInfo.Length, FileBuffer.Length))
            End Using
            For Counter As Integer = n To FileBuffer.Length - 1
                FileBuffer(Counter) = FillChar
            Next

            Return FileBuffer
        End Function

        Public Function TruncateFileName(Filename As String, Extension As String, Checksum As String, Index As UInteger, UseNTExtensions As Boolean) As String
            Dim Suffix = ""
            Dim UseChecksum As Boolean = False

            If UseNTExtensions Then
                If Filename.Length < 3 Or Index > 4 Then
                    UseChecksum = True
                End If
                If Index > 4 Then
                    Index -= 4
                End If
            End If

            If UseChecksum Then
                Suffix &= Checksum
            End If
            Suffix = Suffix & "~" & Index
            Dim Length = 8 - Suffix.Length
            If Length > Filename.Length Then
                Length = Filename.Length
            End If

            Filename = Filename.Substring(0, Length) & Suffix

            Return CombineFileParts(Filename, Extension)
        End Function

        Private Function CalcXDFChecksumBlock(Data() As Byte, Start As UInteger, Length As UShort) As UInteger
            Dim Checksum As UInt32 = &HABDC
            Dim Loc2 As UInt16

            Start <<= 9

            For i = 0 To Length - 1
                Loc2 = Data((Data(i + Start) * &H13) Mod Length + Start)
                Checksum = (Checksum + (Loc2 >> 5) + ((Loc2 And &H1F) << 4)) And &HFFFF&
            Next

            Return Checksum
        End Function

        Private Function RemoveDiacritics(value As String) As String
            Dim NormalizedString = value.Normalize(NormalizationForm.FormD)
            Dim SB = New StringBuilder(NormalizedString.Length)

            For i = 0 To NormalizedString.Length - 1
                Dim c = NormalizedString(i)
                Dim UnicodeCategory = Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                If UnicodeCategory <> Globalization.UnicodeCategory.NonSpacingMark Then
                    SB.Append(c)
                End If
            Next

            Return SB.ToString.Normalize(NormalizationForm.FormC)
        End Function
    End Module
End Namespace
