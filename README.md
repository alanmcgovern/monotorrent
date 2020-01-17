MonoTorrent
========

[![NuGet version](https://badge.fury.io/nu/monotorrent.svg)](https://badge.fury.io/nu/monotorrent)

[![Build status (master)](https://dev.azure.com/alanmcgovern0144/MonoTorrent/_apis/build/status/MonoTorrent)](https://dev.azure.com/alanmcgovern0144/MonoTorrent/_apis/build/status/MonoTorrent?branchName=master) ![Test coverage (master)](https://img.shields.io/azure-devops/coverage/alanmcgovern0144/monotorrent/2/master)

[![Build status (monotorrent-1.0)](https://dev.azure.com/alanmcgovern0144/MonoTorrent/_apis/build/status/MonoTorrent)](https://dev.azure.com/alanmcgovern0144/MonoTorrent/_apis/build/status/MonoTorrent?branchName=monotorrent-1.0) ![Test coverage (master)](https://img.shields.io/azure-devops/coverage/alanmcgovern0144/monotorrent/2/monotorrent-1.0)


# Supported Specifications

This is a list of all the BEPs which have been implemented in MonoTorrent. A full list of all available BEPs can be seen [here](http://www.bittorrent.org/beps/bep_0000.html)

## Final/Active BEPs
* BEP 3  - [The BitTorrent Protocol Specification](https://www.bittorrent.org/beps/bep_0003.html). ([Alternative specification](https://wiki.theory.org/index.php/BitTorrentSpecification))
* BEP 20 - [Peer ID Conventions](http://www.bittorrent.org/beps/bep_0020.html)

## Accepted BEPs

* BEP 5  - [DHT Protocol](http://www.bittorrent.org/beps/bep_0005.html)
* BEP 6  - [Fast Extension](http://www.bittorrent.org/beps/bep_0006.html)
* BEP 9  - [Extension for Peers to Send Metadata Files](http://www.bittorrent.org/beps/bep_0009.html)
* BEP 10 - [Extension Protocol](http://www.bittorrent.org/beps/bep_0010.html)
* BEP 11 - [Peer Exchange (PEX)](http://www.bittorrent.org/beps/bep_0011.html)
* BEP 12 - [Multitracker Metadata Extension](http://www.bittorrent.org/beps/bep_0012.html)
* BEP 14 - [Local Service/Peer Discovery](http://www.bittorrent.org/beps/bep_0014.html)
* BEP 15 - [UDP Tracker Protocol](http://www.bittorrent.org/beps/bep_0015.html)
* BEP 19 - [HTTP/FTP/Web Seeding (GetRight-style)
](http://www.bittorrent.org/beps/bep_0019.html)
* BEP 23 - [Tracker Returns Compact Peer Lists](http://www.bittorrent.org/beps/bep_0023.html)
* BEP 27 - [Private Torrents](http://www.bittorrent.org/beps/bep_0027.html)

## Draft BEPs

* BEP 16 - [Superseeding](http://www.bittorrent.org/beps/bep_0016.html)
* BEP 48 - [Tracker Protocol Extension: Scrape](http://www.bittorrent.org/beps/bep_0048.html)

## Others
* [Message Stream Encryption (Vuze)](http://wiki.vuze.com/w/Message_Stream_Encryption)


# Supported Client Features

The client downloads torrents and has a wide range of functionality.

* Prioritise specific files.
* Selective file downloading (including the ability to not download specific files).
* Rarest first piece picking (takes priorisation into account).
* End-game mode to boost the last 1-2% of the download.
* Sequential downloading (for media files).
* Per-torrent download/upload rate limiting.
* Overall download/upload rate limiting.
* In memory cache to reduce disk reads.
* Auto-throttling if the download rate exceeds the piece verification/disk write rate.
* IPV4 connections.
* IPV6 connections.
* IP address ban lists.
* Creating torrents from a single file, a folder, or arbitrary files in arbitrary folders.
* Fast resume data can be saved/restored to avoid hashing the data every time a torrent is started.
* Incremental piece hashing (reduces disk reads by incrementally hashing each block in a piece as it is received).
* Partial Hash Checking. If a `TorrentFile` has its `Priority` set to `DoNotDownload` then these files will be skipped when the hash check runs. If the priority is raised then the files will be automatically hash checked (if needed) before any piece is downloaded.
* Sparse files (NTFS filesystem).

* [UPnP port forwarding](https://github.com/mono/Mono.Nat).
* [NAT-PMP port forwarding](https://github.com/mono/Mono.Nat).
* Creating and using [Magnet URI](https://en.wikipedia.org/wiki/Magnet_URI).


# Supported Tracker Features

This is a standard bittorrent tracker server.

* HTTP announce and scrape requests.
* UDP announce and scrape requests.
* Compact peer responses (reduces bandwidth)
* Optionally allows unregistered torrents. In this mode the tracker will begin maintaining peer lists for a torrent as soon as the first announce request is received. 


## JetBrains

A special thank you to [JetBrains](http://www.jetbrains.com/?from=monotorrent) for supplying a free license to their tooling so I can continue to deliver great features on this opensource project.

* [dotTrace](http://www.jetbrains.com/dottrace/?from=monotorrent) - Performance profiling
* [dotMemory](http://www.jetbrains.com/dotmemory/?from=monotorrent) - Memory allocation/retention profiling
