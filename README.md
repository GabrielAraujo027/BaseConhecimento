# Unitá Soluções Digitais - Sistema de Chamados TI

Este projeto implementa um **sistema de chamados corporativo de suporte TI** para a Unitá Soluções Digitais, utilizando **C# com .NET 8** no backend e **Ollama** como motor de IA para suporte automatizado, junto com um **frontend customizado** para exibição e interação com os chamados.

O sistema auxilia colaboradores em questões de **TI, Facilities, Financeiro** e outras áreas, permitindo abertura, acompanhamento e encaminhamento de chamados, além de integração com atendimento humano quando necessário.

---

## Tecnologias Utilizadas

- **Frontend:** HTML5, CSS3, JavaScript  
- **Backend:** C# com .NET 8  
- **Bot/IA:** Ollama (via API de IA)  
- **Estilização:** Layout customizado inspirado na identidade visual da Unitá  

---

## Funcionalidades

### Gestão de Chamados
- Listagem completa de chamados com filtros por ID, setor, assunto, solicitante, data e status.  
- Exibição do status de chamados em painel lateral (Pendente, Em andamento, Concluído, Cancelado).  
- Visualização detalhada de cada chamado ao clicar na tabela.  
- Histórico do chamado atualizado automaticamente ao abrir detalhes.  
- Ações disponíveis:
  - **Encaminhar** o chamado para outro setor (mantendo status Pendente).  
  - **Encerrar** o chamado (status Concluído).  
  - **Cancelar** o chamado (status Cancelado).  
- Auto-scroll e atualização dinâmica da tabela e painel de status.  
- Filtros interativos para localizar chamados rapidamente.  

### Chat com IA
- Interface de chat para comunicação direta com o motor de IA Ollama.  
- Envio de mensagens do usuário e exibição de respostas do assistente IA em tempo real.  
- Histórico da conversa mantido durante a sessão.  
- Suporte a envio via **Enter** ou botão **Enviar**.  
- Layout intuitivo com distinção visual entre mensagens do usuário e respostas da IA.  
- Possibilidade de auxiliar na abertura de novos chamados ou fornecer suporte rápido sem intervenção humana.  

---
---

## Estrutura do Projeto

/BaseConhecimento
│
├─ /Back # Backend C# (.NET 8)
├─ /Front # Frontend HTML, CSS e JS
├─ README.md # Documentação do projeto
└─ LICENSE.txt # Licença do projeto


---

## Como Executar

1. Abra o arquivo `listagemChamados.html` no Visual Studio Code.  
2. Utilize o **Live Server** para rodar o frontend localmente.  
3. Configure a integração com a API do backend C# e a API da Ollama para funcionamento completo.  

---

## Contato

Para dúvidas ou suporte:  
**gabriel.lira@uvv.net.com.br**  
**maria.estevanovic@uvv.net.com.br**  
**samuel.dasilva@uvv.net.com.br**
