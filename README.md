# Desafio Backend PB - Sistema de Proposta de Cr�dito

## Introdu��o

Este projeto implementa a solu��o para o desafio de desenvolvimento backend proposto pelo PB.  O objetivo � avaliar habilidades t�cnicas na cria��o de um sistema de cadastro de clientes, gera��o de proposta de cr�dito e emiss�o de cart�es, utilizando microsservi�os e comunica��o ass�ncrona.

## Arquitetura

A solu��o foi desenvolvida utilizando uma arquitetura de microsservi�os para promover desacoplamento, escalabilidade e manutenibilidade. Os servi�os principais s�o:

1.   **CadastroClientes.Api:** Respons�vel por receber requisi��es REST para cadastro de novos clientes, validar dados iniciais e publicar um evento `cliente.criado`.
2.   **PropostaCredito.Worker:** Consome o evento `cliente.criado`, aplica as regras de score de cr�dito, persiste a proposta e publica eventos de resultado (`proposta.aprovada` ou `proposta.reprovada`).
3.   **CartaoCredito.Worker:** Consome o evento `proposta.aprovada`, gera os dados do(s) cart�o(�es) de cr�dito conforme as regras de limite e quantidade, e persiste os cart�es gerados.

 A comunica��o entre os servi�os � feita de forma ass�ncrona utilizando o **RabbitMQ** como message broker. Isso garante que os servi�os operem de forma independente e que falhas em um servi�o n�o impactem diretamente os outros.  Foi implementado um mecanismo de resili�ncia usando Dead Letter Queues (DLQs) para tratar mensagens que falham no processamento.

Cada microsservi�o segue os princ�pios da **Clean Architecture**, separando as responsabilidades em camadas:
* **Domain:** Cont�m as entidades e regras de neg�cio principais.
* **Application:** Orquestra os casos de uso, define interfaces (contratos) e DTOs.
* **Infrastructure:** Implementa os contratos da camada de aplica��o (reposit�rios, message bus) e lida com detalhes t�cnicos (acesso a dados, bibliotecas externas).
* **Api/Worker:** Ponto de entrada do servi�o (API REST ou Worker Service para consumo de mensagens).

## Tecnologias Utilizadas

*  **.NET 8.0:** Plataforma de desenvolvimento para os microsservi�os.
* **ASP.NET Core:** Para a constru��o da API REST (`CadastroClientes.Api`).
* **Worker Service:** Template .NET para os consumidores de mensagens (`PropostaCredito.Worker`, `CartaoCredito.Worker`).
*  **RabbitMQ:** Message Broker para comunica��o ass�ncrona entre os servi�os.
*  **xUnit:** Framework para testes de unidade.
* **Moq:** Biblioteca para cria��o de Mocks nos testes de unidade.
* **FluentAssertions:** Biblioteca para asser��es mais leg�veis nos testes.
* **Polly:** Biblioteca para implementa��o de pol�ticas de resili�ncia (Retry na conex�o com RabbitMQ).
* **(Opcional) Docker:** Recomendado para executar o RabbitMQ localmente.

## Como Executar Localmente

### Pr�-requisitos

* .NET 8 SDK instalado.
* Docker Desktop instalado (recomendado para o RabbitMQ).

### Configura��o

1.  **Clonar o Reposit�rio:**
    ```bash
    git clone https://github.com/BrayanSMS/PBCase.git
    cd PBEsfio
    ```
2.  **Iniciar RabbitMQ via Docker:**
    ```bash
    docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
    ```
    * A interface de gerenciamento estar� acess�vel em `http://localhost:15672` (usu�rio: `guest`, senha: `guest`).
3.  **Verificar Configura��es:**
    * Confirme se o `Hostname` do RabbitMQ nos arquivos `appsettings.json` de `CadastroClientes.Api`, `PropostaCredito.Worker` e `CartaoCredito.Worker` est� correto (o padr�o `localhost` deve funcionar com o comando Docker acima).

### Execu��o

**Via Visual Studio:**

1.  Abra a solu��o `PBEsfio.sln`.
2.  Clique com o bot�o direito na Solu��o no Solution Explorer e selecione "Set Startup Projects...".
3.  Escolha a op��o "Multiple startup projects".
4.  Defina a "Action" como "Start" para os seguintes projetos:
    * `CadastroClientes.Api`
    * `PropostaCredito.Worker`
    * `CartaoCredito.Worker`
