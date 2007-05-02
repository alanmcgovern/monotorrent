//
// ToolBox.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace MonoTorrent.Common
{
    public class ToolBox
    {
		private static Random r = new Random();

		public static void Randomize(MonoTorrentCollectionBase collection)
		{
			MonoTorrentCollectionBase clone = collection.Clone();
			collection.Clear();

			while (clone.Count > 0)
			{
				int index = r.Next(0, clone.Count);
				collection.Add(clone[index]);
				clone.RemoveAt(index);
			}
		}

		public static void Switch(MonoTorrentCollectionBase collection, int first, int second)
		{
			object obj = collection[first];
			collection[first] = collection[second];
			collection[second] = obj;
		}

        public static string GetHex(byte[] infoHash)
        {
            StringBuilder sb = new StringBuilder();

            foreach (byte b in infoHash)
            {
                string hex = b.ToString("X");
                hex = hex.Length < 2 ? "0" + hex : hex;
                sb.Append(hex);
            }
            return sb.ToString();
        }

        /// <summary>
        /// This method takes in two byte arrays and checks if they are equal
        /// </summary>
        /// <param name="array1">The first array</param>
        /// <param name="array2">The second array</param>
        /// <returns>True if the arrays are equal, false if they aren't</returns>
        public static bool ByteMatch(byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)      // If the arrays are different lengths, then they are not equal
                return false;

            return ByteMatch(array1, array2, 0, 0, array1.Length);
        }

        public static bool ByteMatch(byte[] array1, byte[] array2, int offset1, int offset2, int count)
        {
            if (array1 == null)
                throw new ArgumentNullException("array1");
            if(array2 == null)
                throw new ArgumentNullException("array2");

            for (int i = 0; i < count; i++)
                if (array1[offset1 + i] != array2[offset2 + i]) // For each element, if it is not the same in both
                    return false;                               // arrays, return false

            return true;                            // If we get here, all the elements matched, so they are equal
        }
    }
}