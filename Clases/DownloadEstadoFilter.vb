''' <summary>
''' P2-11 UI:左导航状态过滤。IModelFilter 实现,只读 DescargaEstado 做判定,
''' 不碰数据源(SetObjects)/ChildrenGetter/刷新节拍。包节点规则:自身命中,
''' 或任一子文件命中(树只有包->文件两层),与导航计数共用同一谓词,所见即所数。
''' 注意:过滤在 OLV 重建列表时生效(切换分组/增删/启停等),后台状态流转
''' (如下载中->完成)在下次重建前仍停留在原分组,导航计数一侧永远实时。
''' </summary>
Friend Class DownloadEstadoFilter
    Implements BrightIdeasSoftware.IModelFilter

    Public Enum NavScope
        All = 0
        Downloading = 1
        Waiting = 2
        Failed = 3
        Completed = 4
    End Enum

    Private ReadOnly _scope As NavScope

    Public Sub New(scope As NavScope)
        _scope = scope
    End Sub

    Public Function Filter(modelObject As Object) As Boolean Implements BrightIdeasSoftware.IModelFilter.Filter
        Try
            Return MatchesScope(modelObject, _scope)
        Catch ex As Exception
            Log.WriteDebug("DownloadEstadoFilter failed: " & Log.SafeException(ex))
            Return True
        End Try
    End Function

    ''' <summary>对象是否属于分组(含包的子文件穿透)。计数与过滤共用,保证一致。</summary>
    Public Shared Function MatchesScope(modelObject As Object, scope As NavScope) As Boolean
        If scope = NavScope.All Then Return True
        Dim d As IDescarga = TryCast(modelObject, IDescarga)
        If d Is Nothing Then Return True
        If ScopeOf(d.DescargaEstado()) = scope Then Return True
        Dim p As Paquete = TryCast(modelObject, Paquete)
        If p IsNot Nothing AndAlso p.ListaFicheros IsNot Nothing Then
            For Each f As Fichero In p.ListaFicheros
                If ScopeOf(f.DescargaEstado()) = scope Then Return True
            Next
        End If
        Return False
    End Function

    Public Shared Function ScopeOf(st As Estado) As NavScope
        Select Case st
            Case Estado.Descargando, Estado.Verificando, Estado.Descomprimiendo,
                 Estado.CreandoLocal, Estado.ComprobandoMD5
                Return NavScope.Downloading
            Case Estado.EnCola, Estado.Pausado
                Return NavScope.Waiting
            Case Estado.Erroneo
                Return NavScope.Failed
            Case Else
                Return NavScope.Completed
        End Select
    End Function

End Class
