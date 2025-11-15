<p align="center">
  <img src="assets/logo.svg" alt="WallpaperSync logo" width="240"/>
</p>

<p align="center">
  Troca automatizada de papéis de parede
</p>

<p align="center">
  <a href="#recursos">Recursos</a> •
  <a href="#comparação-entre-versões">Comparação entre versões</a> •
  <a href="#downloads">Downloads</a> •
  <a href="#perguntas-frequentes-faq">FAQ</a> •
  <a href="#licença">Licença</a>
</p>

**WallpaperSync** é uma ferramenta que lista imagens hospedadas em um servidor (ou uma imagem fornecida pelo usuário) e aplica a imagem selecionada como papel de parede do Windows.  
Ideal para quem quer trocar rapidamente papéis de parede sem criar vestígios desnecessários.

---

## Principais pontos

- **Rápido** — lista e baixa a imagem escolhida em poucos segundos.  
- **Discreto** — operações locais, sem criar logs persistentes por padrão.  
- **Profissional** — mensagens claras, confirmações e proteções contra erros (rate-limit, downloads inválidos).

---

## Recursos

- Agregação automática de **todas** as imagens presentes no servidor.
- Suporte a imagem customizada fornecida pelo usuário.
- Ordenação contínua por nomes numéricos (ex.: `1.jpg`, `2.jpg`, ...).
- Download resiliente com retries e verificação básica de integridade.
- Substituição do papel de parede do Windows via API com fallback.
- Confirmações antes de qualquer alteração e antes de reiniciar/desligar.

---

## ⚠️ Aviso importante — leia antes de usar

- Este programa **pode alterar arquivos do sistema de forma direta**.  
- As imagens disponibilizadas contém **conteúdo adulto / NSFW.**
- O wallpaper possivelmente só será efetivamente aplicado após **reiniciar o Explorador de Arquivos**. O programa oferece essa opção automaticamente com confirmação.

Se você não concorda: **não execute** o programa.

---

## Requisitos

- Windows 10/11
- PowerShell 5.1 recomendado (para a versão Script PowerShell)
- Acesso à internet (para listar e baixar imagens do servidor)

> Se necessário, você pode permitir execução temporária do script com:
> ```powershell
> Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
> ```

---
## Comparação entre versões

| Feature / Recurso                     | Script PowerShell | App GUI (EXE) |
|---------------------------------------|:-----------------:|:-------------:|
| Aplicar wallpaper                     | ✔️                | ✔️            |
| Listagem de wallpapers                | ✔️                | ✔️            |
| Prévias                               | ❌                | ✔️            |
| Fallback automático (TranscodedWallpaper) | ✔️            | ✔️            |
| Interface gráfica                     | ❌                | ✔️            |
| Atualizações futuras                  | ❌ (somente patches) | ✔️         |
| Categorias                            | ❌                | 🔜 (em breve) |

---
## Downloads

### **GUI - Windows App**
**[Download V1.0.0 (Gui)](https://release-assets.githubusercontent.com/github-production-release-asset/1081555498/525b72e2-7507-4580-9d63-0744e86e00a9?sp=r&sv=2018-11-09&sr=b&spr=https&se=2025-11-14T19%3A13%3A28Z&rscd=attachment%3B+filename%3DWallpaperSync.exe&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2025-11-14T18%3A12%3A34Z&ske=2025-11-14T19%3A13%3A28Z&sks=b&skv=2018-11-09&sig=lptXLV%2B3rOkEfhw%2FcKbvAmNO07mOyXbBl37XJCWCUuY%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc2MzE0ODE5NCwibmJmIjoxNzYzMTQ0NTk0LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.WmJs39WO30HPhKgRPxvYPbbLy2J4rlTfiLQgGJcFmss&response-content-disposition=attachment%3B%20filename%3DWallpaperSync.exe&response-content-type=application%2Foctet-stream)**

### **Script PowerShell**
**[Download V1.0.0 (PowerShell)](https://release-assets.githubusercontent.com/github-production-release-asset/1081555498/96f8c53d-bd84-47b8-9d7f-ececb9a20c3a?sp=r&sv=2018-11-09&sr=b&spr=https&se=2025-11-15T00%3A51%3A46Z&rscd=attachment%3B+filename%3DWallpaperSync.ps1&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2025-11-14T23%3A51%3A17Z&ske=2025-11-15T00%3A51%3A46Z&sks=b&skv=2018-11-09&sig=esMSOoGnG8P5AJpcAGAyHlEfpPwe3%2BURI%2ByTQhhbOzs%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc2MzE2NTY2NiwibmJmIjoxNzYzMTY1MzY2LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.Rqv_n-GX_0O4_QB1n6_B5rbYio_6HGsumBER-Fa_uFo&response-content-disposition=attachment%3B%20filename%3DWallpaperSync.ps1&response-content-type=application%2Foctet-stream)**

<details open>
<summary>Como utilizar a versão Script PowerShell</summary>

1. Faça o download do `WallpaperSync.ps1` na página de **Releases**.  
2. Clique com o botão direito → **Executar com PowerShell**.  
3. Leia o aviso e confirme.
4. Siga as instruções exibidas no terminal.

</details>

---

## Perguntas frequentes (FAQ)

**P:** WallpaperSync envia dados para fora do meu computador?  
**R:** Não. Não há telemetria.  As versões GUI e Script PowerShell apenas baixam imagens do servidor.

**P:** O WallpaperSync deixa algum rastro?
**R:** As imagens são baixadas de forma temporária e removidas após realizar o processo.

**P:** Posso desfazer a alteração?  
**R:** Sim — tanto o script quanto a GUI possuem opções de restaurar o papel de parede original.

**P:** Posso utilizar uma imagem minha?  
**R:** Sim — é possível fornecer uma imagem própria em ambas as versões.

---

## Privacidade & responsabilidade

Você é o único responsável pelo uso deste software e pelo conteúdo aplicado.  
Não utilize em equipamentos de terceiros ou ambientes corporativos sem autorização.  
Ao usar o WallpaperSync, você declara estar ciente dos riscos e concorda em assumir a responsabilidade.

---

## Licença

Uso pessoal e não comercial. 
Nenhuma garantia é fornecida - use por sua conta e risco.

---

## Contato / Contribuições

Problemas, dúvidas ou sugestões → abra um **Issue** neste repositório.
