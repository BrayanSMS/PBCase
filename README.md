# Desafio Backend PB - Sistema de Proposta de Crédito

## Introdução

Este projeto implementa a solução para o desafio de desenvolvimento backend proposto pelo PB.  O objetivo é avaliar habilidades técnicas na criação de um sistema de cadastro de clientes, geração de proposta de crédito e emissão de cartões, utilizando microsserviços e comunicação assíncrona.

## Arquitetura

A solução foi desenvolvida utilizando uma arquitetura de microsserviços para promover desacoplamento, escalabilidade e manutenibilidade. Os serviços principais são:

1.   **CadastroClientes.Api:** Responsável por receber requisições REST para cadastro de novos clientes, validar dados iniciais e publicar um evento `cliente.criado`.
2.   **PropostaCredito.Worker:** Consome o evento `cliente.criado`, aplica as regras de score de crédito, persiste a proposta e publica eventos de resultado (`proposta.aprovada` ou `proposta.reprovada`).
3.   **CartaoCredito.Worker:** Consome o evento `proposta.aprovada`, gera os dados do(s) cartão(ões) de crédito conforme as regras de limite e quantidade, e persiste os cartões gerados.

 A comunicação entre os serviços é feita de forma assíncrona utilizando o **RabbitMQ** como message broker. Isso garante que os serviços operem de forma independente e que falhas em um serviço não impactem diretamente os outros.  Foi implementado um mecanismo de resiliência usando Dead Letter Queues (DLQs) para tratar mensagens que falham no processamento.

Cada microsserviço segue os princípios da **Clean Architecture**, separando as responsabilidades em camadas:
* **Domain:** Contém as entidades e regras de negócio principais.
* **Application:** Orquestra os casos de uso, define interfaces (contratos) e DTOs.
* **Infrastructure:** Implementa os contratos da camada de aplicação (repositórios, message bus) e lida com detalhes técnicos (acesso a dados, bibliotecas externas).
* **Api/Worker:** Ponto de entrada do serviço (API REST ou Worker Service para consumo de mensagens).

## Tecnologias Utilizadas

*  **.NET 8.0:** Plataforma de desenvolvimento para os microsserviços.
* **ASP.NET Core:** Para a construção da API REST (`CadastroClientes.Api`).
* **Worker Service:** Template .NET para os consumidores de mensagens (`PropostaCredito.Worker`, `CartaoCredito.Worker`).
*  **RabbitMQ:** Message Broker para comunicação assíncrona entre os serviços.
*  **xUnit:** Framework para testes de unidade.
* **Moq:** Biblioteca para criação de Mocks nos testes de unidade.
* **FluentAssertions:** Biblioteca para asserções mais legíveis nos testes.
* **Polly:** Biblioteca para implementação de políticas de resiliência (Retry na conexão com RabbitMQ).
* **(Opcional) Docker:** Recomendado para executar o RabbitMQ localmente.

## Como Executar Localmente

### Pré-requisitos

* .NET 8 SDK instalado.
* Docker Desktop instalado (recomendado para o RabbitMQ).

### Configuração

1.  **Clonar o Repositório:**
    ```bash
    git clone https://github.com/BrayanSMS/PBCase.git
    cd PBEsfio
    ```
2.  **Iniciar RabbitMQ via Docker:**
    ```bash
    docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
    ```
    * A interface de gerenciamento estará acessível em `http://localhost:15672` (usuário: `guest`, senha: `guest`).
3.  **Verificar Configurações:**
    * Confirme se o `Hostname` do RabbitMQ nos arquivos `appsettings.json` de `CadastroClientes.Api`, `PropostaCredito.Worker` e `CartaoCredito.Worker` está correto (o padrão `localhost` deve funcionar com o comando Docker acima).

### Execução

**Via Visual Studio:**

1.  Abra a solução `PBEsfio.sln`.
2.  Clique com o botão direito na Solução no Solution Explorer e selecione "Set Startup Projects...".
3.  Escolha a opção "Multiple startup projects".
4.  Defina a "Action" como "Start" para os seguintes projetos:
    * `CadastroClientes.Api`
    * `PropostaCredito.Worker`
    * `CartaoCredito.Worker`
5.  Clique em OK.
6.  Pressione F5 ou clique no botão "Start" para iniciar os três serviços.

**Via Linha de Comando (abrir 3 terminais diferentes):**

1.  **Terminal 1 (API Cadastro):**
    ```bash
    cd src/CadastroClientes.Api
    dotnet run
    ```
2.  **Terminal 2 (Worker Proposta):**
    ```bash
    cd src/PropostaCredito.Worker
    dotnet run
    ```
3.  **Terminal 3 (Worker Cartão):**
    ```bash
    cd src/CartaoCredito.Worker
    dotnet run
    ```

### Testando o Fluxo

