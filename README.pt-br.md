# **NetBuff - Sistema de Multiplayer da Buff Buff** <sup>1.0<sup> 

<p align="center">
  <a href="https://buff-buff-studios.itch.io">
    <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/74449a77-3e4c-4ee7-9998-46708cbba555" width="400" alt="NetBuff Logo">
  </a>
</p>

### Sobre
Optamos por implementar nosso próprio sistema multiplayer que se adapta melhor ao nosso projeto, sem o overhead gigante que soluções de ONG (Netcode for GameObjects) e de terceiros (como mirror) comumente possuem, ao mesmo tempo em que fornece todas as ferramentas necessárias para criar um ambiente bastante performático. e plataforma de rede confiável.

Este documento foi criado com o propósito de ser um guia de implementação e também um “procedimento de garantia de confiabilidade”. Todos que estão tentando implementar qualquer novo sistema em um projeto usando esta biblioteca devem seguir as instruções encontradas ao longo do guia (ignorando algumas exceções especiais)

O sistema oferece muitos recursos (alguns dos quais são muito dignos de nota, por isso estão listados abaixo):

- **Reload-proof** (você pode fazer alterações de código não agressivas durante a execução e o estado do servidor será mantido. Os clientes precisarão se juntar novamente sem problemas)
- **Reliable and Fast** (você pode escolher quando a confiabilidade é realmente necessária)
- **Small Overhead** (o que você quer/faz é o que você obtém)
- **Packet Based (Status e Ações)** (sem comandos ou chamadas Rpc, você pode usar os pacotes diretamente, oferecendo mais controle e desempenho/configuração)
- **Reconnect Friendly** (pode sincronizar estados retroativos facilmente sem problemas)
- **Data Lightweight** (tenta salvar dados para dispositivos móveis)


## **Componentes Principais**

### **Network Manager**

Componente principal da rede, gerencia a conexão do cliente e/ou servidor, envio/manipulação de pacotes e estados dos objetos de rede na rede. Pode ser estendido para implementar comportamentos específicos personalizados. Você pode acessar a instância atual do Network Manager usando:
```js
var manager = NetworkManager.Instance;

manager.IsServerRunning //Returns if there's a server running locally
manager.IsClientRunning //Returns if there's a client running locally
manager.ClientId //local client id (If the client is running)
```
