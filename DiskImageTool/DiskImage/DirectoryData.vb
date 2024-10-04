﻿Namespace DiskImage
    Public Class DirectoryData
        Public Property BootSectorOffset As UInteger = 0
        Public Property DeletedFileCount As UInteger = 0
        Public Property EntryCount As UInteger = 0
        Public Property PopulatedEntryCount As UInteger = 0
        Public Property FileCount As UInteger = 0
        Public Property HasAdditionalData As Boolean = False
        Public Property HasBootSector As Boolean = False
        Public Property MaxEntries As UInteger = 0
        Public Property EndOfDirectory As Boolean = False
    End Class
End Namespace