1.  Aguarde os três serviços iniciarem (verifique os logs nos terminais/console).
2.  Acesse a interface Swagger da API de Cadastro (o endereço será informado no log do `CadastroClientes.Api`, geralmente algo como `http://localhost:5xxx` ou `https://localhost:7xxx`).
3.  Utilize o endpoint `POST /api/v1/clientes` para cadastrar um novo cliente. Use CPFs com finais diferentes para testar os diferentes fluxos de aprovação/reprovação (conforme lógica de score implementada).
4.  Observe os logs dos workers `PropostaCredito.Worker` e `CartaoCredito.Worker` para acompanhar o processamento das mensagens.
5.  Verifique a interface do RabbitMQ (`http://localhost:15672`) para ver as filas (`proposta.analisar`, `cartao.emitir`, e as DLQs correspondentes) sendo criadas e as mensagens fluindo.

## Decisões de Design

* **Clean Architecture:** Escolhida para promover baixo acoplamento, alta coesão e testabilidade, separando claramente as responsabilidades.
* **Microsserviços:** A separação em serviços menores permite implantação e escalonamento independentes, além de resiliência.
* **Mensageria Assíncrona (RabbitMQ):** Garante o desacoplamento entre os serviços. A falha no processamento de uma proposta ou cartão não impede o cadastro de novos clientes.
*  **Resiliência (DLQ):** Mensagens que falham repetidamente ou encontram erros inesperados são direcionadas para Dead Letter Queues, permitindo análise posterior sem perda de dados e sem bloquear o fluxo principal.
* **Repositórios em Memória:** Utilizados para simplificar o setup local e focar na lógica principal do desafio. Em um ambiente de produção, seriam substituídos por implementações conectadas a bancos de dados persistentes (SQL Server, PostgreSQL, etc.).
* **Worker Services:** Template adequado para consumidores de longa duração que escutam filas de mensagens.


## Fluxograma

```mermaid
graph TD
    subgraph "Fluxo Principal"
        A["Usuário/Sistema Externo"] -->|"1.POST /api/v1/clientes"| B("Microsserviço: Cadastro Clientes API");
        B -->|"2.Salva Cliente (Status: EmAnalise)"| DB1[("DB Cadastro")];
        B -->|"3.Publica 'cliente.criado'"| C["RabbitMQ: clientes_exchange"];
        C -->|"routingKey='cliente.criado'"| D["Fila: proposta.analisar"];
        E("Microsserviço: Proposta Crédito Worker") -->|"4.Consome msg"| D;
        E -->|"5.Calcula Score & Regras"| E;
        E -->|"6.Salva/Atualiza Proposta"| DB2[("DB Proposta")];
        E -->|"7a. Se Aprovada"| F["RabbitMQ: clientes_exchange"];
        E -->|"7b. Se Reprovada"| G["RabbitMQ: clientes_exchange"];
        F -->|"routingKey='proposta.aprovada'"| H["Fila: cartao.emitir"];
        I("Microsserviço: Cartão Crédito Worker") -->|"8.Consome msg"| H;
        I -->|"9.Gera Cartão(ões)"| I;
        I -->|"10.Salva Cartão(ões)"| DB3[("DB Cartão")];
        G -->|"routingKey='proposta.reprovada'"| J["Fila: notificacao.proposta.reprovada (Exemplo)"];
        K["Ex: Serviço Notificação"] -->|"Consome msg"| J;
        I -->|"11.(Opcional) Publica 'cartao.emitido'"| L["RabbitMQ: clientes_exchange"];
        L -->|"routingKey='cartao.emitido'"| M["Fila: notificacao.cartao.emitido (Exemplo)"];
    end

    subgraph "Resiliência / Tratamento de Erros"
        D -->|"Falha no Consumo (NACK)"| DLQ1_Ex["RabbitMQ: clientes_exchange.dlx"];
        DLQ1_Ex -->|"routingKey='cliente.criado.dlq'"| DLQ1["Fila DLQ: proposta.analisar.dlq"];
        H -->|"Falha no Consumo (NACK)"| DLQ2_Ex["RabbitMQ: clientes_exchange.dlx"];
        DLQ2_Ex -->|"routingKey='proposta.aprovada.dlq'"| DLQ2["Fila DLQ: cartao.emitir.dlq"];
        DLQ1 -->|"Monitoramento/Reprocessamento"| ADM(["Admin/Monitor"]);
        DLQ2 -->|"Monitoramento/Reprocessamento"| ADM;
    end

    classDef microservice fill:#f9f,stroke:#333,stroke-width:2px;
    classDef queue fill:#ccf,stroke:#333,stroke-width:1px;
    classDef exchange fill:#ff9,stroke:#333,stroke-width:1px;
    classDef database fill:#ccf,stroke:#333,stroke-width:1px;
    classDef dlq fill:#fcc,stroke:#f00,stroke-width:1px;
    class B,E,I,K microservice;
    class C,F,G,L,DLQ1_Ex,DLQ2_Ex exchange;
    class D,H,J,M queue;
    class DB1,DB2,DB3 database;
    class DLQ1,DLQ2 dlq;
