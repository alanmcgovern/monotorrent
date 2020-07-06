//
// Unchoker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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


using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    abstract class Unchoker : IUnchoker
    {
        protected TorrentManager Manager { get; }

        protected Unchoker (TorrentManager manager)
        {
            Manager = manager;
        }

        public virtual void Choke (PeerId id)
        {
            Manager.UploadingTo--;
            id.AmChoking = true;
            id.MessageQueue.Enqueue (new ChokeMessage ());
        }

        public abstract void UnchokeReview ();

        public virtual void Unchoke (PeerId id)
        {
            Manager.UploadingTo++;
            id.AmChoking = false;
            id.MessageQueue.Enqueue (new UnchokeMessage ());
            id.LastUnchoked.Restart ();
        }
    }
}
