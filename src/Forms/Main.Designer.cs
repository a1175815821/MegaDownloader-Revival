using System;
using System.Diagnostics;

namespace MegaDownloader
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class Main : System.Windows.Forms.Form
    {

        // Form overrides dispose to clean up the component list.
        [DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components is not null)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            ListaDescargas = new BrightIdeasSoftware.TreeListView();
            ListaDescargas.FormatRow += new EventHandler<BrightIdeasSoftware.FormatRowEventArgs>(ListaDescargas_FormatRow);
            ListaDescargas.FormatCell += new EventHandler<BrightIdeasSoftware.FormatCellEventArgs>(ListaDescargas_FormatCell);
            ListaDescargas.ColumnWidthChanging += new System.Windows.Forms.ColumnWidthChangingEventHandler(ListaDescargas_ColumnWidthChanging);
            ListaDescargas.SelectionChanged += new EventHandler(ListaDescargas_SelectionChanged);
            ListaDescargas.DragDrop += new System.Windows.Forms.DragEventHandler(ListaDescargas_DragDrop);
            ListaDescargas.CanDrop += new EventHandler<BrightIdeasSoftware.OlvDropEventArgs>(ListaDescargas_CanDrop);
            ListaDescargas.CellRightClick += new EventHandler<BrightIdeasSoftware.CellRightClickEventArgs>(ListaDescargas_CellRightClick);
            OlvColumnPrioridad = new BrightIdeasSoftware.OLVColumn();
            OlvColumnNombre = new BrightIdeasSoftware.OLVColumn();
            OlvColumnDescargado = new BrightIdeasSoftware.OLVColumn();
            OlvColumnTamano = new BrightIdeasSoftware.OLVColumn();
            OlvColumnEstado = new BrightIdeasSoftware.OLVColumn();
            OlvColumnProgresoPorc = new BrightIdeasSoftware.OLVColumn();
            OlvColumnProgreso = new BrightIdeasSoftware.OLVColumn();
            OlvColumnVelocidad = new BrightIdeasSoftware.OLVColumn();
            OlvColumnEDT = new BrightIdeasSoftware.OLVColumn();
            OlvColumnRestante = new BrightIdeasSoftware.OLVColumn();
            PanelButtonsRight = new System.Windows.Forms.Panel();
            btnCollaborate = new CommandButton();
            btnCollaborate.Click += new EventHandler(Collaborate_Click);
            btnConfig = new CommandButton();
            btnConfig.Click += new EventHandler(btnConfig_Click);
            MenuDescarga = new System.Windows.Forms.ContextMenuStrip(components);
            AbrirEnCarpetaToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            AbrirEnCarpetaToolStripMenuItem.Click += new EventHandler(AbrirEnCarpetaToolStripMenuItem_Click);
            ToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            SubirPrioridadMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            SubirPrioridadMenuItem.Click += new EventHandler(SubirPrioridad_Click);
            BajarPrioridadMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            BajarPrioridadMenuItem.Click += new EventHandler(BajarPrioridadMenuItem_Click);
            ToolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            ForceDownloadStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ForceDownloadStripMenuItem.Click += new EventHandler(ForceDownloadStripMenuItem_Click);
            PausarStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            PausarStripMenuItem.Click += new EventHandler(PausarStripMenuItem_Click);
            EliminarMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            EliminarMenuItem.Click += new EventHandler(EliminarMenuItem_Click);
            EliminarYBorrarMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            EliminarYBorrarMenuItem.Click += new EventHandler(EliminarYBorrarMenuItem_Click);
            ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            VerErrorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            VerErrorToolStripMenuItem.Click += new EventHandler(VerErrorToolStripMenuItem_Click);
            VerLinksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            VerLinksToolStripMenuItem.Click += new EventHandler(VerLinksToolStripMenuItem_Click);
            VerLinksDescToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            VerLinksDescToolStripMenuItem.Click += new EventHandler(VerLinksDescToolStripMenuItem_Click);
            OcultarEnlacesImagenMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            OcultarEnlacesImagenMenuItem.Click += new EventHandler(OcultarEnlacesImagenMenuItem_Click);
            VerProgresoDescompresionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            VerProgresoDescompresionToolStripMenuItem.Click += new EventHandler(VerProgresoDescompresionToolStripMenuItem_Click);
            ResetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ResetToolStripMenuItem.Click += new EventHandler(ResetToolStripMenuItem_Click);
            LimpiarCompletados2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            LimpiarCompletados2ToolStripMenuItem.Click += new EventHandler(LimpiarCompletados2ToolStripMenuItem_Click);
            PropiedadesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            PropiedadesToolStripMenuItem.Click += new EventHandler(PropiedadesToolStripMenuItem_Click);
            navPanel = new System.Windows.Forms.Panel();
            navListBox = new System.Windows.Forms.ListBox();
            navListBox.SelectedIndexChanged += new EventHandler(navListBox_SelectedIndexChanged);
            detailPanel = new System.Windows.Forms.Panel();
            detailGroup = new SectionGroupBox();
            detailLabel = new System.Windows.Forms.Label();
            MenuPanel = new System.Windows.Forms.ContextMenuStrip(components);
            AgregarLinksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            AgregarLinksToolStripMenuItem.Click += new EventHandler(AgregarLinksToolStripMenuItem_Click);
            LimpiarCompletadosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            LimpiarCompletadosToolStripMenuItem.Click += new EventHandler(LimpiarCompletadosToolStripMenuItem_Click);
            IconoMinimizado = new System.Windows.Forms.NotifyIcon(components);
            IconoMinimizado.DoubleClick += new EventHandler(IconoMinimizado_DoubleClick);
            IconoMinimizado.Click += new EventHandler(IconoMinimizado_Click);
            IconoMinimizado.BalloonTipClicked += new EventHandler(IconoMinimizado_BalloonTipClicked);
            MenuMinimizado = new System.Windows.Forms.ContextMenuStrip(components);
            AbrirToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            AbrirToolStripMenuItem1.Click += new EventHandler(AbrirToolStripMenuItem1_Click);
            AgregarLinkStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            AgregarLinkStripMenuItem.Click += new EventHandler(AgregarLinkStripMenuItem_Click);
            CerrarToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            CerrarToolStripMenuItem.Click += new EventHandler(CerrarToolStripMenuItem_Click);
            TableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            btnPlay = new CommandButton();
            btnPlay.Click += new EventHandler(btnPlay_Click);
            btnPause = new CommandButton();
            btnPause.Click += new EventHandler(btnPause_Click);
            btnStop = new CommandButton();
            btnStop.Click += new EventHandler(btnStop_Click);
            btnAddLink = new CommandButton();
            btnAddLink.Click += new EventHandler(btnAddLink_Click);
            btnUpdate = new CommandButton();
            btnUpdate.Click += new EventHandler(btnUpdate_Click);
            StatusStrip1 = new System.Windows.Forms.StatusStrip();
            StatusToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            RAMProcToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            ToolTipBotones = new System.Windows.Forms.ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)ListaDescargas).BeginInit();
            PanelButtonsRight.SuspendLayout();
            MenuDescarga.SuspendLayout();
            MenuPanel.SuspendLayout();
            MenuMinimizado.SuspendLayout();
            TableLayoutPanel1.SuspendLayout();
            StatusStrip1.SuspendLayout();
            navPanel.SuspendLayout();
            detailPanel.SuspendLayout();
            detailGroup.SuspendLayout();
            SuspendLayout();
            // 
            // ListaDescargas
            // 
            ListaDescargas.AllColumns.Add(OlvColumnNombre);
            ListaDescargas.AllColumns.Add(OlvColumnPrioridad);
            ListaDescargas.AllColumns.Add(OlvColumnDescargado);
            ListaDescargas.AllColumns.Add(OlvColumnTamano);
            ListaDescargas.AllColumns.Add(OlvColumnEstado);
            ListaDescargas.AllColumns.Add(OlvColumnProgresoPorc);
            ListaDescargas.AllColumns.Add(OlvColumnProgreso);
            ListaDescargas.AllColumns.Add(OlvColumnVelocidad);
            ListaDescargas.AllColumns.Add(OlvColumnEDT);
            ListaDescargas.AllColumns.Add(OlvColumnRestante);
            ListaDescargas.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            ListaDescargas.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { OlvColumnNombre, OlvColumnPrioridad, OlvColumnDescargado, OlvColumnTamano, OlvColumnEstado, OlvColumnProgreso, OlvColumnVelocidad, OlvColumnEDT });
            ListaDescargas.Location = new System.Drawing.Point(148, 46);
            ListaDescargas.Name = "ListaDescargas";
            ListaDescargas.OwnerDraw = true;
            ListaDescargas.ShowGroups = false;
            ListaDescargas.Size = new System.Drawing.Size(678, 429);
            ListaDescargas.TabIndex = 0;
            ListaDescargas.UseCompatibleStateImageBehavior = false;
            ListaDescargas.View = System.Windows.Forms.View.Details;
            ListaDescargas.VirtualMode = true;
            // 
            // navPanel
            // 
            navPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            navPanel.Controls.Add(navListBox);
            navPanel.Location = new System.Drawing.Point(12, 46);
            navPanel.Name = "navPanel";
            navPanel.Size = new System.Drawing.Size(168, 429);
            navPanel.TabIndex = 8;
            // 
            // navListBox
            // 
            // 不再 Dock=Fill:下方留给任务总览与快捷入口(见 InitNavRail/LayoutNavRail),
            // 高度由 LayoutNavRail 按 ItemHeight×5 算死,Anchor 只保留 Top|Left|Right。
            navListBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            navListBox.FormattingEnabled = true;
            navListBox.HorizontalScrollbar = false;
            navListBox.IntegralHeight = false;
            navListBox.ItemHeight = 20;
            navListBox.Location = new System.Drawing.Point(0, 0);
            navListBox.Name = "navListBox";
            navListBox.SelectionMode = System.Windows.Forms.SelectionMode.One;
            navListBox.Size = new System.Drawing.Size(168, 130);
            navListBox.TabIndex = 0;
            // 
            // detailPanel
            // 
            detailPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            detailPanel.Controls.Add(detailGroup);
            detailPanel.Location = new System.Drawing.Point(832, 46);
            detailPanel.Name = "detailPanel";
            detailPanel.Size = new System.Drawing.Size(180, 429);
            detailPanel.TabIndex = 9;
            // 
            // detailGroup
            // 
            detailGroup.Controls.Add(detailLabel);
            detailGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            detailGroup.Location = new System.Drawing.Point(0, 0);
            detailGroup.Name = "detailGroup";
            detailGroup.Size = new System.Drawing.Size(180, 429);
            detailGroup.TabIndex = 0;
            detailGroup.TabStop = false;
            detailGroup.Text = "Detalle";
            // 
            // detailLabel
            // 
            // 空态文案载体:无选中时显示引导文字(Detail_Empty + Detail_EmptyHint);
            // 有选中时隐藏,交给运行时构建的结构化详情面板(detailContentPanel)。
            detailLabel.AutoSize = false;
            detailLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            detailLabel.Location = new System.Drawing.Point(3, 16);
            detailLabel.Name = "detailLabel";
            detailLabel.Padding = new System.Windows.Forms.Padding(8);
            detailLabel.Size = new System.Drawing.Size(174, 410);
            detailLabel.TabIndex = 0;
            detailLabel.UseMnemonic = false;
            // 
            // OlvColumnPrioridad
            // 
            OlvColumnPrioridad.Hideable = true;
            OlvColumnPrioridad.Text = "#";
            OlvColumnPrioridad.Width = 20;
            // 
            // OlvColumnNombre
            // 
            OlvColumnNombre.FillsFreeSpace = true;
            OlvColumnNombre.Hideable = false;
            OlvColumnNombre.Text = "Nombre";
            OlvColumnNombre.Width = 185;
            // 
            // OlvColumnDescargado
            // 
            OlvColumnDescargado.Text = "Descargado";
            OlvColumnDescargado.Width = 70;
            // 
            // OlvColumnTamano
            // 
            OlvColumnTamano.Text = "Tamaño";
            OlvColumnTamano.Width = 70;
            // 
            // OlvColumnEstado
            // 
            OlvColumnEstado.Text = "Estado";
            OlvColumnEstado.Width = 90;
            // 
            // OlvColumnProgresoPorc
            // 
            OlvColumnProgresoPorc.Text = "Progreso %";
            OlvColumnProgresoPorc.Width = 55;
            // 
            // OlvColumnProgreso
            // 
            OlvColumnProgreso.Text = "Progreso";
            OlvColumnProgreso.Width = 80;
            // 
            // OlvColumnVelocidad
            // 
            OlvColumnVelocidad.Text = "Velocidad";
            OlvColumnVelocidad.Width = 77;
            // 
            // OlvColumnEDT
            // 
            OlvColumnEDT.Text = "Estimado";
            OlvColumnEDT.Width = 70;
            // 
            // OlvColumnRestante
            // 
            OlvColumnRestante.IsVisible = false;
            OlvColumnRestante.Text = "Restante";
            // 
            // PanelButtonsRight
            // 
            PanelButtonsRight.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            PanelButtonsRight.Controls.Add(btnCollaborate);
            PanelButtonsRight.Controls.Add(btnConfig);
            PanelButtonsRight.Location = new System.Drawing.Point(512, 3);
            PanelButtonsRight.Name = "PanelButtonsRight";
            PanelButtonsRight.Size = new System.Drawing.Size(80, 34);
            PanelButtonsRight.TabIndex = 0;
            // 
            // btnCollaborate
            // 
            btnCollaborate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnCollaborate.Location = new System.Drawing.Point(6, 0);
            btnCollaborate.Name = "btnCollaborate";
            btnCollaborate.Size = new System.Drawing.Size(35, 34);
            btnCollaborate.TabIndex = 6;
            ToolTipBotones.SetToolTip(btnCollaborate, "Colaborar");
            btnCollaborate.UseVisualStyleBackColor = true;
            // 
            // btnConfig
            // 
            btnConfig.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnConfig.Location = new System.Drawing.Point(46, 0);
            btnConfig.Name = "btnConfig";
            btnConfig.Size = new System.Drawing.Size(35, 34);
            btnConfig.TabIndex = 7;
            ToolTipBotones.SetToolTip(btnConfig, "Configuración");
            btnConfig.UseVisualStyleBackColor = true;
            // 
            // MenuDescarga
            // 
            MenuDescarga.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { AbrirEnCarpetaToolStripMenuItem, PropiedadesToolStripMenuItem, ToolStripSeparator2, ForceDownloadStripMenuItem, PausarStripMenuItem, SubirPrioridadMenuItem, BajarPrioridadMenuItem, ToolStripSeparator3, EliminarMenuItem, EliminarYBorrarMenuItem, ResetToolStripMenuItem, LimpiarCompletados2ToolStripMenuItem, ToolStripSeparator1, VerErrorToolStripMenuItem, VerLinksToolStripMenuItem, VerLinksDescToolStripMenuItem, OcultarEnlacesImagenMenuItem, VerProgresoDescompresionToolStripMenuItem });
            MenuDescarga.Name = "MenuDescarga";
            MenuDescarga.Size = new System.Drawing.Size(226, 336);
            // 
            // AbrirEnCarpetaToolStripMenuItem
            // 
            AbrirEnCarpetaToolStripMenuItem.Name = "AbrirEnCarpetaToolStripMenuItem";
            AbrirEnCarpetaToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            AbrirEnCarpetaToolStripMenuItem.Text = "Abrir directorio";
            // 
            // ToolStripSeparator2
            // 
            ToolStripSeparator2.Name = "ToolStripSeparator2";
            ToolStripSeparator2.Size = new System.Drawing.Size(222, 6);
            // 
            // SubirPrioridadMenuItem
            // 
            SubirPrioridadMenuItem.Name = "SubirPrioridadMenuItem";
            SubirPrioridadMenuItem.Size = new System.Drawing.Size(225, 22);
            SubirPrioridadMenuItem.Text = "Subir prioridad";
            // 
            // BajarPrioridadMenuItem
            // 
            BajarPrioridadMenuItem.Name = "BajarPrioridadMenuItem";
            BajarPrioridadMenuItem.Size = new System.Drawing.Size(225, 22);
            BajarPrioridadMenuItem.Text = "Bajar prioridad";
            // 
            // ToolStripSeparator3
            // 
            ToolStripSeparator3.Name = "ToolStripSeparator3";
            ToolStripSeparator3.Size = new System.Drawing.Size(222, 6);
            // 
            // ForceDownloadStripMenuItem
            // 
            ForceDownloadStripMenuItem.Name = "ForceDownloadStripMenuItem";
            ForceDownloadStripMenuItem.Size = new System.Drawing.Size(225, 22);
            ForceDownloadStripMenuItem.Text = "Forzar descarga";
            // 
            // PausarStripMenuItem
            // 
            PausarStripMenuItem.Name = "PausarStripMenuItem";
            PausarStripMenuItem.Size = new System.Drawing.Size(225, 22);
            PausarStripMenuItem.Text = "Poner en pausa";
            // 
            // EliminarMenuItem
            // 
            EliminarMenuItem.Name = "EliminarMenuItem";
            EliminarMenuItem.Size = new System.Drawing.Size(225, 22);
            EliminarMenuItem.Text = "Eliminar";
            // 
            // EliminarYBorrarMenuItem
            // 
            EliminarYBorrarMenuItem.Name = "EliminarYBorrarMenuItem";
            EliminarYBorrarMenuItem.Size = new System.Drawing.Size(225, 22);
            EliminarYBorrarMenuItem.Text = "Eliminar y borrar";
            // 
            // ToolStripSeparator1
            // 
            ToolStripSeparator1.Name = "ToolStripSeparator1";
            ToolStripSeparator1.Size = new System.Drawing.Size(222, 6);
            // 
            // VerErrorToolStripMenuItem
            // 
            VerErrorToolStripMenuItem.Name = "VerErrorToolStripMenuItem";
            VerErrorToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            VerErrorToolStripMenuItem.Text = "Ver error";
            // 
            // VerLinksToolStripMenuItem
            // 
            VerLinksToolStripMenuItem.Name = "VerLinksToolStripMenuItem";
            VerLinksToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            VerLinksToolStripMenuItem.Text = "Ver links";
            // 
            // VerLinksDescToolStripMenuItem
            // 
            VerLinksDescToolStripMenuItem.Name = "VerLinksDescToolStripMenuItem";
            VerLinksDescToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            VerLinksDescToolStripMenuItem.Text = "Ver links + desc";
            // 
            // OcultarEnlacesImagenMenuItem
            // 
            OcultarEnlacesImagenMenuItem.Name = "OcultarEnlacesImagenMenuItem";
            OcultarEnlacesImagenMenuItem.Size = new System.Drawing.Size(225, 22);
            OcultarEnlacesImagenMenuItem.Text = "Ocultar en imagen";
            // 
            // VerProgresoDescompresionToolStripMenuItem
            // 
            VerProgresoDescompresionToolStripMenuItem.Name = "VerProgresoDescompresionToolStripMenuItem";
            VerProgresoDescompresionToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            VerProgresoDescompresionToolStripMenuItem.Text = "Ver progreso descompresión";
            // 
            // ResetToolStripMenuItem
            // 
            ResetToolStripMenuItem.Name = "ResetToolStripMenuItem";
            ResetToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            ResetToolStripMenuItem.Text = "Reset";
            // 
            // LimpiarCompletados2ToolStripMenuItem
            // 
            LimpiarCompletados2ToolStripMenuItem.Name = "LimpiarCompletados2ToolStripMenuItem";
            LimpiarCompletados2ToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            LimpiarCompletados2ToolStripMenuItem.Text = "Limpiar completados";
            // 
            // PropiedadesToolStripMenuItem
            // 
            PropiedadesToolStripMenuItem.Enabled = false;
            PropiedadesToolStripMenuItem.Name = "PropiedadesToolStripMenuItem";
            PropiedadesToolStripMenuItem.Size = new System.Drawing.Size(225, 22);
            PropiedadesToolStripMenuItem.Text = "Propiedades";
            // 
            // MenuPanel
            // 
            MenuPanel.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { AgregarLinksToolStripMenuItem, LimpiarCompletadosToolStripMenuItem });
            MenuPanel.Name = "MenuPanel";
            MenuPanel.Size = new System.Drawing.Size(187, 48);
            // 
            // AgregarLinksToolStripMenuItem
            // 
            AgregarLinksToolStripMenuItem.Name = "AgregarLinksToolStripMenuItem";
            AgregarLinksToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            AgregarLinksToolStripMenuItem.Text = "Agregar links";
            // 
            // LimpiarCompletadosToolStripMenuItem
            // 
            LimpiarCompletadosToolStripMenuItem.Name = "LimpiarCompletadosToolStripMenuItem";
            LimpiarCompletadosToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            LimpiarCompletadosToolStripMenuItem.Text = "Limpiar completados";
            // 
            // IconoMinimizado
            // 
            IconoMinimizado.ContextMenuStrip = MenuMinimizado;
            IconoMinimizado.Icon = My.Resources.Resources.icono;
            IconoMinimizado.Text = "NotifyIcon1";
            IconoMinimizado.Visible = true;
            // 
            // MenuMinimizado
            // 
            MenuMinimizado.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { AbrirToolStripMenuItem1, AgregarLinkStripMenuItem, CerrarToolStripMenuItem });
            MenuMinimizado.Name = "MenuMinimizado";
            MenuMinimizado.Size = new System.Drawing.Size(139, 70);
            // 
            // AbrirToolStripMenuItem1
            // 
            AbrirToolStripMenuItem1.Name = "AbrirToolStripMenuItem1";
            AbrirToolStripMenuItem1.Size = new System.Drawing.Size(138, 22);
            AbrirToolStripMenuItem1.Text = "Abrir";
            // 
            // AgregarLinkStripMenuItem
            // 
            AgregarLinkStripMenuItem.Name = "AgregarLinkStripMenuItem";
            AgregarLinkStripMenuItem.Size = new System.Drawing.Size(138, 22);
            AgregarLinkStripMenuItem.Text = "Agregar link";
            // 
            // CerrarToolStripMenuItem
            // 
            CerrarToolStripMenuItem.Name = "CerrarToolStripMenuItem";
            CerrarToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            CerrarToolStripMenuItem.Text = "Cerrar";
            // 
            // TableLayoutPanel1
            // 
            TableLayoutPanel1.ColumnCount = 6;
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 88.0f));
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 88.0f));
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 88.0f));
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 88.0f));
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120.0f));
            TableLayoutPanel1.Controls.Add(btnPlay, 0, 0);
            TableLayoutPanel1.Controls.Add(btnPause, 1, 0);
            TableLayoutPanel1.Controls.Add(btnStop, 2, 0);
            TableLayoutPanel1.Controls.Add(btnAddLink, 3, 0);
            TableLayoutPanel1.Controls.Add(btnUpdate, 4, 0);
            TableLayoutPanel1.Controls.Add(PanelButtonsRight, 5, 0);
            TableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            TableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            TableLayoutPanel1.Name = "TableLayoutPanel1";
            TableLayoutPanel1.Padding = new System.Windows.Forms.Padding(9, 0, 9, 0);
            TableLayoutPanel1.RowCount = 1;
            TableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0f));
            TableLayoutPanel1.Size = new System.Drawing.Size(604, 40);
            TableLayoutPanel1.TabIndex = 4;
            // 
            // btnPlay
            // 
            btnPlay.Location = new System.Drawing.Point(12, 3);
            btnPlay.Name = "btnPlay";
            btnPlay.Size = new System.Drawing.Size(88, 34);
            btnPlay.TabIndex = 1;
            btnPlay.Text = "Iniciar";
            btnPlay.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            ToolTipBotones.SetToolTip(btnPlay, "Iniciar descargas");
            btnPlay.UseVisualStyleBackColor = true;
            // 
            // btnPause
            // 
            btnPause.Location = new System.Drawing.Point(52, 3);
            btnPause.Name = "btnPause";
            btnPause.Size = new System.Drawing.Size(88, 34);
            btnPause.TabIndex = 2;
            btnPause.Text = "Pausar";
            btnPause.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            ToolTipBotones.SetToolTip(btnPause, "Pausar descargas");
            btnPause.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            btnStop.Location = new System.Drawing.Point(92, 3);
            btnStop.Name = "btnStop";
            btnStop.Size = new System.Drawing.Size(88, 34);
            btnStop.TabIndex = 3;
            btnStop.Text = "Detener";
            btnStop.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            ToolTipBotones.SetToolTip(btnStop, "Detener descargas");
            btnStop.UseVisualStyleBackColor = true;
            // 
            // btnAddLink
            // 
            btnAddLink.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAddLink.Location = new System.Drawing.Point(147, 3);
            btnAddLink.Name = "btnAddLink";
            btnAddLink.Size = new System.Drawing.Size(88, 34);
            btnAddLink.TabIndex = 4;
            btnAddLink.Text = "Añadir";
            btnAddLink.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            ToolTipBotones.SetToolTip(btnAddLink, "Agregar links");
            btnAddLink.UseVisualStyleBackColor = true;
            // 
            // btnUpdate
            // 
            btnUpdate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnUpdate.Location = new System.Drawing.Point(187, 3);
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Size = new System.Drawing.Size(104, 34);
            btnUpdate.TabIndex = 6;
            btnUpdate.Text = "Actualizar";
            btnUpdate.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            ToolTipBotones.SetToolTip(btnUpdate, "Existe una versión nueva de Megadownloader, haga click aquí para descargarla");
            btnUpdate.UseVisualStyleBackColor = true;
            // 
            // StatusStrip1
            // 
            StatusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { StatusToolStripStatusLabel, RAMProcToolStripStatusLabel });
            StatusStrip1.Location = new System.Drawing.Point(0, 360);
            StatusStrip1.Name = "StatusStrip1";
            StatusStrip1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            StatusStrip1.Size = new System.Drawing.Size(604, 22);
            StatusStrip1.TabIndex = 5;
            StatusStrip1.Text = "StatusStrip1";
            // 
            // StatusToolStripStatusLabel
            // 
            StatusToolStripStatusLabel.Name = "StatusToolStripStatusLabel";
            StatusToolStripStatusLabel.RightToLeft = System.Windows.Forms.RightToLeft.No;
            StatusToolStripStatusLabel.Size = new System.Drawing.Size(499, 17);
            StatusToolStripStatusLabel.Spring = true;
            StatusToolStripStatusLabel.Text = "Estado: -";
            StatusToolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // RAMProcToolStripStatusLabel
            // 
            RAMProcToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            RAMProcToolStripStatusLabel.Name = "RAMProcToolStripStatusLabel";
            RAMProcToolStripStatusLabel.RightToLeft = System.Windows.Forms.RightToLeft.No;
            RAMProcToolStripStatusLabel.Size = new System.Drawing.Size(150, 17);
            RAMProcToolStripStatusLabel.Text = "RAM: - / Proc: -";
            // 
            // Main
            // 
            AllowDrop = true;
            AutoScaleDimensions = new System.Drawing.SizeF(7.0f, 15.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1360, 800);
            Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Controls.Add(StatusStrip1);
            Controls.Add(TableLayoutPanel1);
            Controls.Add(detailPanel);
            Controls.Add(navPanel);
            Controls.Add(ListaDescargas);
            Icon = My.Resources.Resources.icono;
            MinimumSize = new System.Drawing.Size(640, 440);
            Name = "Main";
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Text = "MegaDownloader";
            ((System.ComponentModel.ISupportInitialize)ListaDescargas).EndInit();
            PanelButtonsRight.ResumeLayout(false);
            MenuDescarga.ResumeLayout(false);
            MenuPanel.ResumeLayout(false);
            MenuMinimizado.ResumeLayout(false);
            TableLayoutPanel1.ResumeLayout(false);
            StatusStrip1.ResumeLayout(false);
            StatusStrip1.PerformLayout();
            navPanel.ResumeLayout(false);
            detailPanel.ResumeLayout(false);
            detailPanel.PerformLayout();
            detailGroup.ResumeLayout(false);
            detailGroup.PerformLayout();
            Load += new EventHandler(Main_Load);
            Shown += new EventHandler(Main_Shown);
            Paint += new System.Windows.Forms.PaintEventHandler(Main_FirstPaint);
            FormClosed += new System.Windows.Forms.FormClosedEventHandler(Main_FormClosed);
            FormClosing += new System.Windows.Forms.FormClosingEventHandler(Main_FormClosing);
            Resize += new EventHandler(Main_Resize);
            DragDrop += new System.Windows.Forms.DragEventHandler(Main_DragDrop);
            DragEnter += new System.Windows.Forms.DragEventHandler(Main_DragEnter);
            Activated += new EventHandler(UpdateReminder_Tick);
            ResumeLayout(false);
            PerformLayout();

        }
        internal BrightIdeasSoftware.TreeListView ListaDescargas;
        internal System.Windows.Forms.Panel navPanel;
        internal System.Windows.Forms.ListBox navListBox;
        internal System.Windows.Forms.Panel detailPanel;
        internal System.Windows.Forms.GroupBox detailGroup;
        internal System.Windows.Forms.Label detailLabel;
        internal BrightIdeasSoftware.OLVColumn OlvColumnNombre;
        internal BrightIdeasSoftware.OLVColumn OlvColumnTamano;
        internal BrightIdeasSoftware.OLVColumn OlvColumnProgreso;
        internal BrightIdeasSoftware.OLVColumn OlvColumnVelocidad;
        internal System.Windows.Forms.ContextMenuStrip MenuDescarga;
        internal System.Windows.Forms.ToolStripMenuItem AbrirEnCarpetaToolStripMenuItem;
        internal System.Windows.Forms.ToolStripSeparator ToolStripSeparator1;
        internal System.Windows.Forms.ToolStripMenuItem PropiedadesToolStripMenuItem;
        internal System.Windows.Forms.ContextMenuStrip MenuPanel;
        internal System.Windows.Forms.ToolStripMenuItem AgregarLinksToolStripMenuItem;
        internal BrightIdeasSoftware.OLVColumn OlvColumnEstado;
        internal BrightIdeasSoftware.OLVColumn OlvColumnPrioridad;
        internal System.Windows.Forms.NotifyIcon IconoMinimizado;
        internal System.Windows.Forms.ContextMenuStrip MenuMinimizado;
        internal System.Windows.Forms.ToolStripMenuItem AbrirToolStripMenuItem1;
        internal System.Windows.Forms.ToolStripMenuItem CerrarToolStripMenuItem;
        internal System.Windows.Forms.Button btnConfig;
        internal System.Windows.Forms.Button btnCollaborate;
        internal System.Windows.Forms.ToolStripSeparator ToolStripSeparator2;
        internal System.Windows.Forms.ToolStripMenuItem SubirPrioridadMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem BajarPrioridadMenuItem;
        internal System.Windows.Forms.TableLayoutPanel TableLayoutPanel1;
        internal System.Windows.Forms.Button btnPlay;
        internal System.Windows.Forms.Button btnPause;
        internal System.Windows.Forms.Button btnStop;
        internal System.Windows.Forms.ToolStripSeparator ToolStripSeparator3;
        internal System.Windows.Forms.ToolStripMenuItem EliminarMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem EliminarYBorrarMenuItem;
        internal System.Windows.Forms.StatusStrip StatusStrip1;
        internal System.Windows.Forms.ToolStripStatusLabel RAMProcToolStripStatusLabel;
        internal System.Windows.Forms.ToolStripStatusLabel StatusToolStripStatusLabel;
        internal System.Windows.Forms.ToolStripMenuItem ResetToolStripMenuItem;
        internal BrightIdeasSoftware.OLVColumn OlvColumnEDT;
        internal BrightIdeasSoftware.OLVColumn OlvColumnProgresoPorc;
        internal BrightIdeasSoftware.OLVColumn OlvColumnDescargado;
        internal BrightIdeasSoftware.OLVColumn OlvColumnRestante;
        internal System.Windows.Forms.ToolStripMenuItem VerErrorToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem VerLinksToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem VerLinksDescToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem OcultarEnlacesImagenMenuItem;
        // Friend WithEvents SkinEngine As Sunisoft.IrisSkin.SkinEngine
        internal System.Windows.Forms.ToolStripMenuItem LimpiarCompletadosToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem LimpiarCompletados2ToolStripMenuItem;
        internal System.Windows.Forms.Button btnAddLink;
        internal System.Windows.Forms.ToolStripMenuItem PausarStripMenuItem;
        internal System.Windows.Forms.ToolTip ToolTipBotones;
        internal System.Windows.Forms.Button btnUpdate;
        internal System.Windows.Forms.ToolStripMenuItem AgregarLinkStripMenuItem;
        internal System.Windows.Forms.Panel PanelButtonsRight;

        internal System.Windows.Forms.ToolStripMenuItem VerProgresoDescompresionToolStripMenuItem;
        internal System.Windows.Forms.ToolStripMenuItem ForceDownloadStripMenuItem;
    }
}