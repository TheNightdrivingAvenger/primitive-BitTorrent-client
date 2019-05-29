# Primitive BitTorrent client
University project

Primitive client to work with BitTorrent protocol. Only file downloading is implemented, sharing is not.
Known issues:
If this client received a bad piece (with wrong hash) this piece may not be downloaded in some specific cases unless the download is restarted.

It may be full of other bugs and errors, but most of the time it works OK.

This project uses [this library](https://github.com/Krusen/BencodeNET) to work with torrent files
