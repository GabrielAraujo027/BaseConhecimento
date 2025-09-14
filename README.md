# Unitá Soluções Digitais — Sistema de Chamados + IA (Local)

Este projeto implementa um **sistema de chamados corporativo** com backend em **C#/.NET 8**, autenticação **JWT + Identity (usuários e roles)**, e um **assistente de IA** integrado via **Ollama** (modelos locais).  
Áreas contempladas: **TI, Facilities, Financeiro, RH, etc.** — com abertura, acompanhamento e encaminhamento de chamados, além de respostas automáticas baseadas em **base de conhecimento**.

---

## Sumário
- [Arquitetura](#arquitetura)
- [Pré-requisitos](#pré-requisitos)
- [Instalação (passo a passo)](#instalação-passo-a-passo)
- [Configuração (appsettings)](#configuração-appsettings)
- [Rodando em HTTP (sem HTTPS)](#rodando-em-http-sem-https)
- [Ollama (IA local)](#ollama-ia-local)
- [Migrações do EF Core](#migrações-do-ef-core)
- [Autenticação & Perfis](#autenticação--perfis)
- [APIs — Referência Rápida](#apis--referência-rápida)
- [Exemplos de Requisição (fetch/curl)](#exemplos-de-requisição-fetchcurl)
- [Front-end local](#front-end-local)
- [Solução de Problemas](#solução-de-problemas)

---

## Arquitetura

```
Frontend (HTML/JS) ─────────► Backend ASP.NET (.NET 8)
                               │  ├─ Identity + JWT
                               │  ├─ EF Core (SQL Server LocalDB/SQL Server)
                               │  ├─ Chamados (CRUD, Relatórios)
                               │  └─ Knowledge (ingest, chat)
                               └─► Ollama (LLM local: llama3 / nomic-embed-text)
```

---

## Pré-requisitos

1) **.NET SDK 8.x**  
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0

2) **SQL Server LocalDB** (ou SQL Server)  
   - Para LocalDB (Windows), costuma vir com o Visual Studio.  
   - Ou use um SQL Server local/contêiner e ajuste a connection string.

3) **Ollama** (Windows/macOS/Linux)  
   https://ollama.com/download

4) **Node/Live Server (opcional para front)**  
   - Extensão “Live Server” no VS Code, ou qualquer servidor estático.

---

## Instalação (passo a passo)

> Entre na pasta do backend (ex.: `cd Back`).

1. **Restaurar pacotes**
   ```bash
   dotnet restore
   ```

2. **NuGets principais (se faltar algo)**
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore --version 8.0.20
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.20
   dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.20
   dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.20
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.20
   dotnet add package Swashbuckle.AspNetCore --version 6.5.0
   ```

3. **Ferramenta do EF Core (CLI)**
   ```bash
   dotnet tool install -g dotnet-ef --version 8.0.20
   # Se já tiver, atualize:
   dotnet tool update -g dotnet-ef --version 8.0.20
   ```

4. **Configurar appsettings** (abaixo) e **launchSettings.json** (HTTP).

5. **Criar/Atualizar banco**
   - Veja a seção [Migrações do EF Core](#migrações-do-ef-core).

6. **Preparar o Ollama** (modelos) — veja [Ollama (IA local)](#ollama-ia-local).

7. **Rodar o backend**
   ```bash
   dotnet run
   ```
   A API deve subir em algo como: `http://localhost:5206` (Swagger em `/swagger`).

---

## Configuração (appsettings)

**appsettings.Development.json** (exemplo):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\\\MSSQLLocalDB;Database=BaseConhecimentoDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "CHAVE-DEV-SUBSTITUA-POR-UMA-CHAVE-GRANDONA",
    "Issuer": "BaseConhecimento.Dev",
    "Audience": "BaseConhecimento.Dev"
  }
}
```

> **Importante**: ajuste `ConnectionStrings:Default` ao seu SQL Server se necessário.  
> A chave `Jwt:Key` deve ser um segredo longo em produção.

---

## Rodando em HTTP (sem HTTPS)

Para evitar redirecionamentos em preflight CORS, estamos rodando **somente HTTP** no dev.

**Properties/launchSettings.json** (exemplo completo — apenas HTTP):
```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:5206",
      "sslPort": 0
    }
  },
  "profiles": {
    "BaseConhecimento": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://0.0.0.0:5206;http://localhost:5206",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://0.0.0.0:5206"
      }
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## Ollama (IA local)

1) Instale o **Ollama**  
   https://ollama.com/download

2) Puxe os modelos:
   ```bash
   ollama pull llama3
   ollama pull nomic-embed-text
   ```

3. **Teste** o serviço:
   ```bash
   # Geração (texto)
   curl -X POST http://localhost:11434/api/generate \
     -H "Content-Type: application/json" \
     -d "{ \"model\": \"llama3\", \"prompt\": \"diga oi\", \"stream\": false }"

   # Embeddings
   curl -X POST http://localhost:11434/api/embeddings \
     -H "Content-Type: application/json" \
     -d "{ \"model\": \"nomic-embed-text\", \"prompt\": \"exemplo de embedding\" }"
   ```

> O backend usa um **HttpClient nomeado** para `http://localhost:11434/`.

---

## Migrações do EF Core

> **Atenção a versões**: use **EF 8.0.20** nos NuGets **e** no `dotnet-ef`.

1) Criar a primeira migração (se ainda não existir):
```bash
dotnet ef migrations add InitialCreate
```

2) Aplicar ao banco:
```bash
dotnet ef database update
```

