//
// TorrentManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;

using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Optional interface which can be implemented and passed to <see cref="TorrentManager.ChangePicker(IPiecePicker, IPieceRequestUpdater)"/>
    /// to allow piece requests to be cancelled or created for any active peer on demand.
    /// </summary>
    internal interface IPieceRequestUpdater
    {
        /// <summary>
        /// This event should be raised to indicate that <see cref="UpdateRequests(IPiecePicker, IReadOnlyList{IPieceRequester}, IManualPieceRequest)"/>
        /// should be invoked by the <see cref="ClientEngine"/>.
        /// </summary>
        event EventHandler UpdateNeeded;

        /// <summary>
        /// When invoked by the <see cref="ClientEngine"/> it is safe to cancel or create piece requests for any peers. It is up to the
        /// implementator of this method to ensure that <see cref="IPieceRequester.MaxSupportedPendingRequests"/> is respected. Failure
        /// to respect this limit may cause the peer to terminate the connection.
        /// </summary>
        /// <param name="picker">The <see cref="IPiecePicker"/> which can be used to cancel or create requests</param>
        /// <param name="peers">The list of active peers</param>
        /// <param name="requester">Used to enqueue <see cref="RequestMessage"/> or <see cref="CancelMessage"/> messages with the peer depending
        /// on whether a new block needs to be requested, or any existing request is being cancelled.</param>
        void UpdateRequests (IPiecePicker picker, IReadOnlyList<IPieceRequester> peers, IManualPieceRequest requester);
    }
}