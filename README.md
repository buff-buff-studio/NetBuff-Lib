# **NetBuff - A Buff Buff Multiplayer System** <sup>1.0<sup> 

<p align="center">
  <a href="https://buff-buff-studios.itch.io">
    <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/74449a77-3e4c-4ee7-9998-46708cbba555" width="400" alt="NetBuff Logo">
  </a>
</p>

### About
We chose to implement our own multiplayer system that fits better our project, without the giant overhead that NGO (Netcode for GameObjects) and third-party solutions (like mirror) commonly have, while still providing all the tools needed to create a very-performatic and reliable network platform.

This document was created with the purpose of being an implementation guide as also a “reliability assurance procedure”. Everyone who's trying to implement any new system into a project using this library shall follow the instructions found along the guide (ignoring some special exceptions)

The system provides many features (which some are very worthy to note, so they’re listed below):

- **Reload-proof** (you can make non-aggressive code changes runtime and the server state will be kept. Clients will need rejoin with no issues)
- **Reliable and Fast** (you can choose when the reliability is really needed)
- **Small Overhead** (what you want/do is what you get)
- **Packet Based (Status & Actions)** (no Rpc commands or calls, you can use the packets directly, offering more control and performance/configuration)
- **Reconnect Friendly** (can sync retroactive states easily with no problem)
- **Data Lightweight** (tries to save data for mobile devices)


## **Main Components**

### **Network Manager**

Main network component, manages the client and/or server connection, packets sending/handling and network object states across the network. Can be extended to implement custom specific behaviors. You can access the current Network Manager instance using

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/e4dfdd21-1d04-43c3-97a8-448526fbca92">
</p>

### **Network Transport**

Internal interface used in background to handle connections between server and clients (players). Normally you shouldn’t make changes to the transport. The default transport method is **UDP** (User Datagram Protocol), a simple, fast but unreliable protocol. A reliability layer is used to guarantee reliability on the communication.

Currently the following transport methods are supported:

- **UDP (Reliable & Unreliable)**

### **Network Identifier**

Used to sync an object reference across all network-ends. **All objects that need to sync data / state need a network identifier.** This is mainly formed by three fields:

- **Id (32 byte hex number)**: The object unique identifier
- **OwnerId (int):** References the owner of the object. Default is **-1 (Server)**
- **Prefab Id (32 byte hex number):** The object source prefab id (For starting objects, the prefab value is **0000000000000000**)

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/8c819ebe-ae4a-49d7-b21d-f977a6450703">
</p>

These three fields combined provides all the needed information for basic-object handling

### **Network Prefab Registry**

Used to assure that network objects can be instantiated on client-side correctly. All network objects instantiated runtime **shall** be originated by a registered prefab.

### **Network Behaviour**

Base class to create any network related interaction. Any behavior / state - sync normally relies on a network behaviour instance. All network behaviors are mono behaviors so you just need to add them to a GameObject with NetworkIdentity and they will start to behave when connected on a network.

### **Network Transform**

Basic common NetworkBehaviour. Used to sync an object transformation across the network. You can set the update threshold and even which components of Position, Rotation and Scale should be synced **(DO NOT CHANGE SYNC MODE DURING PLAYTIME).** If the tick rate is -1, the defaultTickRate from NetworkManager will be used

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/8e3a6efd-1afc-4169-8d6f-080b408bb43f">
</p>

### **Network Rigidbody Transform**

Extends the NetworkTransform syncing with a better support for physics based objects, syncing the velocity and angularVelocity components as needed. If the sync mode is Position X | Rotation Y, only velocity X component and angular velocity Y component will be synced, saving data. The inspector view is the same as NetworkTransport.

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/d2f24aa9-962d-4060-b9f1-d2fb6c7708b6">
</p>

### **Network Animator**

Syncs animation layer states, parameters, timing and transitions of an Animator across the network. Triggers are not automatically synced if you set them directly on the Animator. Use the NetworkAnimator.SetTrigger (with Authority) method to set them correctly.

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/5bc4b157-6b04-44f1-bcbd-b839e3339bc6">
</p>

## **Basic Structure Diagram**

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/71199ae1-eb67-49eb-8638-0bc138ff0f34">
</p>

(CRUD stands for: Create, Read, Update and Delete)

In 99.99% usage cases you will just need to interact with NetworkManager and the NetworkBehaviour classes. The behaviour base class offers many **events, methods and properties** that may cover almost all use cases.

