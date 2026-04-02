# 📧 MSEMC — Microserviço para Envio de Mensagens aos Clientes

> Microserviço de envio de e-mails desenvolvido em C# com .NET 8, projetado para ser simples, escalável e pronto para ambientes corporativos.

---

## 📋 Sumário

- [Sobre o Projeto](#sobre-o-projeto)
- [Funcionalidades](#funcionalidades)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Arquitetura](#arquitetura)
- [Endpoints](#endpoints)
- [Configuração e Variáveis de Ambiente](#configuração-e-variáveis-de-ambiente)
- [Executando o Projeto](#executando-o-projeto)
- [Pipeline CI/CD](#pipeline-cicd)
- [Roadmap](#roadmap)
- [Contribuição](#contribuição)
- [Licença](#licença)

---

## 📌 Sobre o Projeto

O **MSEMC** é uma API REST corporativa responsável pelo envio de e-mails via protocolo SMTP. Desenvolvida seguindo boas práticas de engenharia de software, pode ser integrada a qualquer sistema ou plataforma que necessite de comunicação por e-mail de forma confiável e segura.

---

## 🎯 Funcionalidades

| Funcionalidade                        | Status        |
|---------------------------------------|---------------|
| Envio de e-mails via SMTP             | ✅ Disponível  |
| Suporte a conteúdo HTML               | ✅ Disponível  |
| Documentação interativa com Swagger   | ✅ Disponível  |
| Suporte a múltiplos destinatários     | 🔄 Em progresso |
| Suporte a anexos                      | 🔄 Em progresso |
| Autenticação JWT                      | 🗓️ Planejado   |
| Rate Limiting                         | 🗓️ Planejado   |
| Fila de processamento (RabbitMQ)      | 🗓️ Planejado   |
| Templates de e-mail dinâmicos         | 🗓️ Planejado   |

---

## 🧰 Tecnologias Utilizadas

| Tecnologia              | Versão / Descrição              |
|-------------------------|---------------------------------|
| .NET                    | 8.0                             |
| ASP.NET Core Web API    | REST API Framework              |
| Swashbuckle (Swagger)   | Documentação interativa         |
| SMTP                    | Gmail e outros provedores       |
| GitHub Actions          | Pipeline CI/CD                  |

---

## 🏗️ Arquitetura

O projeto segue uma estrutura baseada em separação de responsabilidades, orientada aos princípios **SOLID**:

---

## 📬 Endpoints

### `POST /api/email`

Realiza o envio de um e-mail.

**Request Body:**

```json
{
  "to": "destinatario@email.com",
  "subject": "Assunto do email",
  "body": "<h1>Conteúdo HTML</h1>"
}
```

**Respostas:**

| Código HTTP | Descrição                          |
|-------------|------------------------------------|
| `200 OK`    | E-mail enviado com sucesso         |
| `400 Bad Request` | Dados inválidos ou ausentes  |
| `500 Internal Server Error` | Falha no envio      |

> 📖 Acesse a documentação completa em `/swagger` após iniciar a aplicação.

---

## 🔐 Configuração e Variáveis de Ambiente

As credenciais sensíveis **não devem ser versionadas**. Utilize variáveis de ambiente ou Secrets pelo seu provedor de CI/CD.

| Variável                  | Descrição                        |
|---------------------------|----------------------------------|
| `EmailSettings__Email`    | Endereço de e-mail remetente     |
| `EmailSettings__Password` | Senha ou App Password do remetente |

**Exemplo de configuração local (`appsettings.Development.json`):**

```json
{
  "EmailSettings": {
    "Email": "seu_email@gmail.com",
    "Password": "sua_senha_ou_app_password"
  }
}
```

> ⚠️ **Atenção:** Nunca exponha credenciais em repositórios públicos. Utilize **GitHub Secrets** em ambientes de CI/CD.

---

## ▶️ Executando o Projeto

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Conta SMTP configurada (ex: Gmail com App Password)

### Passos

```bash
# Restaurar dependências
dotnet restore

# Executar o projeto
dotnet run
```

Acesse a documentação Swagger em:

```
https://localhost:5001/swagger
```

---

## 🔄 Pipeline CI/CD

Este projeto utiliza **GitHub Actions** para automação do ciclo de vida de desenvolvimento:

- **Build automatizado:** Gera e testa o projeto em diferentes ambientes.
- **Testes:** Executa testes automatizados para garantir a integridade do código.
- **Verificação de vulnerabilidades:** Scans de segurança em dependências e no código.
- **Preparação para deploy:** Pacote do aplicativo preparado para implantação.

---

## 📈 Roadmap

### 🔧 Curto Prazo
- [ ] Aplicar princípios SOLID no `Controller`
- [ ] Melhorar tratamento de exceções e padronizar respostas HTTP
- [ ] Implementar validações robustas nos modelos (Data Annotations / FluentValidation)

### 📦 Médio Prazo
- [ ] Suporte a anexos e múltiplos destinatários (CC/BCC)
- [ ] Integração com **Serilog** para logs estruturados
- [ ] Implementação de observabilidade e rastreamento distribuído

### 🚀 Longo Prazo
- [ ] Autenticação e autorização via **JWT**
- [ ] Rate limiting para proteção contra abusos
- [ ] Fila de processamento assíncrono com **RabbitMQ**
- [ ] Templates de e-mail dinâmicos
- [ ] Deploy automatizado em ambiente cloud (Azure / AWS)

---

## 🤝 Contribuição

Contribuições são bem-vindas! Para contribuir com este projeto:

1. Faça um fork do repositório
2. Crie uma branch para sua feature: `git checkout -b feature/minha-feature`
3. Realize suas alterações e faça o commit: `git commit -m 'feat: minha nova feature'`
4. Envie para o seu fork: `git push origin feature/minha-feature`
5. Abra um **Pull Request** detalhando as mudanças

> Por favor, siga as convenções de commit ([Conventional Commits](https://www.conventionalcommits.org/)) e garanta que os testes estejam passando antes de abrir o PR.

---

## 📄 Licença

Este projeto está licenciado sob a licença **MIT**. Consulte o arquivo [LICENSE](./LICENSE) para mais informações.

---

<p align="center">
  Desenvolvido por Matheus Meigre em C# · .NET 8
</p>