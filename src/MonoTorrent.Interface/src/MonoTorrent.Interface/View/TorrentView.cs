/*
 * $Id: TorrentView.cs 884 2006-08-20 11:17:54Z piotr $
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

using MonoTorrent.Common;

using MonoTorrent.Interface.Helpers;
using MonoTorrent.Interface.Model;

namespace MonoTorrent.Interface.View
{
    public class TorrentView : Notebook
    {
        private ITorrent model;

        private Label nameLabel;

        private Label commentLabel;

        private Label announceUrlsLabel;

        private Label creationDateLabel;

        private Label createdByLabel;

        private Label pieceLengthLabel;

        private Label sizeLabel;

        private Label infoHashLabel;

        private TreeView filesView;

        public TorrentView()
        {
            InitWidgets();
            InitPages();
            Update();
        }

        public ITorrent Model {
            set {
                model = value;
                Update();
            }
        }

        private void InitWidgets()
        {
            nameLabel = new Label();
            commentLabel = new Label();
            creationDateLabel = new Label();
            announceUrlsLabel = new Label();
            createdByLabel = new Label();
            pieceLengthLabel = new Label();
            sizeLabel = new Label();
            infoHashLabel = new Label();
        }

        private void InitPages()
        {
            AppendPage(GetGeneralPage(),
                    new Label(Catalog.GetString("General")));
            AppendPage(GetFilesPage(),
                    new Label(Catalog.GetString("Files")));
        }

        private Widget GetGeneralPage()
        {
            string[] labelNames = new string[] {
                Catalog.GetString("Name:"),
                Catalog.GetString("Comment:"),
                Catalog.GetString("Announce URLs:"),
                Catalog.GetString("Creation date:"),
                Catalog.GetString("Created by:"),
                Catalog.GetString("Piece length:"),
                Catalog.GetString("Size:"),
                Catalog.GetString("InfoHash:")
            };
            Label label;
            uint length = (uint) labelNames.Length;
            Table generalPage = new Table(2, length, false);
            ScrolledWindow scrolled = new ScrolledWindow();
            Label[] labels = new Label[] {
                nameLabel,
                commentLabel,
                announceUrlsLabel,
                creationDateLabel,
                createdByLabel,
                pieceLengthLabel,
                sizeLabel,
                infoHashLabel
            };

            generalPage.BorderWidth = 10;
            generalPage.ColumnSpacing = 5;
            generalPage.RowSpacing = 5;

            for (uint i = 0; i < length; i++) {
                label = new Label(string.Format("<b>{0}</b>", labelNames[i]));
                label.Xalign = 0;
                label.UseMarkup = true;
                generalPage.Attach(label, 0, 1, i, i + 1,
                        AttachOptions.Fill, AttachOptions.Fill, 0, 0);

                labels[i].Xalign = 0;
                generalPage.Attach(labels[i], 1, 2, i, i + 1,
                        AttachOptions.Fill, AttachOptions.Fill, 0, 0);
            }

            scrolled.AddWithViewport(generalPage);
            scrolled.ShadowType = ShadowType.None;
            return scrolled;
        }

        private Widget GetFilesPage()
        {
            ScrolledWindow scrolled = new ScrolledWindow();
            filesView = new TorrentFilesView();
            scrolled.Add(filesView);
            scrolled.ShadowType = ShadowType.None;
            return scrolled;
        }

        private void Update()
        {
            if (model != null) {
                nameLabel.Text = model.Name;
                commentLabel.Text = model.Comment;
                announceUrlsLabel.Text = string.Empty;
                foreach (string announceUrl in model.AnnounceUrls) {
                    announceUrlsLabel.Text += 
                            string.Format("{0}\n", announceUrl);
                }
                announceUrlsLabel.Text = announceUrlsLabel.Text.Substring(0,
                        announceUrlsLabel.Text.Length - 1);
                creationDateLabel.Text = model.CreationDate.ToString();
                createdByLabel.Text = model.CreatedBy;
                pieceLengthLabel.Text = model.PieceLength.ToString();
                sizeLabel.Text = Formatter.FormatSize(model.Size);
                infoHashLabel.Text = Formatter.FormatBytes(model.InfoHash);

                filesView.Model = new TorrentFilesList(model);
            } else {
                nameLabel.Text = string.Empty;
                commentLabel.Text = string.Empty;
                creationDateLabel.Text = string.Empty;
                announceUrlsLabel.Text = string.Empty;
                createdByLabel.Text = string.Empty;
                pieceLengthLabel.Text = string.Empty;
                sizeLabel.Text = string.Empty;
                infoHashLabel.Text = string.Empty;

                filesView.Model = new TorrentFilesList();
            }
        }
    }
}
