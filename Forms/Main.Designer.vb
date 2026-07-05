<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Main
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.ListaDescargas = New BrightIdeasSoftware.TreeListView()
        Me.OlvColumnPrioridad = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnNombre = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnDescargado = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnTamano = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnEstado = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnProgresoPorc = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnProgreso = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnVelocidad = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnEDT = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.OlvColumnRestante = CType(New BrightIdeasSoftware.OLVColumn(), BrightIdeasSoftware.OLVColumn)
        Me.PanelButtonsRight = New System.Windows.Forms.Panel()
        Me.btnCollaborate = New System.Windows.Forms.Button()
        Me.btnConfig = New System.Windows.Forms.Button()
        Me.MenuDescarga = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.AbrirEnCarpetaToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripSeparator2 = New System.Windows.Forms.ToolStripSeparator()
        Me.SubirPrioridadMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.BajarPrioridadMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripSeparator3 = New System.Windows.Forms.ToolStripSeparator()
        Me.ForceDownloadStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.PausarStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.EliminarMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.EliminarYBorrarMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripSeparator1 = New System.Windows.Forms.ToolStripSeparator()
        Me.VerErrorToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.VerLinksToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.VerLinksDescToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.OcultarEnlacesImagenMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ToolStripSeparator4 = New System.Windows.Forms.ToolStripSeparator()
        Me.VerProgresoDescompresionToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ResetToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.LimpiarCompletados2ToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.PropiedadesToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.MenuPanel = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.AgregarLinksToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.LimpiarCompletadosToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.IconoMinimizado = New System.Windows.Forms.NotifyIcon(Me.components)
        Me.MenuMinimizado = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.AbrirToolStripMenuItem1 = New System.Windows.Forms.ToolStripMenuItem()
        Me.AgregarLinkStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.CerrarToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.TableLayoutPanel1 = New System.Windows.Forms.TableLayoutPanel()
        Me.btnPlay = New System.Windows.Forms.Button()
        Me.btnPause = New System.Windows.Forms.Button()
        Me.btnStop = New System.Windows.Forms.Button()
        Me.btnAddLink = New System.Windows.Forms.Button()
        Me.btnUpdate = New System.Windows.Forms.Button()
        Me.StatusStrip1 = New System.Windows.Forms.StatusStrip()
        Me.StatusToolStripStatusLabel = New System.Windows.Forms.ToolStripStatusLabel()
        Me.RAMProcToolStripStatusLabel = New System.Windows.Forms.ToolStripStatusLabel()
        Me.ToolTipBotones = New System.Windows.Forms.ToolTip(Me.components)
        CType(Me.ListaDescargas, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.PanelButtonsRight.SuspendLayout()
        Me.MenuDescarga.SuspendLayout()
        Me.MenuPanel.SuspendLayout()
        Me.MenuMinimizado.SuspendLayout()
        Me.TableLayoutPanel1.SuspendLayout()
        Me.StatusStrip1.SuspendLayout()
        Me.SuspendLayout()
        '
        'ListaDescargas
        '
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnPrioridad)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnNombre)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnDescargado)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnTamano)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnEstado)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnProgresoPorc)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnProgreso)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnVelocidad)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnEDT)
        Me.ListaDescargas.AllColumns.Add(Me.OlvColumnRestante)
        Me.ListaDescargas.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ListaDescargas.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.OlvColumnPrioridad, Me.OlvColumnNombre, Me.OlvColumnDescargado, Me.OlvColumnTamano, Me.OlvColumnEstado, Me.OlvColumnProgreso, Me.OlvColumnVelocidad, Me.OlvColumnEDT})
        Me.ListaDescargas.Location = New System.Drawing.Point(12, 46)
        Me.ListaDescargas.Name = "ListaDescargas"
        Me.ListaDescargas.OwnerDraw = True
        Me.ListaDescargas.ShowGroups = False
        Me.ListaDescargas.Size = New System.Drawing.Size(580, 311)
        Me.ListaDescargas.TabIndex = 0
        Me.ListaDescargas.UseCompatibleStateImageBehavior = False
        Me.ListaDescargas.View = System.Windows.Forms.View.Details
        Me.ListaDescargas.VirtualMode = True
        '
        'OlvColumnPrioridad
        '
        Me.OlvColumnPrioridad.Hideable = False
        Me.OlvColumnPrioridad.Text = "#"
        Me.OlvColumnPrioridad.Width = 20
        '
        'OlvColumnNombre
        '
        Me.OlvColumnNombre.FillsFreeSpace = True
        Me.OlvColumnNombre.Hideable = False
        Me.OlvColumnNombre.Text = "Nombre"
        Me.OlvColumnNombre.Width = 185
        '
        'OlvColumnDescargado
        '
        Me.OlvColumnDescargado.Text = "Descargado"
        Me.OlvColumnDescargado.Width = 70
        '
        'OlvColumnTamano
        '
        Me.OlvColumnTamano.Text = "Tamaño"
        Me.OlvColumnTamano.Width = 70
        '
        'OlvColumnEstado
        '
        Me.OlvColumnEstado.Text = "Estado"
        Me.OlvColumnEstado.Width = 50
        '
        'OlvColumnProgresoPorc
        '
        Me.OlvColumnProgresoPorc.IsVisible = False
        Me.OlvColumnProgresoPorc.Text = "Progreso %"
        '
        'OlvColumnProgreso
        '
        Me.OlvColumnProgreso.Text = "Progreso"
        Me.OlvColumnProgreso.Width = 80
        '
        'OlvColumnVelocidad
        '
        Me.OlvColumnVelocidad.Text = "Velocidad"
        Me.OlvColumnVelocidad.Width = 77
        '
        'OlvColumnEDT
        '
        Me.OlvColumnEDT.Text = "Estimado"
        Me.OlvColumnEDT.Width = 70
        '
        'OlvColumnRestante
        '
        Me.OlvColumnRestante.IsVisible = False
        Me.OlvColumnRestante.Text = "Restante"
        '
        'PanelButtonsRight
        '
        Me.PanelButtonsRight.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.PanelButtonsRight.Controls.Add(Me.btnCollaborate)
        Me.PanelButtonsRight.Controls.Add(Me.btnConfig)
        Me.PanelButtonsRight.Location = New System.Drawing.Point(512, 3)
        Me.PanelButtonsRight.Name = "PanelButtonsRight"
        Me.PanelButtonsRight.Size = New System.Drawing.Size(80, 34)
        Me.PanelButtonsRight.TabIndex = 0
        '
        'btnCollaborate
        '
        Me.btnCollaborate.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCollaborate.Location = New System.Drawing.Point(6, 0)
        Me.btnCollaborate.Name = "btnCollaborate"
        Me.btnCollaborate.Size = New System.Drawing.Size(35, 34)
        Me.btnCollaborate.TabIndex = 6
        Me.ToolTipBotones.SetToolTip(Me.btnCollaborate, "Colaborar")
        Me.btnCollaborate.UseVisualStyleBackColor = True
        '
        'btnConfig
        '
        Me.btnConfig.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnConfig.Location = New System.Drawing.Point(46, 0)
        Me.btnConfig.Name = "btnConfig"
        Me.btnConfig.Size = New System.Drawing.Size(35, 34)
        Me.btnConfig.TabIndex = 7
        Me.ToolTipBotones.SetToolTip(Me.btnConfig, "Configuración")
        Me.btnConfig.UseVisualStyleBackColor = True
        '
        'MenuDescarga
        '
        Me.MenuDescarga.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.AbrirEnCarpetaToolStripMenuItem, Me.ToolStripSeparator2, Me.SubirPrioridadMenuItem, Me.BajarPrioridadMenuItem, Me.ToolStripSeparator3, Me.ForceDownloadStripMenuItem, Me.PausarStripMenuItem, Me.EliminarMenuItem, Me.EliminarYBorrarMenuItem, Me.ToolStripSeparator1, Me.VerErrorToolStripMenuItem, Me.VerLinksToolStripMenuItem, Me.VerLinksDescToolStripMenuItem, Me.OcultarEnlacesImagenMenuItem, Me.ToolStripSeparator4, Me.VerProgresoDescompresionToolStripMenuItem, Me.ResetToolStripMenuItem, Me.LimpiarCompletados2ToolStripMenuItem, Me.PropiedadesToolStripMenuItem})
        Me.MenuDescarga.Name = "MenuDescarga"
        Me.MenuDescarga.Size = New System.Drawing.Size(226, 336)
        '
        'AbrirEnCarpetaToolStripMenuItem
        '
        Me.AbrirEnCarpetaToolStripMenuItem.Name = "AbrirEnCarpetaToolStripMenuItem"
        Me.AbrirEnCarpetaToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.AbrirEnCarpetaToolStripMenuItem.Text = "Abrir directorio"
        '
        'ToolStripSeparator2
        '
        Me.ToolStripSeparator2.Name = "ToolStripSeparator2"
        Me.ToolStripSeparator2.Size = New System.Drawing.Size(222, 6)
        '
        'SubirPrioridadMenuItem
        '
        Me.SubirPrioridadMenuItem.Name = "SubirPrioridadMenuItem"
        Me.SubirPrioridadMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.SubirPrioridadMenuItem.Text = "Subir prioridad"
        '
        'BajarPrioridadMenuItem
        '
        Me.BajarPrioridadMenuItem.Name = "BajarPrioridadMenuItem"
        Me.BajarPrioridadMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.BajarPrioridadMenuItem.Text = "Bajar prioridad"
        '
        'ToolStripSeparator3
        '
        Me.ToolStripSeparator3.Name = "ToolStripSeparator3"
        Me.ToolStripSeparator3.Size = New System.Drawing.Size(222, 6)
        '
        'ForceDownloadStripMenuItem
        '
        Me.ForceDownloadStripMenuItem.Name = "ForceDownloadStripMenuItem"
        Me.ForceDownloadStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.ForceDownloadStripMenuItem.Text = "Forzar descarga"
        '
        'PausarStripMenuItem
        '
        Me.PausarStripMenuItem.Name = "PausarStripMenuItem"
        Me.PausarStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.PausarStripMenuItem.Text = "Poner en pausa"
        '
        'EliminarMenuItem
        '
        Me.EliminarMenuItem.Name = "EliminarMenuItem"
        Me.EliminarMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.EliminarMenuItem.Text = "Eliminar"
        '
        'EliminarYBorrarMenuItem
        '
        Me.EliminarYBorrarMenuItem.Name = "EliminarYBorrarMenuItem"
        Me.EliminarYBorrarMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.EliminarYBorrarMenuItem.Text = "Eliminar y borrar"
        '
        'ToolStripSeparator1
        '
        Me.ToolStripSeparator1.Name = "ToolStripSeparator1"
        Me.ToolStripSeparator1.Size = New System.Drawing.Size(222, 6)
        '
        'VerErrorToolStripMenuItem
        '
        Me.VerErrorToolStripMenuItem.Name = "VerErrorToolStripMenuItem"
        Me.VerErrorToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.VerErrorToolStripMenuItem.Text = "Ver error"
        '
        'VerLinksToolStripMenuItem
        '
        Me.VerLinksToolStripMenuItem.Name = "VerLinksToolStripMenuItem"
        Me.VerLinksToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.VerLinksToolStripMenuItem.Text = "Ver links"
        '
        'VerLinksDescToolStripMenuItem
        '
        Me.VerLinksDescToolStripMenuItem.Name = "VerLinksDescToolStripMenuItem"
        Me.VerLinksDescToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.VerLinksDescToolStripMenuItem.Text = "Ver links + desc"
        '
        'OcultarEnlacesImagenMenuItem
        '
        Me.OcultarEnlacesImagenMenuItem.Name = "OcultarEnlacesImagenMenuItem"
        Me.OcultarEnlacesImagenMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.OcultarEnlacesImagenMenuItem.Text = "Ocultar en imagen"
        '
        'ToolStripSeparator4
        '
        Me.ToolStripSeparator4.Name = "ToolStripSeparator4"
        Me.ToolStripSeparator4.Size = New System.Drawing.Size(222, 6)
        '
        'VerProgresoDescompresionToolStripMenuItem
        '
        Me.VerProgresoDescompresionToolStripMenuItem.Name = "VerProgresoDescompresionToolStripMenuItem"
        Me.VerProgresoDescompresionToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.VerProgresoDescompresionToolStripMenuItem.Text = "Ver progreso descompresión"
        '
        'ResetToolStripMenuItem
        '
        Me.ResetToolStripMenuItem.Name = "ResetToolStripMenuItem"
        Me.ResetToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.ResetToolStripMenuItem.Text = "Reset"
        '
        'LimpiarCompletados2ToolStripMenuItem
        '
        Me.LimpiarCompletados2ToolStripMenuItem.Name = "LimpiarCompletados2ToolStripMenuItem"
        Me.LimpiarCompletados2ToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.LimpiarCompletados2ToolStripMenuItem.Text = "Limpiar completados"
        '
        'PropiedadesToolStripMenuItem
        '
        Me.PropiedadesToolStripMenuItem.Enabled = False
        Me.PropiedadesToolStripMenuItem.Name = "PropiedadesToolStripMenuItem"
        Me.PropiedadesToolStripMenuItem.Size = New System.Drawing.Size(225, 22)
        Me.PropiedadesToolStripMenuItem.Text = "Propiedades"
        '
        'MenuPanel
        '
        Me.MenuPanel.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.AgregarLinksToolStripMenuItem, Me.LimpiarCompletadosToolStripMenuItem})
        Me.MenuPanel.Name = "MenuPanel"
        Me.MenuPanel.Size = New System.Drawing.Size(187, 48)
        '
        'AgregarLinksToolStripMenuItem
        '
        Me.AgregarLinksToolStripMenuItem.Name = "AgregarLinksToolStripMenuItem"
        Me.AgregarLinksToolStripMenuItem.Size = New System.Drawing.Size(186, 22)
        Me.AgregarLinksToolStripMenuItem.Text = "Agregar links"
        '
        'LimpiarCompletadosToolStripMenuItem
        '
        Me.LimpiarCompletadosToolStripMenuItem.Name = "LimpiarCompletadosToolStripMenuItem"
        Me.LimpiarCompletadosToolStripMenuItem.Size = New System.Drawing.Size(186, 22)
        Me.LimpiarCompletadosToolStripMenuItem.Text = "Limpiar completados"
        '
        'IconoMinimizado
        '
        Me.IconoMinimizado.ContextMenuStrip = Me.MenuMinimizado
        Me.IconoMinimizado.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.IconoMinimizado.Text = "NotifyIcon1"
        Me.IconoMinimizado.Visible = True
        '
        'MenuMinimizado
        '
        Me.MenuMinimizado.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.AbrirToolStripMenuItem1, Me.AgregarLinkStripMenuItem, Me.CerrarToolStripMenuItem})
        Me.MenuMinimizado.Name = "MenuMinimizado"
        Me.MenuMinimizado.Size = New System.Drawing.Size(139, 70)
        '
        'AbrirToolStripMenuItem1
        '
        Me.AbrirToolStripMenuItem1.Name = "AbrirToolStripMenuItem1"
        Me.AbrirToolStripMenuItem1.Size = New System.Drawing.Size(138, 22)
        Me.AbrirToolStripMenuItem1.Text = "Abrir"
        '
        'AgregarLinkStripMenuItem
        '
        Me.AgregarLinkStripMenuItem.Name = "AgregarLinkStripMenuItem"
        Me.AgregarLinkStripMenuItem.Size = New System.Drawing.Size(138, 22)
        Me.AgregarLinkStripMenuItem.Text = "Agregar link"
        '
        'CerrarToolStripMenuItem
        '
        Me.CerrarToolStripMenuItem.Name = "CerrarToolStripMenuItem"
        Me.CerrarToolStripMenuItem.Size = New System.Drawing.Size(138, 22)
        Me.CerrarToolStripMenuItem.Text = "Cerrar"
        '
        'TableLayoutPanel1
        '
        Me.TableLayoutPanel1.ColumnCount = 6
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 55.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle())
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120.0!))
        Me.TableLayoutPanel1.Controls.Add(Me.btnPlay, 0, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.btnPause, 1, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.btnStop, 2, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.btnAddLink, 3, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.btnUpdate, 4, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.PanelButtonsRight, 5, 0)
        Me.TableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top
        Me.TableLayoutPanel1.Location = New System.Drawing.Point(0, 0)
        Me.TableLayoutPanel1.Name = "TableLayoutPanel1"
        Me.TableLayoutPanel1.Padding = New System.Windows.Forms.Padding(9, 0, 9, 0)
        Me.TableLayoutPanel1.RowCount = 1
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.TableLayoutPanel1.Size = New System.Drawing.Size(604, 40)
        Me.TableLayoutPanel1.TabIndex = 4
        '
        'btnPlay
        '
        Me.btnPlay.Location = New System.Drawing.Point(12, 3)
        Me.btnPlay.Name = "btnPlay"
        Me.btnPlay.Size = New System.Drawing.Size(34, 34)
        Me.btnPlay.TabIndex = 1
        Me.ToolTipBotones.SetToolTip(Me.btnPlay, "Iniciar descargas")
        Me.btnPlay.UseVisualStyleBackColor = True
        '
        'btnPause
        '
        Me.btnPause.Location = New System.Drawing.Point(52, 3)
        Me.btnPause.Name = "btnPause"
        Me.btnPause.Size = New System.Drawing.Size(34, 34)
        Me.btnPause.TabIndex = 2
        Me.ToolTipBotones.SetToolTip(Me.btnPause, "Pausar descargas")
        Me.btnPause.UseVisualStyleBackColor = True
        '
        'btnStop
        '
        Me.btnStop.Location = New System.Drawing.Point(92, 3)
        Me.btnStop.Name = "btnStop"
        Me.btnStop.Size = New System.Drawing.Size(34, 34)
        Me.btnStop.TabIndex = 3
        Me.ToolTipBotones.SetToolTip(Me.btnStop, "Detener descargas")
        Me.btnStop.UseVisualStyleBackColor = True
        '
        'btnAddLink
        '
        Me.btnAddLink.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnAddLink.Location = New System.Drawing.Point(147, 3)
        Me.btnAddLink.Name = "btnAddLink"
        Me.btnAddLink.Size = New System.Drawing.Size(34, 34)
        Me.btnAddLink.TabIndex = 4
        Me.ToolTipBotones.SetToolTip(Me.btnAddLink, "Agregar links")
        Me.btnAddLink.UseVisualStyleBackColor = True
        '
        'btnUpdate
        '
        Me.btnUpdate.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnUpdate.Location = New System.Drawing.Point(187, 3)
        Me.btnUpdate.Name = "btnUpdate"
        Me.btnUpdate.Size = New System.Drawing.Size(34, 34)
        Me.btnUpdate.TabIndex = 6
        Me.ToolTipBotones.SetToolTip(Me.btnUpdate, "Existe una versión nueva de Megadownloader, haga click aquí para descargarla")
        Me.btnUpdate.UseVisualStyleBackColor = True
        '
        'StatusStrip1
        '
        Me.StatusStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.StatusToolStripStatusLabel, Me.RAMProcToolStripStatusLabel})
        Me.StatusStrip1.Location = New System.Drawing.Point(0, 360)
        Me.StatusStrip1.Name = "StatusStrip1"
        Me.StatusStrip1.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.StatusStrip1.Size = New System.Drawing.Size(604, 22)
        Me.StatusStrip1.TabIndex = 5
        Me.StatusStrip1.Text = "StatusStrip1"
        '
        'StatusToolStripStatusLabel
        '
        Me.StatusToolStripStatusLabel.Name = "StatusToolStripStatusLabel"
        Me.StatusToolStripStatusLabel.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.StatusToolStripStatusLabel.Size = New System.Drawing.Size(499, 17)
        Me.StatusToolStripStatusLabel.Spring = True
        Me.StatusToolStripStatusLabel.Text = "Estado: -"
        Me.StatusToolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'RAMProcToolStripStatusLabel
        '
        Me.RAMProcToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text
        Me.RAMProcToolStripStatusLabel.Name = "RAMProcToolStripStatusLabel"
        Me.RAMProcToolStripStatusLabel.RightToLeft = System.Windows.Forms.RightToLeft.No
        Me.RAMProcToolStripStatusLabel.Size = New System.Drawing.Size(90, 17)
        Me.RAMProcToolStripStatusLabel.Text = "RAM: - / Proc: -"
        '
        'Main
        '
        Me.AllowDrop = True
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(604, 382)
        Me.Controls.Add(Me.StatusStrip1)
        Me.Controls.Add(Me.TableLayoutPanel1)
        Me.Controls.Add(Me.ListaDescargas)
        Me.Icon = Global.MegaDownloader.My.Resources.Resources.icono
        Me.MinimumSize = New System.Drawing.Size(410, 250)
        Me.Name = "Main"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.Manual
        Me.Text = "MegaDownloader"
        CType(Me.ListaDescargas, System.ComponentModel.ISupportInitialize).EndInit()
        Me.PanelButtonsRight.ResumeLayout(False)
        Me.MenuDescarga.ResumeLayout(False)
        Me.MenuPanel.ResumeLayout(False)
        Me.MenuMinimizado.ResumeLayout(False)
        Me.TableLayoutPanel1.ResumeLayout(False)
        Me.StatusStrip1.ResumeLayout(False)
        Me.StatusStrip1.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents ListaDescargas As BrightIdeasSoftware.TreeListView
    Friend WithEvents OlvColumnNombre As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnTamano As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnProgreso As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnVelocidad As BrightIdeasSoftware.OLVColumn
    Friend WithEvents MenuDescarga As System.Windows.Forms.ContextMenuStrip
    Friend WithEvents AbrirEnCarpetaToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolStripSeparator1 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents PropiedadesToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents MenuPanel As System.Windows.Forms.ContextMenuStrip
    Friend WithEvents AgregarLinksToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents OlvColumnEstado As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnPrioridad As BrightIdeasSoftware.OLVColumn
    Friend WithEvents IconoMinimizado As System.Windows.Forms.NotifyIcon
    Friend WithEvents MenuMinimizado As System.Windows.Forms.ContextMenuStrip
    Friend WithEvents AbrirToolStripMenuItem1 As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents CerrarToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents btnConfig As System.Windows.Forms.Button
    Friend WithEvents btnCollaborate As System.Windows.Forms.Button
    Friend WithEvents ToolStripSeparator2 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents SubirPrioridadMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents BajarPrioridadMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents TableLayoutPanel1 As System.Windows.Forms.TableLayoutPanel
    Friend WithEvents btnPlay As System.Windows.Forms.Button
    Friend WithEvents btnPause As System.Windows.Forms.Button
    Friend WithEvents btnStop As System.Windows.Forms.Button
    Friend WithEvents ToolStripSeparator3 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents EliminarMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents EliminarYBorrarMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents StatusStrip1 As System.Windows.Forms.StatusStrip
    Friend WithEvents RAMProcToolStripStatusLabel As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents StatusToolStripStatusLabel As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents ResetToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents OlvColumnEDT As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnProgresoPorc As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnDescargado As BrightIdeasSoftware.OLVColumn
    Friend WithEvents OlvColumnRestante As BrightIdeasSoftware.OLVColumn
    Friend WithEvents VerErrorToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents VerLinksToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents VerLinksDescToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents OcultarEnlacesImagenMenuItem As System.Windows.Forms.ToolStripMenuItem
    'Friend WithEvents SkinEngine As Sunisoft.IrisSkin.SkinEngine
    Friend WithEvents LimpiarCompletadosToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents LimpiarCompletados2ToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolStripSeparator4 As System.Windows.Forms.ToolStripSeparator
    Friend WithEvents btnAddLink As System.Windows.Forms.Button
    Friend WithEvents PausarStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolTipBotones As System.Windows.Forms.ToolTip
    Friend WithEvents btnUpdate As System.Windows.Forms.Button
    Friend WithEvents AgregarLinkStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents PanelButtonsRight As System.Windows.Forms.Panel

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
    End Sub
    Friend WithEvents VerProgresoDescompresionToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ForceDownloadStripMenuItem As System.Windows.Forms.ToolStripMenuItem
End Class