5.  Clique em OK.
6.  Pressione F5 ou clique no bot�o "Start" para iniciar os tr�s servi�os.

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
3.  **Terminal 3 (Worker Cart�o):**
    ```bash
    cd src/CartaoCredito.Worker
    dotnet run
    ```

### Testando o Fluxo

1.  Aguarde os tr�s servi�os iniciarem (verifique os logs nos terminais/console).
2.  Acesse a interface Swagger da API de Cadastro (o endere�o ser� informado no log do `CadastroClientes.Api`, geralmente algo como `http://localhost:5xxx` ou `https://localhost:7xxx`).
3.  Utilize o endpoint `POST /api/v1/clientes` para cadastrar um novo cliente. Use CPFs com finais diferentes para testar os diferentes fluxos de aprova��o/reprova��o (conforme l�gica de score implementada).
4.  Observe os logs dos workers `PropostaCredito.Worker` e `CartaoCredito.Worker` para acompanhar o processamento das mensagens.
5.  Verifique a interface do RabbitMQ (`http://localhost:15672`) para ver as filas (`proposta.analisar`, `cartao.emitir`, e as DLQs correspondentes) sendo criadas e as mensagens fluindo.

## Decis�es de Design

* **Clean Architecture:** Escolhida para promover baixo acoplamento, alta coes�o e testabilidade, separando claramente as responsabilidades.
* **Microsservi�os:** A separa��o em servi�os menores permite implanta��o e escalonamento independentes, al�m de resili�ncia.
* **Mensageria Ass�ncrona (RabbitMQ):** Garante o desacoplamento entre os servi�os. A falha no processamento de uma proposta ou cart�o n�o impede o cadastro de novos clientes.
*  **Resili�ncia (DLQ):** Mensagens que falham repetidamente ou encontram erros inesperados s�o direcionadas para Dead Letter Queues, permitindo an�lise posterior sem perda de dados e sem bloquear o fluxo principal.
* **Reposit�rios em Mem�ria:** Utilizados para simplificar o setup local e focar na l�gica principal do desafio. Em um ambiente de produ��o, seriam substitu�dos por implementa��es conectadas a bancos de dados persistentes (SQL Server, PostgreSQL, etc.).
* **Worker Services:** Template adequado para consumidores de longa dura��o que escutam filas de mensagens.


## Fluxograma

```mermaid
graph TD
    subgraph "Fluxo Principal"
        A["Usu�rio/Sistema Externo"] -->|"1.POST /api/v1/clientes"| B("Microsservi�o: Cadastro Clientes API");
        B -->|"2.Salva Cliente (Status: EmAnalise)"| DB1[("DB Cadastro")];
        B -->|"3.Publica 'cliente.criado'"| C["RabbitMQ: clientes_exchange"];
        C -->|"routingKey='cliente.criado'"| D["Fila: proposta.analisar"];
        E("Microsservi�o: Proposta Cr�dito Worker") -->|"4.Consome msg"| D;
        E -->|"5.Calcula Score & Regras"| E;
        E -->|"6.Salva/Atualiza Proposta"| DB2[("DB Proposta")];
        E -->|"7a. Se Aprovada"| F["RabbitMQ: clientes_exchange"];
        E -->|"7b. Se Reprovada"| G["RabbitMQ: clientes_exchange"];
        F -->|"routingKey='proposta.aprovada'"| H["Fila: cartao.emitir"];
        I("Microsservi�o: Cart�o Cr�dito Worker") -->|"8.Consome msg"| H;
        I -->|"9.Gera Cart�o(�es)"| I;
        I -->|"10.Salva Cart�o(�es)"| DB3[("DB Cart�o")];
        G -->|"routingKey='proposta.reprovada'"| J["Fila: notificacao.proposta.reprovada (Exemplo)"];
        K["Ex: Servi�o Notifica��o"] -->|"Consome msg"| J;
        I -->|"11.(Opcional) Publica 'cartao.emitido'"| L["RabbitMQ: clientes_exchange"];
        L -->|"routingKey='cartao.emitido'"| M["Fila: notificacao.cartao.emitido (Exemplo)"];
    end

    subgraph "Resili�ncia / Tratamento de Erros"
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
