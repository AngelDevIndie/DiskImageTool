﻿Namespace DiskImage
    Public Interface IDirectory
        ReadOnly Property ClusterChain As List(Of UShort)
        ReadOnly Property Data As DirectoryData
        ReadOnly Property DirectoryEntries As List(Of DirectoryEntry)
        ReadOnly Property Disk As Disk
        ReadOnly Property IsRootDirectory As Boolean
        ReadOnly Property ParentEntry As DirectoryEntry
        ReadOnly Property SectorChain As List(Of UInteger)
        Function AddDirectory(DirectoryData() As Byte, Options As AddFileOptions, LFNFileName As String, Optional Index As Integer = -1) As AddDirectoryData
        Function AddFile(FilePath As String, Options As AddFileOptions, Optional Index As Integer = -1) As Integer
        Function AddFile(FileInfo As IO.FileInfo, Options As AddFileOptions, Optional Index As Integer = -1) As Integer
        Function GetAvailableFileName(FileName As String, UseNTExtensions As Boolean, Optional CurrentIndex As Integer = -1) As String
        Function GetContent() As Byte()
        Function GetFile(Index As UInteger) As DirectoryEntry
        Function GetIndex(DirectoryEntry As DirectoryEntry) As Integer
        Function GetAvailableEntry() As DirectoryEntry
        Function GetFileIndex(FileBytes() As Byte, IncludeDirectories As Boolean, Optional SkipIndex As Integer = -1) As Integer
        Function GetFileIndex(Filename As String, IncludeDirectories As Boolean, Optional SkipIndex As Integer = -1) As Integer
        Function RemoveEntry(Index As UInteger) As Boolean
        Function RemoveLFN(Index As UInteger) As Boolean
        Sub UpdateEntryCounts()
        Function UpdateLFN(FileName As String, Index As Integer, UseNTExtensions As Boolean) As Boolean
    End Interface
End Namespace
