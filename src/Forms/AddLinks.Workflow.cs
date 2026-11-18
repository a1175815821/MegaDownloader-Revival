using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MegaDownloader
{

    public partial class AddLinks
    {
        private BatchPreflightView preflightView;
        private bool onSelectionPage;
        private bool closeAfterImport;
        private int importCompleted;
        private int importTotal;
        private bool importCommitting;

        private void ShowSelectionPage()
        {
            if (importBusy || _watchResolving || preflightDisposed)
                return;
            if (string.IsNullOrWhiteSpace(txtLinks.Text))
            {
                ShowInputNotice(Language.GetText("Links not valid"), true);
                txtLinks.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtRuta.Text))
            {
                ShowInputNotice(Language.GetText("AddLinks_PathRequired"), true);
                txtRuta.Focus();
                return;
            }
            ShowInputNotice("");
            if (_linkCountTimer is not null)
                _linkCountTimer.Stop();
            preflightProgressTimer.Stop();
            bool created = preflightView is null;
            if (created)
            {
                var prepared = CurrentPreflightRequest();
                var mainForm = Main;
                preflightView = new BatchPreflightView(new List<string>(), Config, new Dictionary<string, URLProcessor.FileURL>(StringComparer.Ordinal), txtRuta.Text, preflightSession, prepared, () => mainForm.GetQueuedLinkSnapshot());
                preflightView.BackRequested += BackFromSelection;
                preflightView.CancelRequested += DismissSelection;
                preflightView.ConfirmRequested += SubmitSelection;
                addLinksPageHost.Controls.Add(preflightView);
            }
            else
            {
                preflightView.UpdateDestination(txtRuta.Text);
            }
            onSelectionPage = true;
            addLinksInputPage.Visible = false;
            preflightView.Visible = true;
            preflightView.BringToFront();
            addLinksStepLabel.Text = Language.GetText("AddLinks_StepSelection");
            AcceptButton = preflightView.ConfirmationButton;
            CancelButton = preflightView.DismissButton;
            // Form.CancelButton assigns DialogResult.Cancel by default; this button
            // cancels the build in place while an import is running.
            preflightView.DismissButton.DialogResult = DialogResult.None;
            if (created)
            {
                ThemeManager.ApplyTheme(this);
                preflightView.StartInspection();
            }
            preflightView.Focus();
        }

        private void BackFromSelection(object sender, EventArgs e)
        {
            if (!importBusy)
                ShowInputPage();
        }

        private void ShowInputPage(bool mergeDeferred = true)
        {
            onSelectionPage = false;
            if (preflightView is not null)
                preflightView.Visible = false;
            addLinksInputPage.Visible = true;
            addLinksInputPage.BringToFront();
            addLinksStepLabel.Text = Language.GetText("AddLinks_StepInput");
            AcceptButton = btnAgregar;
            CancelButton = addLinksCancel;
            UpdateAddLinksInputHeight();
            if (mergeDeferred && deferredInputs.Count > 0)
            {
                RestoreDeferredInputs(false);
                ShowInputNotice(Language.GetText("AddLinks_DeferredMerged"));
            }
            else if (mergeDeferred)
            {
                if (preflightView is not null && preflightView.InspectionStopped)
                {
                    preflightProgressTimer.Stop();
                    lblLinkCount.Text = Language.GetText("AddLinks_CheckStopped");
                    preflightProgressBar.Style = ProgressBarStyle.Continuous;
                    preflightProgressBar.Value = 0;
                }
                else if (preflightView is not null)
                {
                    // Returning is navigation, not a new request (including after
                    // the short metadata cache would otherwise have expired).
                    warmRequest = preflightView.ActiveRequest;
                    displayedProgress = null;
                    preflightProgressTimer.Tick -= RefreshPreflightProgress;
                    preflightProgressTimer.Tick += RefreshPreflightProgress;
                    preflightProgressTimer.Start();
                    RefreshPreflightProgress(null, EventArgs.Empty);
                }
                else
                {
                    UpdateLinkCount();
                }
            }
            txtLinks.Focus();
        }

        private void InvalidateSelectionPage()
        {
            if (preflightView is null)
                return;
            if (onSelectionPage)
                ShowInputPage(false);
            var obsolete = preflightView;
            preflightView = null;
            addLinksPageHost.Controls.Remove(obsolete);
            obsolete.Dispose();
        }

        private void ShowInputNotice(string text, bool isError = false)
        {
            if (addLinksFeedback is null)
                return;
            addLinksFeedback.Text = text;
            addLinksFeedback.Visible = !string.IsNullOrEmpty(text);
            UiStyles.SetRole(addLinksFeedback, isError ? UiStyles.Role.ErrorText : UiStyles.Role.BodyText);
        }

        private void DismissSelection(object sender, EventArgs e)
        {
            if (importBusy)
            {
                CancelImport();
            }
            else
            {
                Close();
            }
        }

        private void CancelImport()
        {
            if (!importBusy || importCommitting || importCancellation is null)
                return;
            importCancellation.Cancel();
            RefreshImportProgress();
        }

        private void AddLinks_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!importBusy || preflightDisposed)
                return;
            if (e.CloseReason != CloseReason.UserClosing)
            {
                // App/system shutdown must not wait for the UI continuation to run.
                if (importCancellation is not null)
                    importCancellation.Cancel();
                return;
            }
            // Never close between building a package and publishing it to the queue.
            e.Cancel = true;
            closeAfterImport = true;
            CancelImport();
        }

        private void RefreshImportProgress()
        {
            if (preflightView is null || preflightView.IsDisposed)
                return;
            preflightView.ShowImportProgress(Volatile.Read(ref importCompleted), importTotal, importCommitting, importCancellation is not null && importCancellation.IsCancellationRequested);
        }

        private async void SubmitSelection(object sender, EventArgs e)
        {
            if (importBusy || preflightDisposed || preflightView is null)
                return;
            var selected = preflightView.SelectedFiles;
            if (selected is null || selected.Count == 0)
                return;
            importBusy = true;
            importCancellation = new CancellationTokenSource();
            var source = importCancellation;
            var token = source.Token;
            importTotal = selected.Count;
            Volatile.Write(ref importCompleted, 0);
            importCommitting = false;
            bool committed = false;
            preflightView.ShowNotice("");
            preflightView.SetImporting(true);
            btnAgregar.Enabled = false;
            btnWatchOnline.Enabled = false;
            txtLinks.ReadOnly = true;
            OpcionesPaquete.Enabled = false;
            preflightProgressTimer.Tick -= RefreshPreflightProgress;
            preflightProgressTimer.Tick += RefreshPreflightProgress;
            preflightProgressTimer.Start();
            RefreshImportProgress();
            try
            {
                var latest = await Task.Run(() => Main.GetQueuedLinkSnapshot(), token);
                selected = await Task.Run(() =>
                    {
                        var known = new HashSet<string>(latest.Keys, StringComparer.Ordinal);
                        var result = new List<URLProcessor.FileURL>();
                        foreach (URLProcessor.FileURL @file in selected)
                        {
                            token.ThrowIfCancellationRequested();
                            string key = BatchLinkPreflight.Identity(@file);
                            if (key is null || known.Add(key))
                                result.Add(@file);
                        }
                        return result;
                    }, token);
                token.ThrowIfCancellationRequested();
                if (preflightDisposed)
                    return;
                if (selected.Count == 0)
                {
                    preflightView.ShowNotice(Language.GetText("Preflight_AllQueued"));
                    return;
                }
                importTotal = selected.Count;
                committed = await FinishAddPackageAsync(selected);
                if (committed)
                {
                    UltimaConfiguracionUsada.ExisteUltimaConfiguracion = true;
                    UltimaConfiguracionUsada.CrearDirectorioPaquete = chkCrearDirectorio.Checked;
                    UltimaConfiguracionUsada.ExtraerAutomaticamente = chkUnZip.Checked;
                    UltimaConfiguracionUsada.RutaDescarga = txtRuta.Text;
                    UltimaConfiguracionUsada.IniciarDescarga = chkStartDownload.Checked;
                    if (chkStartDownload.Checked)
                    {
                        try
                        {
                            Main.StartDownload();
                        }
                        catch (Exception ex)
                        {
                            // The tasks are already queued; a start failure must not
                            // put the same import back into the uncommitted state.
                            Log.WriteError("Starting the imported tasks failed: " + Log.SafeException(ex));
                            ToastForm.ShowToast(Main, Language.GetText("AddLinks_AddedPaused"));
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (!preflightDisposed)
                    preflightView.ShowNotice(Language.GetText("AddLinks_ImportCancelled"));
            }
            catch (Exception ex)
            {
                Log.WriteError("Error while adding links: " + Log.SafeException(ex));
                if (!preflightDisposed)
                    preflightView.ShowNotice(DownloadAdvice.FirstLine(Log.Redact(ex.Message)), true);
            }
            finally
            {
                importBusy = false;
                importCommitting = false;
                importCancellation = null;
                source.Dispose();
                if (!preflightDisposed)
                {
                    preflightProgressTimer.Stop();
                    txtLinks.ReadOnly = false;
                    OpcionesPaquete.Enabled = true;
                    ResetResolveButtons();
                    preflightView.SetImporting(false);
                    if (closeAfterImport || committed && deferredInputs.Count == 0)
                    {
                        Close();
                    }
                    else if (committed)
                    {
                        ShowInputPage(false);
                        RestoreDeferredInputs(true);
                        ShowInputNotice(Language.GetText("AddLinks_AddedMore").Replace("%N%", importTotal.ToString()));
                    }
                }
            }
        }
    }
}