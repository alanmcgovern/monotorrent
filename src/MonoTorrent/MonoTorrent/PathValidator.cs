//
// PathValidator.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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

namespace MonoTorrent
{
    static class PathValidator
    {
        public static void Validate (string path)
        {
            // Make sure the user doesn't try to overwrite system files. Ensure
            // that the path is relative and doesn't try to access its parent folder

            // Unix rooted
            if (path.StartsWith ("/"))
                throw new ArgumentException ($"The path '{path}' cannot be an absolute path starting with '/'.");
            // Windows rooted
            if (path.Length > 1 && path[1] == ':')
                throw new ArgumentException ($"The path '{path}' cannot be an absolute path.");

            // Embedded traversals
            if (path.Contains ("/../"))
                throw new ArgumentException (string.Format ("The path '{1}' cannot contain '{0}'.", "/../", path));
            if (path.Contains ("\\..\\"))
                throw new ArgumentException (string.Format ("The path '{1}' cannot contain '{0}'.", "\\..\\", path));

            // Starting traversals
            if (path.StartsWith ("..\\"))
                throw new ArgumentException (string.Format ("The path '{1}' cannot contain '{0}'.", "..\\", path));
            if (path.StartsWith ("../"))
                throw new ArgumentException (string.Format ("The path '{1}' cannot contain '{0}'.", "../", path));
        }
    }
}