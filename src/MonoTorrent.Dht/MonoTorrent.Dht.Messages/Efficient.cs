using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    static class Krpc
    {
        internal static class Queries
        {
            public static ReadOnlySpan<byte> AnnouncePeer => "announce_peer"u8;
            public static ReadOnlySpan<byte> FindNode => "find_node"u8;
            public static ReadOnlySpan<byte> GetPeers => "get_peers"u8;
            public static ReadOnlySpan<byte> Ping => "ping"u8;
        }

        internal static class MessageType
        {
            public static ReadOnlySpan<byte> Query => "q"u8;
            public static ReadOnlySpan<byte> Response => "r"u8;
            public static ReadOnlySpan<byte> Error => "e"u8;

        }

        /// <summary>
        /// Created by the source, echoed back by the receiver.
        /// </summary>
        public static ReadOnlySpan<byte> TransactionIdKey => "t"u8;

        /// <summary>
        /// A BEncodedString which represents the message type. The value is a
        /// BEncoded string set to 'q' for query, 'r' for response, or 'e' for error.
        /// </summary>
        public static ReadOnlySpan<byte> MessageTypeKey => "y"u8;

        /// <summary>
        /// A BEncodedString representing the version of the client which sent this message
        /// </summary>
        public static ReadOnlySpan<byte> ClientIdentifierKey => "v"u8;



        /// <summary>
        /// A BEncodedString specifying the query type (e.g. find_peers, get_peers, announce, ping)
        /// </summary>
        public static ReadOnlySpan<byte> QueryTypeKey => "q"u8;
        /// <summary>
        /// A BEncodedDictionary of arguments sent with a query message
        /// </summary>
        public static ReadOnlySpan<byte> ArgumentsKey => "a"u8;



        /// <summary>
        /// A BEncodedDictionary holding the return values for the original query.
        /// </summary>
        public static ReadOnlySpan<byte> ReturnValuesKey => "r"u8;


        /// <summary>
        /// This is part of all query arguments and response return values.
        /// </summary>
        public static ReadOnlySpan<byte> NodeId => "id"u8;

        /// <summary>
        /// Used in find_node to hold the nodeid of the node being searched for.
        /// </summary>
        public static ReadOnlySpan<byte> Target => "target"u8;
        public static ReadOnlySpan<byte> InfoHash => "info_hash"u8;
        public static ReadOnlySpan<byte> Token => "token"u8;
        public static ReadOnlySpan<byte> Port => "port"u8;
        public static ReadOnlySpan<byte> ImpliedPort => "implied_port"u8;
        public static ReadOnlySpan<byte> Nodes => "nodes"u8;
        public static ReadOnlySpan<byte> Values => "values"u8;
    }

    public enum KrpcType
    {
        Unknown,
        Query,
        Error,
        Response
    }

    public enum QueryMethod
    {
        Unknown,
        Ping,
        FindNode,
        GetPeers,
        AnnouncePeer
    }

    public readonly struct KrpcRequestExtensions
    {
        readonly ReadOnlyMemory<byte> target;
        readonly ReadOnlyMemory<byte> infoHash;
        readonly ReadOnlyMemory<byte> token;

        public readonly ReadOnlySpan<byte> Target => target.Span;
        public readonly ReadOnlySpan<byte> InfoHash => infoHash.Span;
        public readonly ReadOnlySpan<byte> Token => token.Span;

        public readonly int Port;
        public readonly bool ImpliedPort;

        public KrpcRequestExtensions (
            ReadOnlyMemory<byte> target,
            ReadOnlyMemory<byte> infoHash,
            ReadOnlyMemory<byte> token,
            int port,
            bool impliedPort)
        {
            this.target = target;
            this.infoHash = infoHash;
            this.token = token;
            Port = port;
            ImpliedPort = impliedPort;
        }
    }

    public readonly struct KrpcResponseExtensions
    {
        readonly ReadOnlyMemory<byte> nodes;
        readonly ReadOnlyMemory<byte> values;
        readonly ReadOnlyMemory<byte> token;

        public readonly ReadOnlySpan<byte> Nodes => nodes.Span;
        public readonly ReadOnlySpan<byte> Values => values.Span;
        public readonly ReadOnlySpan<byte> Token => token.Span;

        public KrpcResponseExtensions (
            ReadOnlyMemory<byte> nodes,
            ReadOnlyMemory<byte> values,
            ReadOnlyMemory<byte> token)
        {
            this.nodes = nodes;
            this.values = values;
            this.token = token;
        }

        public bool HasNodes => !nodes.IsEmpty;
        public bool HasValues => !values.IsEmpty;
    }

    public readonly struct KrpcMessage
    {
        readonly ReadOnlyMemory<byte> transactionId;
        readonly ReadOnlyMemory<byte> nodeId;
        readonly KrpcRequestExtensions request;
        readonly KrpcResponseExtensions response;

        public KrpcType MessageType { get; }
        public QueryMethod QueryMethod { get; }
        public readonly ReadOnlySpan<byte> TransactionId => transactionId.Span;
        public readonly ReadOnlySpan<byte> NodeId => nodeId.Span;
        public readonly KrpcRequestExtensions Request => request;
        public readonly KrpcResponseExtensions Response => response;

        public KrpcMessage (
            KrpcType messageType,
            QueryMethod queryMethod,
            ReadOnlyMemory<byte> tx,
            ReadOnlyMemory<byte> nodeId,
            KrpcRequestExtensions request,
            KrpcResponseExtensions response)
        {
            MessageType = messageType;
            QueryMethod = queryMethod;
            transactionId = tx;
            this.nodeId = nodeId;
            this.request = request;
            this.response = response;
        }

        public static KrpcMessage Parse (ReadOnlyMemory<byte> buffer)
        {
            var reader = new BEncodeReader (buffer.Span);

            reader.ExpectDictionaryBegin ();

            ReadOnlyMemory<byte> tx = default;
            ReadOnlyMemory<byte> y = default;
            ReadOnlyMemory<byte> q = default;

            ReadOnlyMemory<byte> nodeId = default;

            ReadOnlyMemory<byte> target = default;
            ReadOnlyMemory<byte> infoHash = default;
            ReadOnlyMemory<byte> token = default;

            ReadOnlyMemory<byte> nodes = default;
            ReadOnlyMemory<byte> values = default;
            
            int port = 0;
            bool impliedPort = false;

            while (reader.TryReadKey (out var key)) {
                if (key.SequenceEqual (Krpc.TransactionIdKey)) {
                    tx = reader.CaptureString (buffer);
                } else if (key.SequenceEqual (Krpc.MessageTypeKey)) {
                    y = reader.CaptureString (buffer);
                } else if (key.SequenceEqual (Krpc.QueryTypeKey)) {
                    q = reader.CaptureString (buffer);
                } else if (key.SequenceEqual (Krpc.ArgumentsKey)) {
                    reader.ExpectDictionaryBegin ();

                    while (reader.TryReadKey (out var akey)) {
                        if (akey.SequenceEqual (Krpc.NodeId))
                            nodeId = reader.CaptureString (buffer);

                        else if (akey.SequenceEqual (Krpc.Target))
                            target = reader.CaptureString (buffer);

                        else if (akey.SequenceEqual (Krpc.InfoHash))
                            infoHash = reader.CaptureString (buffer);

                        else if (akey.SequenceEqual (Krpc.Token))
                            token = reader.CaptureString (buffer);

                        else if (akey.SequenceEqual (Krpc.Port)) {
                            reader.CaptureInteger (buffer);
                            port = (int) reader.Integer;
                        } else if (akey.SequenceEqual (Krpc.ImpliedPort)) {
                            reader.CaptureInteger (buffer);
                            impliedPort = reader.Integer != 0;
                        } else
                            reader.SkipValue ();
                    }
                } else if (key.SequenceEqual (Krpc.ReturnValuesKey)) {
                    reader.ExpectDictionaryBegin ();

                    while (reader.TryReadKey (out var rkey)) {
                        if (rkey.SequenceEqual (Krpc.NodeId))
                            nodeId = reader.CaptureString (buffer);

                        else if (rkey.SequenceEqual (Krpc.Nodes))
                            nodes = reader.CaptureString (buffer);

                        else if (rkey.SequenceEqual (Krpc.Values))
                            values = reader.CaptureAny (buffer);

                        else if (rkey.SequenceEqual (Krpc.Token))
                            token = reader.CaptureString (buffer);

                        else
                            reader.SkipValue ();
                    }
                } else {
                    reader.SkipValue ();
                }
            }

            KrpcType messageType;
            QueryMethod queryMethod;

            if (y.Span.Length == 1 && y.Span[0] == (byte) 'q') {
                messageType = KrpcType.Query;
            } else if (y.Span.Length == 1 && y.Span[0] == (byte) 'r') {
                messageType = KrpcType.Response;
            } else if (y.Span.Length == 1 && y.Span[0] == (byte) 'e') {
                messageType = KrpcType.Error;
            } else {
                messageType = KrpcType.Unknown;
            }

            if (q.Span.SequenceEqual (Krpc.Queries.Ping))
                queryMethod = QueryMethod.Ping;
            else if (q.Span.SequenceEqual (Krpc.Queries.FindNode))
                queryMethod = QueryMethod.FindNode;
            else if (q.Span.SequenceEqual (Krpc.Queries.GetPeers))
                queryMethod = QueryMethod.GetPeers;
            else if (q.Span.SequenceEqual (Krpc.Queries.AnnouncePeer))
                queryMethod = QueryMethod.AnnouncePeer;
            else
                queryMethod = QueryMethod.Unknown;

            var request = new KrpcRequestExtensions (
                target,
                infoHash,
                token,
                port,
                impliedPort);

            var response = new KrpcResponseExtensions (
                nodes,
                values,
                token);

            return new KrpcMessage (
                messageType,
                queryMethod,
                tx,
                nodeId,
                request,
                response);
        }
    }

    public static class KrpcMessageEncoder
    {
        // large enough fixed overhead for all static protocol bytes. Is it worth calculating this exactly?
        const int BaseSize = 96;

        public static int Estimate (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> query = default,
            ReadOnlySpan<byte> target = default,
            ReadOnlySpan<byte> infoHash = default,
            ReadOnlySpan<byte> token = default,
            ReadOnlySpan<byte> nodes = default,
            ReadOnlySpan<byte> values = default)
        {
            return BaseSize
                 + transactionId.Length
                 + nodeId.Length
                 + query.Length
                 + target.Length
                 + infoHash.Length
                 + token.Length
                 + nodes.Length
                 + values.Length;
        }

        //
        // --------------------------------------------------------------------
        // QUERY ENCODERS
        // --------------------------------------------------------------------
        //

        public static int EncodePing (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ArgumentsKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.End ();

            w.WriteString (Krpc.QueryTypeKey);
            w.WriteString (Krpc.Queries.Ping);

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Query);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodePing (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId, Krpc.Queries.Ping)];

            return buffer.Slice (0, EncodePing (buffer.Span, transactionId, nodeId));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeFindNode (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> target)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ArgumentsKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.WriteString (Krpc.Target);
            w.WriteString (target);

            w.End ();

            w.WriteString (Krpc.QueryTypeKey);
            w.WriteString (Krpc.Queries.FindNode);

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Query);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeFindNode (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> target)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId,
                         Krpc.Queries.FindNode,
                         target: target)];

            return buffer.Slice (0, EncodeFindNode (buffer.Span, transactionId, nodeId, target));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeGetPeers (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> infoHash)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ArgumentsKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.WriteString (Krpc.InfoHash);
            w.WriteString (infoHash);

            w.End ();

            w.WriteString (Krpc.QueryTypeKey);
            w.WriteString (Krpc.Queries.GetPeers);

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Query);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeGetPeers (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> infoHash)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId,
                         Krpc.Queries.GetPeers,
                         infoHash: infoHash)];

            return buffer.Slice (0, EncodeGetPeers (buffer.Span, transactionId, nodeId, infoHash));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeAnnouncePeer (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> infoHash,
            ReadOnlySpan<byte> token,
            int port,
            bool impliedPort)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ArgumentsKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            if (impliedPort) {
                w.WriteString (Krpc.ImpliedPort);
                w.WriteLong (1);
            }

            w.WriteString (Krpc.InfoHash);
            w.WriteString (infoHash);

            w.WriteString (Krpc.Port);
            w.WriteLong (port);

            w.WriteString (Krpc.Token);
            w.WriteString (token);

            w.End ();

            w.WriteString (Krpc.QueryTypeKey);
            w.WriteString (Krpc.Queries.AnnouncePeer);

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Query);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeAnnouncePeer (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> infoHash,
            ReadOnlySpan<byte> token,
            int port,
            bool impliedPort)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId,
                         Krpc.Queries.AnnouncePeer,
                         infoHash: infoHash,
                         token: token)];

            return buffer.Slice (0, EncodeAnnouncePeer (
                buffer.Span,
                transactionId,
                nodeId,
                infoHash,
                token,
                port,
                impliedPort));
        }

        //
        // --------------------------------------------------------------------
        // RESPONSE ENCODERS
        // --------------------------------------------------------------------
        //

        public static int EncodePingResponse (
        Span<byte> dest,
        ReadOnlySpan<byte> transactionId,
        ReadOnlySpan<byte> nodeId)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ReturnValuesKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.End ();

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Response);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodePingResponse (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId)];

            return buffer.Slice (0, EncodePingResponse (buffer.Span, transactionId, nodeId));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeFindNodeResponse (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> nodes)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ReturnValuesKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.WriteString (Krpc.Nodes);
            w.WriteString (nodes);

            w.End ();

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Response);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeFindNodeResponse (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> nodes)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId, nodes: nodes)];

            return buffer.Slice (0, EncodeFindNodeResponse (buffer.Span, transactionId, nodeId, nodes));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeGetPeersResponseNodes (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> token,
            ReadOnlySpan<byte> nodes)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ReturnValuesKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.WriteString (Krpc.Nodes);
            w.WriteString (nodes);

            w.WriteString (Krpc.Token);
            w.WriteString (token);

            w.End ();

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Response);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeGetPeersResponseNodes (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> token,
            ReadOnlySpan<byte> nodes)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId,
                         token: token,
                         nodes: nodes)];

            return buffer.Slice (0, EncodeGetPeersResponseNodes (
                buffer.Span,
                transactionId,
                nodeId,
                token,
                nodes));
        }

        //
        // --------------------------------------------------------------------
        //

        public static int EncodeGetPeersResponseValues (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> token,
            ReadOnlySpan<byte> values)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            w.WriteString (Krpc.ReturnValuesKey);
            w.BeginDict ();

            w.WriteString (Krpc.NodeId);
            w.WriteString (nodeId);

            w.WriteString (Krpc.Token);
            w.WriteString (token);

            w.WriteString (Krpc.Values);
            w.WriteRaw (values);

            w.End ();

            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Response);

            w.End ();

            return w.Written;
        }

        public static ReadOnlyMemory<byte> EncodeGetPeersResponseValues (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId,
            ReadOnlySpan<byte> token,
            ReadOnlySpan<byte> values)
        {
            Memory<byte> buffer = new byte[
                Estimate (transactionId, nodeId,
                         token: token,
                         values: values)];

            return buffer.Slice (0, EncodeGetPeersResponseValues (
                buffer.Span,
                transactionId,
                nodeId,
                token,
                values));
        }

        public static int EncodeAnnouncePeerResponse (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId)
        {
            return EncodePingResponse (
                dest,
                transactionId,
                nodeId);
        }

        public static ReadOnlyMemory<byte> EncodeAnnouncePeerResponse (
            ReadOnlySpan<byte> transactionId,
            ReadOnlySpan<byte> nodeId)
        {
            return EncodePingResponse (
                transactionId,
                nodeId);
        }

        public static int EncodeError (
            Span<byte> dest,
            ReadOnlySpan<byte> transactionId,
            int errorCode,
            ReadOnlySpan<byte> errorMessage)
        {
            var w = new BEncodeWriter (dest);

            w.BeginDict ();

            // t -> transaction id
            w.WriteString (Krpc.TransactionIdKey);
            w.WriteString (transactionId);

            // y -> error
            w.WriteString (Krpc.MessageTypeKey);
            w.WriteString (Krpc.MessageType.Error); // "e"

            // e -> list [code, message]
            w.WriteString ("e"u8);
            w.BeginList (); // <-- you need this (see note below)

            w.WriteLong (errorCode);
            w.WriteString (errorMessage);

            w.End (); // list

            w.End (); // dict

            return w.Written;
        }

        public static byte[] EncodeError (
            ReadOnlySpan<byte> transactionId,
            int errorCode,
            ReadOnlySpan<byte> errorMessage)
        {
            const int BaseSize = 128;

            var buffer = new byte[
                BaseSize +
                transactionId.Length +
                errorMessage.Length];

            EncodeError (buffer, transactionId, errorCode, errorMessage);
            return buffer;
        }
    }
}
