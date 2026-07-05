Public Interface IDescarga


    Function DescargaPrioridad() As Integer

    Function DescargaNombre() As String

    Function DescargaTamanoBytes() As Long

    Function DescargaPorcentaje() As Decimal

    Function DescargaVelocidadKBs() As Decimal

    Function DescargaEstado() As Estado

    Function DescargaExtraccionAutomatica() As Boolean

    Function DescargaExtraccionPassword() As String

    Function DescargaTiempoEstimadoDescarga() As String

End Interface

Public Enum Estado
    Verificando
    CreandoLocal
    EnCola
    Descargando
    Pausado
    Completado
    ComprobandoMD5
    Descomprimiendo
    Erroneo
End Enum