Sending/Receiving custom packets can (and shall) also be handled by the behaviors, where **each NetworkIdentity receives the callbacks for** only the **packets owned by him**, but don’t worry, **you can add custom packet listeners when needed** (just remember to remove the listener as well on the OnDisable method).

All state/action synchronization may also be done using the events or your own methods inside the behaviors. **Any manager synced across the network (as a LevelManager for example) should be derived from a NetworkBehaviour.**

## **Network Behaviour**

This section will show information of the base class NetworkBehaviour alongside with basic implementation details. As said before, the behaviour class is composed of three main component types: **Event Callbacks, Properties** and **Methods.** You can find a list of all them bellow:

### **Properties**

|**Properties**|||
| - | :- | :- |
|**Side**|**Name**|**Description**|
|Both|**Identity (NetworkIdentity)**|Returns the identity component attached to the network object|
|Both|**HasAuthority (bool)**|Returns if the local environment has authority over the object. When the OwnerId is -1 (Default), HasAuthority will return true only on the server. When the object is owned by a client, it will only be true in the client local environment. **Use this to control input / state handling.**|
|Both|**IsOwnedByAClient (bool)**|Returns if the OwnerId is not -1|
|Both|**Id (NetworkId)**|Returns the Id of the network object|
|Both|**OwnerId (int)**|Returns the owner id. If you want to change the ownership of the object you can use the method **SetOwner** (only works on the server or with authority)|
|Both|**PrefabId (NetworkId)**|Returns the prefabId used to create the object. If the object is a scene object (aka exists before the network starts), the prefab id will be empty (0000000000000000)|


### **Event Callbacks**

|**Event Callbacks**|||
| - | :- | :- |
|**Side**|**Name**|**Description**|
|Server|**OnServerReceivePacket(IOwnedPacket packet, int clientId)**|Called when the object receives an OwnedPacket on the server side. **The packets received are not default broadcast to the clients. You should do it on this callback.** (See the section about packets for more info)|
|Client|**OnClientReceivePacket(IOwnedPacket packet)**|<p>Called when the object receives an OwnedPacket on the client side. (See the section about packets for more info).</p><p>**THIS IS ALSO CALLED ON SERVER (NOT HOST) ENDS**</p>|
|Both|**OnSpawned(bool isRetroactive)**|Called when the object is spawned on the network end (Even the pre existing ones will call this method). **Very useful to load the initial state of an object.**|
|Server|**OnClientConnected(int clientId)**|Called when a client joins the server when the object already exists. **Very useful for retroactive state loading, when the server needs to send data to the client of the actual object state.**|
|Server|**OnClientDisconnected(int clientId)**|Called when a client leaves the server|
|Server|**OnDespawned**|Called when an object is despawned (normally along with OnDestroy)|
|Both|**OnActiveChanged(bool active)**|Called when the object. Works the same as OnEnable / OnDisable but on the network environment.|
|Both|**OnOwnerChanged(int newOwner)**|Called when the ownership of a network object changes|

### **Methods**

|**Methods**|||
| - | :- | :- |
|**Side**|**Name**|**Description**|
|Server|**ServerBroadcastPacket(IPacket packet, bool reliable = false)**|<p>Broadcast a packet to all the clients. You can choose if the packet should be reliable.</p><p>If it’s a constant state update, where a packet loss is not an issue, you may set it as false for performance and low-data usage</p>|
|Server|**ServerBroadcastPacketExceptFor(IPac ket packet, int except, bool reliable = false)**|Broadcast a packet to all the clients except one. Useful when spreading a packet received from a client.|
|Server|**ServerSendPacket(IPacket packet, int clientId, bool reliable = false)**|Sends a packet a client|
|Client|**ClientSendPacket(IPacket packet, bool reliable = false)**|Sends a pocket to the server|
|Auth|**SendPacket(IPacket packet, bool reliable = false)**|Sends a packet to the other network end. Automatically changes based if the owner is a client or the server.|
|Both|**GetPacketListener<T>()**|Get a packet listener to register your own listeners. Useful to handle non-owned packets|
|Auth|**Despawn()**|Despawns (a.k.a. destroys) a network object across the network.|
|Auth|**SetActive(bool active)**|Sets if the object is active across the network|
|Auth|**SetOwner(int clientId)**|Sets the owner of the object across the network|
|Both|**GetNetworkObject(NetworkId id)**|Returns a network object by its id|
|Both|**GetNetworkObjects()**|Returns all the network objects|
|Both|**GetNetworkObjectsOwnedBy(int clientId)**|Returns all the network objects owned by a client **(use -1 for server owned objects)**|
|Both|**GetNetworkObjectCount()**|Return the count of network objects|

