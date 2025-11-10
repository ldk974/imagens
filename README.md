# WallpaperSync — Troca automatizada de wallpapers (conteúdo adulto) 🔒🖼️

**WallpaperSync** é um utilitário em PowerShell que lista imagens hospedadas nos *Releases* deste repositório e aplica uma imagem selecionada como papel de parede do Windows.  
Feito para quem quer trocar rapidamente wallpapers pessoais sem criar vestígios desnecessários — com consentimento explícito e total controle do usuário.

---

## Principais pontos (na lata)

- **Rápido** — lista e baixa a imagem escolhida em poucos segundos.  
- **Discreto** — operações locais, sem criar logs persistentes por padrão.  
- **Profissional** — mensagens claras, confirmações e proteções contra erros (rate-limit, downloads inválidos).  
- **Direto** — não tenta disfarçar o propósito: foi feito para aplicar wallpapers pessoais, incluindo conteúdo adulto. Use com responsabilidade.

---

## Recursos

- Agrega automaticamente **todos** os assets `.jpg` presentes nos *Releases* do repositório.
- Ordenação contínua por nomes numéricos (ex.: `1.jpg`, `2.jpg`, ...).
- Mostra contador público de downloads (dados do GitHub).
- Download resiliente com retries e verificação básica de integridade.
- Substitui o arquivo de tema do Windows em `%AppData%\Microsoft\Windows\Themes\TrancodedWallpaper` (sem extensão).
- Confirmações explícitas antes de qualquer alteração e antes de reiniciar/desligar.
- Fecha automaticamente ao terminar.

---

## ⚠️ Aviso importante — leia antes de usar

- **Este script altera arquivos do sistema** (pasta de temas do Windows).  
- **As imagens podem conter conteúdo adulto / NSFW.**  
- **Não** rode este script em computadores que você não possui ou sem autorização explícita (trabalho, máquina compartilhada, equipamento de terceiros).  
- O wallpaper só será efetivamente aplicado após **reiniciar** o sistema. O script oferece a opção de reiniciar automaticamente com confirmação.

Se você não concorda: **não execute** o script.

---

## Requisitos

- Windows 10/11 (PowerShell 5.1 recomendado)  
- Acesso à internet (para listar e baixar assets do GitHub)  
- Salve o script com **UTF-8 with BOM** (para acentuação correta) — o README traz instruções

> Se necessário, você pode permitir execução temporária com:
> ```powershell
> Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
> ```

---

## Como executar

1. Faça o download do `baixar_imagem_releases_final.ps1` na página de **Releases**.  
2. Clique com o botão direito → **Executar com PowerShell**.  
3. Leia o aviso e confirme.  
4. Escolha a imagem na lista (os arquivos aparecem numerados).  
5. Confirme a operação. O arquivo será baixado e substituirá o arquivo em:
