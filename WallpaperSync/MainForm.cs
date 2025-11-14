using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using WallpaperSync;

namespace WallpaperSync
{
    public partial class MainForm : Form
    {
        private readonly List<ImageEntry> Images = new();
        private readonly string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallpaperSync");
        private readonly string cacheDir;
        private readonly string backupDir;
        private string lastAppliedPath = null; // caminho pra fazer o backup do wallpaper original lá

        private readonly HttpClient http;
        private readonly string transcodedPath;

        public MainForm()
        {
            InitializeComponent();

            http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36"
            );

            cacheDir = Path.Combine(appdata, "cache");
            backupDir = Path.Combine(appdata, "backup");
            transcodedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper"
            );

            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(backupDir);

            listWallpapers.DisplayMember = "Name";
            chkShowPreviews.Checked = false;

            Load += MainForm_Load;

            DebugLogger.Log("MainForm inicializada. Diretórios criados.");

        }

        private static List<string> ParseHtmlLinks(string html)
        {
            if (string.IsNullOrEmpty(html)) return new List<string>();

            try
            {
                var matches = Regex.Matches(html, "<a\\s+href\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                return matches
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(h =>
                        h.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        h.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        h.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        h.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }


        // gera URL da pasta de wallpapers (ex: {baseUrl}wallpapers/sfw/)
        private string BuildWallpapersUrl(string baseUrl, string folder)
        {
            if (string.IsNullOrEmpty(baseUrl)) return null;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}wallpapers/{folder}/";
        }

        // gera URL da thumbnail padrão (ex: {baseUrl}thumbs/sfw/arquivo.jpg)
        private string BuildThumbUrl(string baseUrl, string folder, string fileName)
        {
            if (string.IsNullOrEmpty(baseUrl)) return null;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}thumbs/{folder}/{fileName}";
        }

        // tenta variações de nome da thumbnail (sem sufixo e com sufixo _thumb)
        // retorna a URL encontrada (ou null se nenhuma das variações existir)
        private async Task<string> TryThumbVariantsAsync(string thumbBaseUrl, string fileName)
        {
            if (string.IsNullOrWhiteSpace(thumbBaseUrl)) return null;

            try
            {
                // tenta URL exatamente como veio
                DebugLogger.Log($"Verificando existência da thumbnail: {thumbBaseUrl}");
                using var resp = await http.GetAsync(thumbBaseUrl);
                if (resp.IsSuccessStatusCode) return thumbBaseUrl;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Erro verificando thumbnail padrão: {ex.Message}");
            }

            // tenta com sufixo _thumb antes da extensão
            try
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                string altName = $"{nameOnly}_thumb{ext}";
                string altUrl = thumbBaseUrl.EndsWith(fileName) ? thumbBaseUrl.Substring(0, thumbBaseUrl.Length - fileName.Length) + altName
                                                                 : thumbBaseUrl.Replace(fileName, altName);

                DebugLogger.Log($"Tentando variante de thumbnail: {altUrl}");
                using var resp2 = await http.GetAsync(altUrl);
                if (resp2.IsSuccessStatusCode) return altUrl;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Erro verificando variante de thumbnail: {ex.Message}");
            }

            return null;
        }


        private async void MainForm_Load(object sender, EventArgs e)
        {
            #if DEBUG
            var logForm = new DebugLogForm();
            logForm.Show();
            DebugLogger.Log("Aplicativo iniciado em modo DEBUG.");
        #endif

            ToggleControls(false);
            lblStatus.Text = "Carregando catálogo...";

            await LoadImages();
            await RefreshViewAsync();

            ToggleControls(true);
            lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";
            DebugLogger.Log($"Imagens carregadas: {Images.Count}");
        }

        private void ToggleControls(bool enabled)
        {
            chkShowPreviews.Enabled = enabled;
            btnRefresh.Enabled = enabled;
            btnUndo.Enabled = enabled;
        }

        private async Task LoadImages()
        {
            DebugLogger.Log("Iniciando LoadImages()");

            try
            {
                Images.Clear();
                listWallpapers.Items.Clear();

                // baixa o arquivo que contém a base URL
                const string urlTxt = "https://raw.githubusercontent.com/ldk974/WallpaperSync/refs/heads/master/current_urls.txt";
                DebugLogger.Log($"Baixando arquivo de URLs: {urlTxt}");

                string rawContent;
                try
                {
                    rawContent = await http.GetStringAsync(urlTxt);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Falha ao baixar current_urls.txt: {ex.Message}");
                    return;
                }

                string baseUrl = rawContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim();

                if (string.IsNullOrEmpty(baseUrl))
                {
                    DebugLogger.Log("Erro: baseUrl vazio.");
                    return;
                }

                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                DebugLogger.Log($"BaseURL detectada: {baseUrl}");

                // categorias fixas
                string[] folders = new[] { "sfw", "nsfw" };
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var client = http; // usa o HttpClient já existente na classe

                foreach (string folder in folders)
                {
                    string url = $"{baseUrl}wallpapers/{folder}/";
                    DebugLogger.Log($"Lendo pasta: {url}");

                    string html;
                    try
                    {
                        html = await client.GetStringAsync(url);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"Falha ao ler pasta {url}: {ex.Message}");
                        continue;
                    }

                    var links = ParseHtmlLinks(html);

                    foreach (var fileRaw in links)
                    {
                        string urlFull = $"{url}{fileRaw}";
                        if (!seen.Add(urlFull)) continue;

                        string file = Uri.UnescapeDataString(fileRaw);
                        string name = Path.GetFileNameWithoutExtension(file);
                        string ext = Path.GetExtension(file).ToLowerInvariant();

                        string tag = name.Contains("[SFW]", StringComparison.OrdinalIgnoreCase) ? "SFW" :
                                     name.Contains("[NSFW]", StringComparison.OrdinalIgnoreCase) ? "NSFW" :
                                     folder.ToUpper();

                        string thumbCandidate = BuildThumbUrl(baseUrl, folder, fileRaw);

                        Images.Add(new ImageEntry
                        {
                            Url = urlFull,
                            ThumbnailUrl = thumbCandidate,
                            Name = name,
                            Tag = tag,
                            OriginalExtension = ext
                        });
                    }
                }

                DebugLogger.Log($"LoadImages() finalizado. Total de itens: {Images.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"LoadImages() falhou: {ex}");
            }
        }



        private async Task PopulateGridAsync()
        {
            flpGrid.SuspendLayout();
            flpGrid.Controls.Clear();
            var tasks = new List<Task<Panel>>();
            int total = Images.Count;
            int done = 0;

            foreach (var entry in Images)
            {
                var panel = CreateThumbnailCardPlaceholder(entry);
                flpGrid.Controls.Add(panel);
                tasks.Add(LoadThumbnailAndFillPanel(entry, panel).ContinueWith(t =>
                {
                    System.Threading.Interlocked.Increment(ref done);
                    var s = $"Carregando thumbnails... {done}/{total}";
                    if (lblStatus.InvokeRequired) lblStatus.Invoke(new Action(() => lblStatus.Text = s));
                    else lblStatus.Text = s;
                    return t.Result;
                }));
            }

            await Task.WhenAll(tasks);
            if (lblStatus.InvokeRequired) lblStatus.Invoke(new Action(() => lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)"));
            else lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";

            flpGrid.ResumeLayout();
        }

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
                Height = 90, // thumbnail 16:9
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
                var lbl = panel.Controls.OfType<Label>().FirstOrDefault();
                if (lbl != null)
                    lbl.InvokeIfRequired(() => lbl.Text = string.IsNullOrEmpty(entry.Name) ? entry.FileName : entry.Name);

                if (pic == null) return panel;

                // gera a thumbnail
                string thumbPath = await DownloadThumbnail(entry);

                // carrega thumb do cache para evitar trava no arquivo fonte
                using (var tb = Image.FromFile(thumbPath))
                {
                    var imgCopy = new Bitmap(tb);
                    if (pic.InvokeRequired)
                        pic.Invoke(new Action(() => { pic.Image?.Dispose(); pic.Image = imgCopy; }));
                    else
                    {
                        pic.Image?.Dispose();
                        pic.Image = imgCopy;
                    }
                }
            }
            catch
            {

            }

            return panel;
        }


        private string GetSafeFileNameFromUrl(ImageEntry entry)
        {
            if (entry == null)
                return Guid.NewGuid().ToString("N") + ".jpg";

            string ext = entry.OriginalExtension;
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            // usa a URL completa (ou ThumbnailUrl) para gerar hash
            string raw = (entry.ThumbnailUrl ?? entry.Url) ?? Guid.NewGuid().ToString("N");

            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(raw.ToLowerInvariant());
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 20);
            }

            string namePart = entry.Name;
            foreach (char c in Path.GetInvalidFileNameChars())
                namePart = namePart.Replace(c, '_');

            if (string.IsNullOrWhiteSpace(namePart))
                namePart = "img_" + hash;

            return $"{namePart}_{hash}{ext}";
        }



        private async Task<string> DownloadToCache(ImageEntry entry)
        {
            var originalsDir = Path.Combine(cacheDir, "originals");
            Directory.CreateDirectory(originalsDir);

            string safeName = GetSafeFileNameFromUrl(entry);
            string ext = Path.GetExtension(safeName);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            string targetPath = Path.Combine(originalsDir, Path.GetFileNameWithoutExtension(safeName) + ext);

            // se não existe não baixa dnv
            if (File.Exists(targetPath))
                return targetPath;

            // usa a URL original, não a de thumbnail
            string downloadUrl = entry.Url;

            lblStatus.InvokeIfRequired(() =>
                lblStatus.Text = $"Baixando original: {entry.Name}"
            );

            using (var resp = await http.GetAsync(downloadUrl))
            {
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"Falha ao baixar imagem ({resp.StatusCode})");

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(targetPath, bytes);
            }

            return targetPath;
        }

        private async Task<string> DownloadThumbnail(ImageEntry entry)
        {
            string safe = GetSafeFileNameFromUrl(entry);
            if (string.IsNullOrWhiteSpace(safe))
                safe = Guid.NewGuid().ToString("N") + ".jpg";

            var thumbsDir = Path.Combine(cacheDir, "thumbs");
            Directory.CreateDirectory(thumbsDir);

            string thumbPath = Path.Combine(thumbsDir, Path.GetFileNameWithoutExtension(safe) + ".thumb.jpg");

            if (File.Exists(thumbPath))
                return thumbPath;

            // tenta a thumbnail remota (padrão)
            string downloadUrlThumb = entry.ThumbnailUrl;
            if (!string.IsNullOrEmpty(downloadUrlThumb))
            {
                // garante que a URL está codificada certa
                try { downloadUrlThumb = Uri.EscapeUriString(downloadUrlThumb); } catch { }

                DebugLogger.Log($"Tentando baixar thumbnail (padrão): {downloadUrlThumb}");
                try
                {
                    using (var resp = await http.GetAsync(downloadUrlThumb))
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(thumbPath, bytes);
                            DebugLogger.Log($"Thumbnail salva: {thumbPath}");
                            return thumbPath;
                        }
                        else
                        {
                            DebugLogger.Log($"Thumbnail remota não encontrada (status {resp.StatusCode}). Tentando variantes...");
                        }
                    }
                }
                catch (Exception exThumb)
                {
                    DebugLogger.Log($"Erro ao baixar thumbnail padrão: {exThumb.Message}. Tentando variantes...");
                }

                // tenta variantes (_thumb)
                try
                {
                    string fileName = Path.GetFileName(downloadUrlThumb);
                    string urlVariant = await TryThumbVariantsAsync(downloadUrlThumb, fileName);
                    if (!string.IsNullOrEmpty(urlVariant))
                    {
                        DebugLogger.Log($"Thumbnail variante encontrada: {urlVariant}");
                        using var resp2 = await http.GetAsync(urlVariant);
                        if (resp2.IsSuccessStatusCode)
                        {
                            var bytes2 = await resp2.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(thumbPath, bytes2);
                            DebugLogger.Log($"Thumbnail variante salva: {thumbPath}");
                            return thumbPath;
                        }
                    }
                    else
                    {
                        DebugLogger.Log("Nenhuma variante de thumbnail remota encontrada.");
                    }
                }
                catch (Exception exVariant)
                {
                    DebugLogger.Log($"Erro ao testar variantes de thumbnail: {exVariant.Message}");
                }
            }

            // fallback: baixar original e gera thumbnail local
            DebugLogger.Log($"Baixando original para gerar thumbnail local: {entry.Url}");
            try
            {
                using (var resp = await http.GetAsync(entry.Url))
                {
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException($"Falha ao baixar imagem original ({resp.StatusCode})");

                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    using (var ms = new MemoryStream(bytes))
                    using (var src = Image.FromStream(ms))
                    {
                        int targetW = 150;
                        int targetH = 84;

                        using var thumbBmp = new Bitmap(targetW, targetH);
                        using (var g = Graphics.FromImage(thumbBmp))
                        {
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                            float srcRatio = (float)src.Width / src.Height;
                            float targetRatio = (float)targetW / targetH;
                            Rectangle srcRect;
                            if (srcRatio > targetRatio)
                            {
                                int newWidth = (int)(src.Height * targetRatio);
                                int x = (src.Width - newWidth) / 2;
                                srcRect = new Rectangle(x, 0, newWidth, src.Height);
                            }
                            else
                            {
                                int newHeight = (int)(src.Width / targetRatio);
                                int y = (src.Height - newHeight) / 2;
                                srcRect = new Rectangle(0, y, src.Width, newHeight);
                            }

                            g.DrawImage(src, new Rectangle(0, 0, targetW, targetH), srcRect, GraphicsUnit.Pixel);
                        }

                        thumbBmp.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Falha ao gerar thumbnail local: {ex.Message}");
                throw;
            }

            DebugLogger.Log($"Thumbnail gerada localmente: {thumbPath}");
            return thumbPath;
        }




        private async Task OnThumbnailClicked(ImageEntry entry)
        {
            // quando clicado, mostra o preview grandão (baixa se num tiver)
            ToggleControls(false);
            lblStatus.Text = $"Preparando preview: {entry.Name}";

            string path = await DownloadToCache(entry);

            var preview = new PreviewForm(entry.Name, path);
            var res = preview.ShowDialog(this);

            if (res == DialogResult.OK)
            {
                // decidiu aplicar
                bool applied = await ApplyWorkflowAsync(path);
                if (applied)
                {
                    DebugLogger.Log("Imagem aplicada com sucesso. Feito");
                }
            }

            ToggleControls(true);
            lblStatus.Text = $"Catálogo carregado ({Images.Count} imagens)";
            preview.Dispose();
        }

        private async Task<bool> ApplyWorkflowAsync(string imagePath)
        {
            DebugLogger.Log("");
            DebugLogger.Log("==============================================");
            DebugLogger.Log("Iniciando ApplyWorkflowAsync");
            DebugLogger.Log($"Timestamp inicial: {DateTime.Now:HH:mm:ss.fff}");
            DebugLogger.Log($"Imagem recebida: {imagePath}");

            try
            {
                // backup do transcoded original
                if (File.Exists(transcodedPath))
                {
                    string backupTarget = Path.Combine(
                        backupDir,
                        "TranscodedWallpaper_original_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak"
                    );

                    File.Copy(transcodedPath, backupTarget, false);
                    lastAppliedPath = backupTarget;

                    DebugLogger.Log($"Backup criado: {backupTarget}");
                }
                else
                {
                    DebugLogger.Log("Nenhum transcodedPath existente para backup.");
                }

                // copia pro transcoded
                File.Copy(imagePath, transcodedPath, true);
                DebugLogger.Log($"Arquivo copiado para transcodedPath: {transcodedPath}");

                // tenta mandar pela api
                DebugLogger.Log("Método preferencial: Aplicação via API (WallpaperManager.SetWallpaper)");
                bool apiOk = WallpaperManager.SetWallpaper(transcodedPath);
                DebugLogger.Log($"Resultado da API: {(apiOk ? "Sucesso" : "FALHA")}");

                if (!apiOk)
                {
                    DebugLogger.Log("API falhou — iniciando fallback para TranscodedWallpaper.");
                    bool fallbackOk = ApplyViaTranscodedWallpaper(transcodedPath);

                    DebugLogger.Log($"Resultado do fallback TranscodedWallpaper: {(fallbackOk ? "Sucesso" : "FALHA")}");

                    if (!fallbackOk)
                    {
                        DebugLogger.Log("Fallback também falhou. Aplicação do wallpaper não concluída.");
                        DebugLogger.Log("==============================================");
                        return false;
                    }
                }
                else
                {
                    DebugLogger.Log("Aplicação concluída via API (sem fallback).");
                }

                DebugLogger.Log("Aplicação finalizada com SUCESSO.");
                DebugLogger.Log($"Timestamp final: {DateTime.Now:HH:mm:ss.fff}");
                DebugLogger.Log("==============================================");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ERRO em ApplyWorkflowAsync: {ex}");
                DebugLogger.Log("==============================================");
                return false;
            }
        }



        private bool ApplyViaTranscodedWallpaper(string sourceFile)
        {
            DebugLogger.Log("Método selecionado: Aplicação via TranscodedWallpaper");
            DebugLogger.Log($"Arquivo de origem: {sourceFile}");

            string themePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper"
            );

            DebugLogger.Log($"Destino do TranscodedWallpaper: {themePath}");

            try
            {
                File.Copy(sourceFile, themePath, true);
                DebugLogger.Log("Arquivo copiado com sucesso para TranscodedWallpaper.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Falha ao copiar para TranscodedWallpaper: {ex.Message}");
                return false;
            }

            // atualiza o registro
            try
            {
                Microsoft.Win32.Registry.SetValue(
                    @"HKEY_CURRENT_USER\Control Panel\Desktop",
                    "WallPaper",
                    themePath
                );
                DebugLogger.Log("Registro atualizado com sucesso (HKCU\\...\\WallPaper).");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Aviso: não foi possível atualizar o registry: {ex.Message}");
            }

            // forçar atualização do explorer
            try
            {
                bool ok = WallpaperManager.SetWallpaper(themePath);
                DebugLogger.Log($"WallpaperManager.SetWallpaper (via Transcoded) retornou: {(ok ? "Sucesso" : "FALHA")}");
                return ok;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Erro ao acionar API no método TranscodedWallpaper: {ex.Message}");
                return false;
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            _ = ReloadAndPopulateAsync();
            DebugLogger.Log("Atualizando lista");
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
            DebugLogger.Log("Usuário clicou em UNDO.");

            try
            {
                if (string.IsNullOrEmpty(lastAppliedPath) || !File.Exists(lastAppliedPath))
                {
                    var files = Directory.GetFiles(backupDir, "TranscodedWallpaper_original_*.bak")
                                         .OrderByDescending(f => f)
                                         .ToArray();

                    if (files.Length > 0) lastAppliedPath = files[0];
                }

                if (string.IsNullOrEmpty(lastAppliedPath))
                {
                    DebugLogger.Log("Nenhum backup encontrado.");
                    MessageBox.Show("Backup não encontrado.");
                    return;
                }

                File.Copy(lastAppliedPath, transcodedPath, true);
                WallpaperManager.SetWallpaper(transcodedPath);

                DebugLogger.Log($"Wallpaper restaurado a partir de: {lastAppliedPath}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Erro ao restaurar wallpaper: {ex}");
                MessageBox.Show($"Falha: {ex.Message}");
            }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (Directory.Exists(cacheDir))
                    Directory.Delete(cacheDir, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao limpar cache: {ex.Message}");
            }
        }

        private async Task RefreshViewAsync()
        {
            flpGrid.Visible = chkShowPreviews.Checked;
            listWallpapers.Visible = !chkShowPreviews.Checked;

            if (chkShowPreviews.Checked)
            {
                await PopulateGridAsync();
                DebugLogger.Log("Mostrando grade");
            }
            else
            {
                PopulateList();
                DebugLogger.Log("Mostrando lista");
            }
        }
        private void PopulateList()
        {
            listWallpapers.BeginUpdate();
            listWallpapers.Items.Clear();
            foreach (var img in Images)
            {
                listWallpapers.Items.Add(img);
            }
            listWallpapers.EndUpdate();
        }
        private async void listWallpapers_DoubleClick(object sender, EventArgs e)
        {
            var selected = listWallpapers.SelectedItem;
            if (selected is not ImageEntry img)
                return;

            string path = await DownloadToCache(img);
            using (var preview = new PreviewForm(img.Name, path))
            {
                preview.ShowDialog();
            }

        }
    }

    public static class ControlExtensions
    {
        public static void InvokeIfRequired(this Control c, Action a)
        {
            if (c == null) return;
            if (c.InvokeRequired) c.Invoke(a);
            else a();
        }
    }

    public class ImageEntry
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; } = "NSFW";
        public string OriginalExtension { get; set; } = ".jpg";
        public string ThumbnailUrl { get; set; }
        public string FileName

        {
            get
            {
                try
                {
                    var p = new Uri(Url).AbsolutePath;
                    var file = Path.GetFileName(Uri.UnescapeDataString(p));
                    return file;
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