Auth side means that it can be used on both sides but need to have the ownership over the object

## **Packets and Owned Packets**

The main (and only) data transfer unit across the network is packets, a data structure serialized/deserialized to/from byte arrays. Packets are very compact, blazing fast and reliable. You can define your own packets easily and the NetworkManager will register them automatically, ASSURING that a packet type will have the same id all across the network.

There are two main types of packets:

- **IPacket:** Default packet type, used by background systems and on actions not actually linked to a NetworkIdentity.
- **IOwnedPacket:** Based on IPacket, works the same, but actually carries an NetworkId used by the NetworkManager to link it to a certain NetworkIdentity, automatically calling its receiving callbacks.

### **Creating a Packet**

You can easily create a packet type just by creating a class. For convention you shall use properties instead of fields, with the name in PascalCase:

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/adf1c2a9-8225-4c5a-b745-25386989a837">
</p>

The Serialize method will tell the NetworkTransport how to translate into bytes and the Deserialize one will do the reverse way

**YOU MAY DEFINE THE PACKETS EACH ONE ON ITS OWN SEPARATED CLASS FOR ORGANIZATION PURPOSES**

### **Sending a Packet**

**To send a packet** you can **use** any **one of the listed methods found in the NetworkBehaviour class that fits your use case**. Normally **you may use SendPacket** alongside **HasAuthority** checks. See the PlayerController example below:

<p align="center">
  <img src="[https://github.com/buff-buff-studio/NetworkLib/assets/17664054/e4dfdd21-1d04-43c3-97a8-448526fbca92](https://github.com/buff-buff-studio/NetworkLib/assets/17664054/6ab8a3db-b5ca-492e-9864-dd50de4d8650)">
</p>

If the object is **guaranteed** to be always owned by the server you don’t need to worry about broadcasting the packet to other players. But **normally** (as in the example above) **you shall handle the broadcasting process** in the **OnServerReceivePacket** method:
<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/da5d380d-0f88-428e-bbea-859827c8a6c3">
</p>

### **Handling Received Packets**

if the packet is an **IOwnedPacket**, the handling process is **automated**: the callback of the behaviors of the network object that owns the packet will be called. **For other packets** you may **use the NetworkBehaviour.GetPacketListener** to add/remove your own listeners **(you can also use this method to handle IOwnedPackets of other objects)**:

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/1dbd8ae0-d518-480d-8fc7-561214c4f591">
</p>

### **Packet Reliability: States and Actions**

There are basically two main types of packets: state packets and action packets. Don’t get confused, both of them normally deals with an object state (as pretty much any method of any class), but there’s an obvious difference between them:

|**State Packets**|**Action Packets**|
| - | - |
|Constant Updated (Normally at a constant rate)|Updated only when needed (Actions/Commands/Etc…)|
|Not crucial, if a packet is lost the next one will just update the state with no problems|Crucial. If a packet is lost the game will differ between server and clients|
|Sent with reliable = false (Or omit the field, as false is it default value)|Sent with reliable = true, so the server will guarantee the order and the delivery of the packet|
|**Limited to 1000 bytes**|**No Bytes Limitation**|

<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/42c9ac1b-7a8d-4041-9fea-81ab75d1b5e6">
</p>

### **Packing Packets**

For optimization purposes packets are packed (...) together with others if they’re sent at the same short-time period. Also if a reliable packet is too big to be sent on the same time, it will be automatically split.

## **Miscellaneous**

### **Split Screen Support**

**Any kind of multiplayer support can be added**. Split screen/local multiplayer can be done creating a local-kind network transport or the **UDP** default transporter itself. Bluetooth and other connection types can also be considered

### **Regenerating Ids**

Sometimes you may need to regenerate the id of an object. You can do this clicking on the **N** button on the id field **DO NOT DO THIS IN RUNTIME**

![](Aspose.Words.8206784b-08db-4513-a674-b3a7d0ba6eb2.013.png)
<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/10112813-60aa-420a-b73b-245125879f18">
</p>

You can also regenerate all ids at once using the Regenerate Ids button on NetworkManager:

![](Aspose.Words.8206784b-08db-4513-a674-b3a7d0ba6eb2.014.png)
<p align="center">
  <img src="https://github.com/buff-buff-studio/NetworkLib/assets/17664054/15779bab-3f74-41da-ab56-aa3eadaac458">
</p>

> [!WARNING]
> **A BUILD GENERATED WITH DIFFERENT IDS WILL NOT WORK WITH A BUILD/EDITOR WITH NEW ONES**
