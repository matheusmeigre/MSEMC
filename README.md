# MSEMC — Microserviço de Envio de Mensagens aos Clientes

> API REST para envio assíncrono de e-mails, desenvolvida em C# com .NET 8. Projetada com foco em resiliência, observabilidade e segurança para ambientes corporativos.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED)](https://www.docker.com)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600)](https://masstransit.io)
[![Deploy](https://img.shields.io/badge/Deploy-Railway-0B0D0E)](https://railway.app)

---

## Sumário

- [Sobre o Projeto](#sobre-o-projeto)
- [Funcionalidades](#funcionalidades)
- [Tecnologias](#tecnologias)
- [Arquitetura](#arquitetura)
- [Endpoints](#endpoints)
- [Configuração](#configuração)
- [Executando o Projeto](#executando-o-projeto)
- [Pipeline CI/CD](#pipeline-cicd)
- [Licença](#licença)

---

## Sobre o Projeto

O **MSEMC** é uma API REST que recebe solicitações de envio de e-mail, valida e publica na fila do RabbitMQ para processamento assíncrono. Um consumer dedicado consome a fila e realiza o envio via SMTP (MailKit). O resultado é publicado como evento (`EmailSentEvent` ou `EmailFailedEvent`).

Pode ser integrado a qualquer sistema que precise de envio de e-mail desacoplado — notificações, alertas, confirmações de pedido, etc.

---

## Funcionalidades

| Funcionalidade | Status |
|---|---|
| Envio assíncrono via RabbitMQ (MassTransit) | ✅ |
| Fallback InMemory (sem RabbitMQ em dev) | ✅ |
| Envio SMTP com MailKit | ✅ |
| Suporte a HTML, CC e BCC | ✅ |
| Autenticação por API Key (`X-API-Key`) | ✅ |
| Rate Limiting (Fixed Window) | ✅ |
| Validação com FluentValidation | ✅ |
| Resiliência com Polly v8 (retry + circuit breaker + timeout) | ✅ |
| Logs estruturados com Serilog | ✅ |
| Métricas personalizadas com OpenTelemetry | ✅ |
| Health Check SMTP | ✅ |
| Documentação Swagger com API Key | ✅ |
| Containerização com Docker | ✅ |
| Deploy na Railway | ✅ |

---

## Tecnologias

| Tecnologia | Uso |
|---|---|
| .NET 8 / ASP.NET Core (Minimal APIs) | Framework principal |
| MailKit 4.12 | Envio SMTP |
| MassTransit 8.4 + RabbitMQ | Mensageria assíncrona |
| Polly v8 | Resiliência (retry, circuit breaker, timeout) |
| FluentValidation 11 | Validação de contratos |
| Serilog | Logs estruturados |
| OpenTelemetry (System.Diagnostics) | Métricas e rastreamento |
| Swashbuckle 6.6 | Swagger / OpenAPI |
| Docker + Docker Compose | Containerização |
| GitHub Actions | Pipeline CI/CD |
| Railway | Hospedagem em nuvem |

---

## Arquitetura

```
Cliente HTTP
    │
    ▼
[ API Key Auth ] → 401 se inválida
    │
[ Rate Limiter ] → 429 se excedido
    │
[ FluentValidation ] → 400 se inválido
    │
    ▼
POST /api/messages
    │
    ▼
MassTransitEmailPublisher ──► RabbitMQ (fila)
                                    │
                                    ▼
                            SendEmailConsumer
                                    │
                            MailKitEmailSender
                            (Polly: retry + CB)
                                    │
                        ┌───────────┴───────────┐
                        ▼                       ▼
                EmailSentEvent          EmailFailedEvent
```

### Estrutura de pastas

```
Abstractions/       Interfaces (IEmailSender, IEmailQueuePublisher)
Configuration/      Options Pattern (SmtpOptions, ApiKeyOptions, RabbitMqOptions...)
Contracts/          DTOs de request e response
Domain/             Entidades, enums e Result pattern
Endpoints/          Minimal API endpoints
Infrastructure/     MailKit, HealthChecks, Polly, OpenTelemetry
Messaging/          Commands, Events, Consumers, Publishers (MassTransit)
Middleware/         GlobalExceptionHandler, RequestLoggingMiddleware
Security/           ApiKeyAuthenticationHandler
Validators/         FluentValidation
tests/              Testes unitários (xUnit + NSubstitute + FluentAssertions)
```

---

## Endpoints

### `POST /api/messages`

Enfileira um e-mail para envio assíncrono. Requer autenticação via API Key.

**Headers:**
```
X-API-Key: sua-chave-api
Content-Type: application/json
```

**Request Body:**
```json
{
  "recipient": "destinatario@email.com",
  "subject": "Assunto do e-mail",
  "body": "<h1>Conteúdo HTML</h1>",
  "isHtml": true,
  "ccRecipients": ["copia@email.com"],
  "bccRecipients": ["oculto@email.com"]
}
```

**Respostas:**

| HTTP | Descrição |
|---|---|
| `202 Accepted` | Mensagem enfileirada com sucesso |
| `400 Bad Request` | Validação falhou (detalhes no body) |
| `401 Unauthorized` | API Key ausente ou inválida |
| `429 Too Many Requests` | Rate limit excedido |

**Response 202:**
```json
{
  "messageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Queued",
  "acceptedAt": "2026-04-15T18:00:00Z"
}
```

### `GET /health`

Liveness check — retorna `200` se a aplicação está rodando.

### `GET /health/ready`

Readiness check — valida a conectividade SMTP.

---

## Configuração

Credenciais **nunca devem ser versionadas**. Use variáveis de ambiente ou `.env` local.

### Variáveis de ambiente

| Variável | Descrição |
|---|---|
| `Smtp__Host` | Host SMTP (ex: `smtp.gmail.com`) |
| `Smtp__Port` | Porta SMTP (ex: `587`) |
| `Smtp__Username` | Usuário/email SMTP |
| `Smtp__Password` | App Password do Gmail |
| `Smtp__SenderEmail` | E-mail remetente |
| `Smtp__EnableSsl` | `true` ou `false` |
| `ApiKey__Key` | Chave secreta para autenticação |
| `RabbitMq__Host` | Host do RabbitMQ |
| `RabbitMq__Port` | Porta AMQP (padrão: `5672`) |
| `RabbitMq__Username` | Usuário RabbitMQ |
| `RabbitMq__Password` | Senha RabbitMQ |

### Gerar uma API Key segura

```powershell
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

---

## Executando o Projeto

### Com Docker (recomendado)

Crie um arquivo `.env` na raiz do projeto:

```env
SMTP_USERNAME=seu@gmail.com
SMTP_PASSWORD=sua-app-password
SMTP_SENDER_EMAIL=seu@gmail.com
API_KEY=sua-chave-gerada
```

Suba os containers:

```bash
docker-compose up --build
```

Acesse o Swagger em: `http://localhost:8080/swagger`

> O RabbitMQ Management UI estará disponível em `http://localhost:15672` (usuário: `guest`, senha: `guest`).

### Sem Docker (dotnet run)

Configure o `appsettings.Development.json` com suas credenciais locais (não commitar):

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "seu@gmail.com",
    "Password": "sua-app-password",
    "EnableSsl": true,
    "SenderEmail": "seu@gmail.com"
  },
  "ApiKey": {
    "Key": "dev-api-key"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```

```bash
dotnet restore
dotnet run
```

> Se o RabbitMQ não estiver disponível, a aplicação usa automaticamente o transporte **InMemory** como fallback.

---

## Pipeline CI/CD

O arquivo [.github/workflows/deploy.yml](.github/workflows/deploy.yml) executa a cada push na branch `master`:

1. **Security Audit** — verifica dependências com vulnerabilidades conhecidas
2. **Cache NuGet** — acelera restaurações
3. **Build** — compila com analyzers habilitados
4. **Tests** — executa os testes unitários
5. **Publish** — gera o artefato de produção

As variáveis sensíveis são injetadas via **GitHub Secrets**:

| Secret | Variável de ambiente |
|---|---|
| `SMTP_USERNAME` | `Smtp__Username` |
| `SMTP_PASSWORD` | `Smtp__Password` |
| `SMTP_SENDER_EMAIL` | `Smtp__SenderEmail` |
| `API_KEY` | `ApiKey__Key` |
| `RABBITMQ_HOST` | `RabbitMq__Host` |
| `RABBITMQ_USERNAME` | `RabbitMq__Username` |
| `RABBITMQ_PASSWORD` | `RabbitMq__Password` |

---

## Licença

Este projeto está licenciado sob a licença **MIT**. Consulte o arquivo [LICENSE](./LICENSE) para mais informações.

---

<p align="center">Desenvolvido por Matheus Meigre · C# · .NET 8</p>