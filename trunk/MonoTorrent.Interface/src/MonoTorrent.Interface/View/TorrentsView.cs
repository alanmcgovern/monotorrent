/*
 * $Id: TorrentsView.cs 931 2006-08-22 17:45:59Z piotr $
 * Copyright (c) 2006 by Piotr Wolny <gildur@gmail.com>
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using Mono.Unix;

using Gtk;

namespace MonoTorrent.Interface.View
{
    public class TorrentsView : TreeViewWithPopup
    {
        public TorrentsView()
        {
            Init();
        }

        private void Init()
        {
            InitColumns();
            Selection.Mode = SelectionMode.Single;
            Reorderable = true;
            RulesHint = true;
        }

        private void InitColumns()
        {
            string[] columnTitles = {
                Catalog.GetString("State"),
                Catalog.GetString("Name"),
                Catalog.GetString("Size"),
                Catalog.GetString("Progress"),
                Catalog.GetString("Downloaded"),
                Catalog.GetString("Uploaded"),
                Catalog.GetString("DL speed"),
                Catalog.GetString("UL speed"),
                Catalog.GetString("Leechs"),
                Catalog.GetString("Seeds"),
                Catalog.GetString("Peers")
            };
            int length = columnTitles.Length;
            TreeViewColumn[] columns = new TreeViewColumn[length];
            
            for(int i = 0; i < length; i++) {
                columns[i] = new TreeViewColumn(
                        columnTitles[i], new CellRendererText(), "text", i);
                columns[i].SortColumnId = i;
                columns[i].SortIndicator = true;
                columns[i].Resizable = true;
                columns[i].Reorderable = true;
                AppendColumn(columns[i]);
            }

            SearchColumn = 1;

            HeadersVisible = true;
            HeadersClickable = true;
        }
    }
}
