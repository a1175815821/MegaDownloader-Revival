''' <summary>
''' P2-11 UI:左导航状态分组判定。纯谓词工具类:计数、Roots 子集、ChildrenGetter 包裹
''' 三处共用同一规则(包自身命中,或任一子文件命中;树只有包->文件两层),所见即所数。
''' 注意:最初用 OLV 自带 UseFiltering/ModelFilter 实现,但在本工程 VirtualMode
''' TreeListView 上会导致启动期静默退出(反射仅证明属性存在,不证明可用,已二分验证),
''' 故改用 Roots 子集 + ChildrenGetter 包裹,本类不再实现 IModelFilter。
''' 过滤在列表重建时生效(切换分组/增删/启停等);导航计数一侧永远实时。
''' </summary>
Friend Class DownloadEstadoFilter

    Public Enum NavScope
        All = 0
        Downloading = 1
        Waiting = 2
        Failed = 3
        Completed = 4
    End Enum

    Private Sub New()
    End Sub

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
            Case Estado.Completado
                Return NavScope.Completed
            Case Else
                ' 未知状态宁可落在已完成,也不在各分组里消失。
                Return NavScope.Completed
        End Select
    End Function

End Class