> Se editar o modelo (ex.: campos novos), crie novas migrações:
```bash
dotnet ef migrations add AddCamposX
dotnet ef database update
```

---

## Autenticação & Perfis

- **Identity + JWT** já configurados.
- **Roles** principais:
  - `Solicitante` — pode usar o chat e abrir chamados.
  - `Atendente` — pode **listar/editar** chamados e **ingestar** conhecimento.

> **Seed** opcional (criar usuários/roles) pode estar no `Program.cs`.  
> Caso contrário, use os endpoints de **Auth** para registrar e promover um usuário (ex.: adicionando role no banco/manual).

---

## APIs — Referência Rápida

### Auth
- `POST /api/Auth/register`  
  `{"email":"user@dominio.com","password":"SuaSenha123!"}`
- `POST /api/Auth/login`  
  `{"email":"user@dominio.com","password":"SuaSenha123!"}`
- `GET /api/Auth/me` (Bearer)

### Knowledge (base de conhecimento)
- `POST /api/knowledge/ingest` *(Atendente)*  
  ```json
  {
    "categoria": "TI",
    "subcategoria": "Reset de senha",
    "conteudo": "Passos para resetar senha...",
    "perguntasFrequentes": "como resetar senha, esqueci a senha, trocar senha"
  }
  ```
- `POST /api/knowledge/ingest/batch` *(Atendente)* — array do mesmo DTO
- `POST /api/knowledge/chat` *(Anônimo permitido)*  
  ```json
  {
    "message": "Meu wifi caiu e preciso de ajuda",
    "history": [
      {"role":"user","content":"aconteceu de novo hoje cedo"}
    ]
  }
  ```

### Chamados
- `GET /api/chamados` *(Atendente)* — **filtros + paginação**, ordena por `DataCriacao desc`  
  Query params: `status, setor, solicitante, de, ate, search, page, pageSize`
- `GET /api/chamados/{id}` *(Atendente)*
- `POST /api/chamados` *(Autenticado)*  
  ```json
  {"titulo":"Impressora sem tinta","descricao":"Pede troca de cartucho","setorResponsavel":"TI"}
  ```
- `PUT /api/chamados/{id}` *(Atendente)*  
  ```json
  {"id":1,"statusEnum":"Concluido","setorResponsavel":"TI"}
  ```
- `DELETE /api/chamados/{id}` *(Atendente)*
- `GET /api/chamados/relatorio` *(Atendente)*  
  Query: `inicio`, `fim`, `setor` (opcional).  
  Retorna: `AbertosNaUltimaHora, Pendentes, EmAndamento, Concluido, Cancelado, TempoMedioConclusaoHoras`.

---

## Front-end local

- Abra o `index.html`/`listagemChamados.html` no VS Code.
- Use **Live Server** (porta 5500): `http://127.0.0.1:5500`.
- Ajuste as URLs de API para `http://SEU_IP:5206/...`.
- **CORS** no backend já permite `http://127.0.0.1:5500` e `http://localhost:5500`.

---

## Solução de Problemas

- **CORS / preflight com redirect**  
  Certifique-se de estar **apenas em HTTP** no dev (este projeto já está sem `UseHttpsRedirection` e com `launchSettings` só HTTP).

- **EF Tools/Runtime mismatch**  
  Use **EF 8.0.20** para *todos* pacotes EF + `dotnet-ef 8.0.20`.

- **Ollama “embedding":[]**  
  Use o campo `prompt` com `nomic-embed-text`. Exemplo:
  ```bash
  curl -X POST http://localhost:11434/api/embeddings \
    -H "Content-Type: application/json" \
    -d "{ \"model\": \"nomic-embed-text\", \"prompt\": \"texto para embed\" }"
  ```

- **Sem IA respondendo / Lento**  
  Verifique se o **Ollama** está rodando (`ollama list`, `ollama ps`) e se os modelos foram **pull**.

---

### Contato

Para dúvidas ou suporte:  
**gabriel.lira@uvv.net.com.br**  
**maria.estevanovic@uvv.net.com.br**  
**samuel.dasilva@uvv.net.com.br**
