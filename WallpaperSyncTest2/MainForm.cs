using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WallpaperSyncTest2;

namespace WallpaperSyncTest2
{
    public partial class MainForm : Form
    {
        private const string RepoImagesTxt = "https://raw.githubusercontent.com/ldk974/imagens/main/images.txt";
        private readonly List<ImageEntry> Images = new();
        private readonly HttpClient http = new HttpClient();
        private readonly string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallpaperSyncTest2");
        private readonly string cacheDir;
        private readonly string backupDir;
        private string lastAppliedPath = null; // path to backup original wallpaper

        public MainForm()
        {
            InitializeComponent();

            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36"
        );

            cacheDir = Path.Combine(appdata, "cache");
            backupDir = Path.Combine(appdata, "backup");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(backupDir);
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            chkShowPreviews.Checked = false;
            cacheDir = Path.Combine(appdata, "cache");
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            ToggleControls(false);
            lblStatus.Text = "Carregando catálogo...";

            await LoadImages();
            await RefreshViewAsync();

            ToggleControls(true);
            lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";
        }

        private void ToggleControls(bool enabled)
        {
            chkShowPreviews.Enabled = enabled;
            btnRefresh.Enabled = enabled;
            btnUndo.Enabled = enabled;
        }

        private async Task LoadImages()
        {
            Images.Clear();
            try
            {
                // base URL do teu servidor (sem barra final)
                string baseUrl = "https://icq-dancing-sponsored-inspection.trycloudflare.com/";

                // define as pastas
                var folders = new[] { "sfw", "nsfw" };

                foreach (var folder in folders)
                {
                    string url = $"{baseUrl}/{folder}/";
                    string html = await http.GetStringAsync(url);

                    // parseia simples do autoindex padrão do Nginx
                    var links = System.Text.RegularExpressions.Regex.Matches(html, "<a href=\"([^\"]+)\">")
                        .Cast<System.Text.RegularExpressions.Match>()
                        .Select(m => m.Groups[1].Value)
                        .Where(h => h.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                 || h.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                 || h.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                 || h.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var file in links)
                    {
                        var urlFull = $"{url}{file}";
                        string tag = folder.ToUpper();
                        string name = Path.GetFileNameWithoutExtension(file);

                        // usa [SFW]/[NSFW] se estiver no nome do arquivo
                        if (name.Contains("[SFW]", StringComparison.OrdinalIgnoreCase))
                            tag = "SFW";
                        else if (name.Contains("[NSFW]", StringComparison.OrdinalIgnoreCase))
                            tag = "NSFW";

                        Images.Add(new ImageEntry { Url = urlFull, Name = name, Tag = tag });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao carregar imagens: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async Task PopulateGridAsync()
        {
            flpGrid.SuspendLayout();
            flpGrid.Controls.Clear();

            // colecione tasks para garantir que todas as thumbnails sejam carregadas
            var tasks = new List<Task<Panel>>();

            foreach (var entry in Images)
            {
                // cria o painel vazio com label já adicionado
                var panel = CreateThumbnailCardPlaceholder(entry);
                flpGrid.Controls.Add(panel);

                // carrega a thumbnail e substitui a PictureBox.Image quando pronta
                tasks.Add(LoadThumbnailAndFillPanel(entry, panel));
            }

            // aguarda todas terminarem (não bloqueia a UI porque o caller faz await)
            await Task.WhenAll(tasks);

            flpGrid.ResumeLayout();
        }

        // cria placeholder do card (sem iniciar o download)
        private Panel CreateThumbnailCardPlaceholder(ImageEntry entry)
        {
            var panel = new Panel
            {
                Width = 160,
                Height = 140,
                Margin = new Padding(8),
                Tag = entry
            };

            var pic = new PictureBox
            {
                Width = 150,
                Height = 90, // thumbnail 16:9 visual
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(0x2F, 0x31, 0x36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lbl = new Label
            {
                Text = entry.Name,
                AutoEllipsis = true,
                Width = 150,
                Height = 40,
                Top = pic.Bottom + 4,
                Left = 0,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
            };

            panel.Controls.Add(pic);
            panel.Controls.Add(lbl);

            // clique no painel ou label abre preview
            pic.Click += async (s, e) => { await OnThumbnailClicked(entry); };
            lbl.Click += async (s, e) => { await OnThumbnailClicked(entry); };

            return panel;
        }

        // baixa thumbnail, cria imagem 16:9 com qualidade e coloca no panel
        private async Task<Panel> LoadThumbnailAndFillPanel(ImageEntry entry, Panel panel)
        {
            try
            {
                var pic = panel.Controls.OfType<PictureBox>().FirstOrDefault();
                if (pic == null) return panel;

                string local = await GetCachedImagePath(entry);
                if (!File.Exists(local))
                {
                    // garante download direto
                    var tmp = await DownloadToCache(entry);
                    local = tmp;
                }

                // cria thumbnail de 160x90 mantendo aspecto via DrawImage (alta qualidade)
                using (var src = Image.FromFile(local))
                {
                    var targetW = 150; // largura interna do picturebox
                    var targetH = 84;  // 16:9 ~ 150x84
                    var thumbBmp = new Bitmap(targetW, targetH);
                    using (var g = Graphics.FromImage(thumbBmp))
                    {
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        // calcula crop center para 16:9 sem distorcer
                        float srcRatio = (float)src.Width / src.Height;
                        float targetRatio = (float)targetW / targetH;
                        Rectangle srcRect;
                        if (srcRatio > targetRatio)
                        {
                            // imagem mais larga -> crop lateral
                            int newWidth = (int)(src.Height * targetRatio);
                            int x = (src.Width - newWidth) / 2;
                            srcRect = new Rectangle(x, 0, newWidth, src.Height);
                        }
                        else
                        {
                            // imagem mais alta -> crop vertical
                            int newHeight = (int)(src.Width / targetRatio);
                            int y = (src.Height - newHeight) / 2;
                            srcRect = new Rectangle(0, y, src.Width, newHeight);
                        }

                        g.DrawImage(src, new Rectangle(0, 0, targetW, targetH), srcRect, GraphicsUnit.Pixel);
                    }

                    // seta a imagem no PictureBox sem travar o arquivo fonte
                    var imgCopy = new Bitmap(thumbBmp);
                    // free thumbBmp before assigning
                    thumbBmp.Dispose();
                    // invoke em UI thread se necessário
                    if (pic.InvokeRequired)
                        pic.Invoke(new Action(() => pic.Image = imgCopy));
                    else
                        pic.Image = imgCopy;
                }
            }
            catch
            {
                // falha silenciosa na thumbnail — não atrapalha demais
            }

            return panel;
        }

        private async Task<string> GetCachedImagePath(ImageEntry entry)
        {
            // derive filename from ID or URL
            string id = GetIdFromUrl(entry.Url);
            string ext = ".jpg";
            return Path.Combine(cacheDir, id + ext);
        }

        private string GetIdFromUrl(string url)
        {
            // try to extract last segment
            try
            {
                var u = new Uri(url);
                return Path.GetFileName(u.AbsolutePath).Split('?')[0];
            }
            catch
            {
                return Guid.NewGuid().ToString("n").Substring(0, 8);
            }
        }

        private async Task<string> DownloadToCache(ImageEntry entry)
        {
            string id = GetIdFromUrl(entry.Url);
            string target = Path.Combine(cacheDir, id + ".jpg");
            if (File.Exists(target)) return target;

            // ensure direct download from pixeldrain: convert /u/ to /api/file/
            var direct = entry.Url;
            if (direct.Contains("/u/") && !direct.Contains("/api/file/"))
            {
                direct = direct.Replace("/u/", "/api/file/");
            }

            using (var resp = await http.GetAsync(direct))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        $"Falha ao baixar \"{entry.Name}\": {resp.StatusCode}",
                        "Erro de download", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    throw new HttpRequestException($"Status: {resp.StatusCode}");
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(target, bytes);
            }
            return target;
        }

        private async Task OnThumbnailClicked(ImageEntry entry)
        {
            // on click, show preview form (download if needed)
            ToggleControls(false);
            lblStatus.Text = $"Preparando preview: {entry.Name}";

            string path = await DownloadToCache(entry);

            var preview = new PreviewForm(entry.Name, path);
            var res = preview.ShowDialog(this);

            if (res == DialogResult.OK)
            {
                // user chose to apply
                bool applied = await ApplyWorkflowAsync(path);
                if (applied)
                {
                    MessageBox.Show("Imagem aplicada com sucesso.", "Feito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            ToggleControls(true);
            lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";
            preview.Dispose();
        }

        private async Task<bool> ApplyWorkflowAsync(string imagePath)
        {
            try
            {
                // backup current TranscodedWallpaper if exists (persistente)
                string systemTranscoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                if (File.Exists(systemTranscoded))
                {
                    string backupTarget = Path.Combine(backupDir, "TranscodedWallpaper_original_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak");
                    try { File.Copy(systemTranscoded, backupTarget, overwrite: false); lastAppliedPath = backupTarget; } catch { /* non-fatal */ }
                }

                // move or copy the image to final location expected by Windows
                string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                // copy to ensure not locking the cached file
                File.Copy(imagePath, dest, true);

                // apply via SystemParametersInfo
                // try API first
                bool ok = WallpaperManager.SetWallpaper(dest);
                if (!ok)
                {
                    // fallback: copy to TranscodedWallpaper and set registry (works even se personalização estiver bloqueada)
                    try
                    {
                        ApplyViaTranscodedWallpaper(dest);
                    }
                    catch (Exception ex2)
                    {
                        throw new Exception("Falha ao aplicar wallpaper (API e fallback): " + ex2.Message);
                    }
                }

                // optional: ask user to restart explorer
                var choice = MessageBox.Show("Deseja reiniciar o Explorer agora para aplicar imediatamente?", "Aplicar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (choice == DialogResult.Yes)
                {
                    RestartExplorer();
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao aplicar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ApplyViaTranscodedWallpaper(string sourceFile)
        {
            string themePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");

            File.Copy(sourceFile, themePath, true);

            // também tenta atualizar o valor no registry para maior compatibilidade
            try
            {
                Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", themePath);
            }
            catch { /* ignore se não puder escrever */ }

            // Usa o helper já existente para chamar a API (encapsulado no WallpaperManager)
            try
            {
                WallpaperManager.SetWallpaper(themePath);
            }
            catch
            {
                // se mesmo assim falhar, não há muito o que fazer aqui além de notificar o usuário
                throw new Exception("Aplicação via TranscodedWallpaper falhou ao acionar a API de atualização.");
            }
        }

        private void RestartExplorer()
        {
            try
            {
                var procs = System.Diagnostics.Process.GetProcessesByName("explorer");
                foreach (var p in procs) p.Kill();
                Task.Delay(500).Wait();
                System.Diagnostics.Process.Start("explorer.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao reiniciar Explorer: {ex.Message}", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            _ = ReloadAndPopulateAsync();
        }

        private async Task ReloadAndPopulateAsync()
        {
            ToggleControls(false);
            lblStatus.Text = "Atualizando catálogo...";
            await LoadImages();
            await RefreshViewAsync();
            ToggleControls(true);
            lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";
        }

        private async void chkShowPreviews_CheckedChanged(object sender, EventArgs e)
        {
            await RefreshViewAsync();
        }

        private void btnUndo_Click(object sender, EventArgs e)
        {
            // restore last backup if exists
            if (string.IsNullOrEmpty(lastAppliedPath) || !File.Exists(lastAppliedPath))
            {
                var files = Directory.GetFiles(backupDir, "TranscodedWallpaper_original_*.bak").OrderByDescending(f => f).ToArray();
                if (files.Length > 0) lastAppliedPath = files[0];
            }

            if (!string.IsNullOrEmpty(lastAppliedPath) && File.Exists(lastAppliedPath))
            {
                try
                {
                    string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
                    File.Copy(lastAppliedPath, dest, true);
                    WallpaperManager.SetWallpaper(dest);
                    MessageBox.Show("Wallpaper anterior restaurado.", "Restaurado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao restaurar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Backup não encontrado.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // delete cache folder to remove traces
            try
            {
                if (Directory.Exists(cacheDir))
                    Directory.Delete(cacheDir, recursive: true);
            }
            catch { /* ignore */ }
        }

        private async Task RefreshViewAsync()
        {
            flpGrid.Visible = chkShowPreviews.Checked;
            listWallpapers.Visible = !chkShowPreviews.Checked;

            if (chkShowPreviews.Checked)
                await PopulateGridAsync();
            else
                PopulateList();
        }
        private void PopulateList()
        {
            listWallpapers.Items.Clear();
            foreach (var img in Images)
            {
                listWallpapers.Items.Add(img.FileName);
            }
        }
        private async void listWallpapers_DoubleClick(object sender, EventArgs e)
        {
            if (listWallpapers.SelectedItems.Count == 0)
                return;

            int index = listWallpapers.SelectedIndex;
            if (index < 0 || index >= Images.Count)
                return;

            var img = Images[index];
            string path = await DownloadToCache(img);
            using (var preview = new PreviewForm(img.Name, path))
            {
                preview.ShowDialog();
            }

        }
    }


    public class ImageEntry
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; } = "NSFW";
        public string FileName
        {
            get
            {
                try
                {
                    return Path.GetFileName(new Uri(Url).AbsolutePath);
                }
                catch
                {
                    return Name ?? Url;
                }
            }
        }

    }

    public static class WallpaperManager
    {
        private const int SPI_SETDESKWALLPAPER = 0x14;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int action, int uParam, string vParam, int winIni);

        public static bool SetWallpaper(string file)
        {
            return SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, file,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
    }
}
