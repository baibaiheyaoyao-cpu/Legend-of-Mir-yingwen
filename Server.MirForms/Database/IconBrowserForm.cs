using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace Server
{
    /// <summary>
    /// 技能图标对照表: 双库切换浏览(技能页=MagIcon2.Lib / 快捷栏=MagIcon.Lib).
    /// 按图标值N展示 帧2N(普通态)/帧2N+1(按下态) — 与客户端 SkillButton.Index = Icon*2/*2+1 对应.
    /// 解码: 每帧头17字节(W,H,X,Y,SX,SY,Shadow,Length) + GZip数据(A8R8G8B8).
    /// </summary>
    public class IconBrowserForm : Form
    {
        public event Action<int> IconSelected;
        public int SelectedIcon = -1;

        private readonly bool _pickMode;
        private readonly FlowLayoutPanel _flow;
        private readonly TextBox _jump;
        private readonly RadioButton _rbPage;   //技能页: MagIcon2.Lib(大图)
        private readonly RadioButton _rbBar;    //快捷栏: MagIcon.Lib(小图)
        private readonly Label _libLabel;

        public IconBrowserForm(bool pickMode)
        {
            _pickMode = pickMode;

            Text = "技能图标对照表 (双库切换 · 图标值N → 帧2N普通/2N+1按下 · 双击选用)";
            Size = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
            _rbPage = new RadioButton { Text = "技能页 (MagIcon2·大图)", AutoSize = true, Checked = true };
            _rbBar = new RadioButton { Text = "快捷栏 (MagIcon·小图)", AutoSize = true };
            _rbPage.CheckedChanged += (s, e) => RefreshFlow();
            _rbBar.CheckedChanged += (s, e) => RefreshFlow();
            top.Controls.Add(new Label { Text = "查看位置:", AutoSize = true, Anchor = AnchorStyles.Left });
            top.Controls.Add(_rbPage);
            top.Controls.Add(_rbBar);
            top.Controls.Add(new Label { Text = "   跳到图标值:", AutoSize = true, Anchor = AnchorStyles.Left });
            _jump = new TextBox { Width = 70 };
            top.Controls.Add(_jump);
            var jumpButton = new Button { Text = "跳转", Width = 60 };
            top.Controls.Add(jumpButton);
            top.Controls.Add(new Label { Text = "双击图标=选用(回填编号); 每格左帧=普通态 右帧=按下态", AutoSize = true, Anchor = AnchorStyles.Left });

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
            };

            _libLabel = new Label { Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.DarkBlue };

            var bottom = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = "技能页图标 = MagIcon2[Icon×2/×2+1] (MainDialogs L3940) · 快捷栏小格 = MagIcon (L1610) · 两库同编号须为同一图标的小/大两版",
                ForeColor = Color.DarkBlue,
            };

            Controls.Add(_flow);
            Controls.Add(_libLabel);
            Controls.Add(top);
            Controls.Add(bottom);

            jumpButton.Click += (s, e) => JumpTo();
            Load += (s, e) => RefreshFlow();
        }

        private void JumpTo()
        {
            if (!int.TryParse(_jump.Text, out int n)) return;
            int target = n * 96; //每格宽约96px, 粗略换算滚动位置
            if (_flow.VerticalScroll.Visible) _flow.AutoScrollPosition = new Point(0, Math.Min(target, _flow.VerticalScroll.Maximum));
        }

        private static string ResolveLib(string name)
        {
            foreach (string dir in new[] { @".\Data", @"C:\yingwen-mir2\kehux\Data" })
            {
                string p = Path.Combine(dir, name);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private void RefreshFlow()
        {
            string libName = _rbBar.Checked ? "MagIcon.Lib" : "MagIcon2.Lib";
            string path = ResolveLib(libName);
            if (path == null)
            {
                MessageBox.Show("未找到 " + libName + " (查找目录: .\\Data 与 C:\\yingwen-mir2\\kehux\\Data)");
                return;
            }

            _libLabel.Text = "当前库: " + path;

            //清空旧格并释放位图
            foreach (Control c in _flow.Controls)
            {
                foreach (Control cc in c.Controls)
                {
                    if (cc is PictureBox pb)
                    {
                        pb.Image?.Dispose();
                        pb.Image = null;
                    }
                }
                c.Dispose();
            }
            _flow.Controls.Clear();

            try
            {
                LoadFrom(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取图标库失败: " + ex.Message);
            }
        }

        private void LoadFrom(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            int version = br.ReadInt32();
            if (version < 2) throw new Exception("Lib版本过低: " + version);

            int count = br.ReadInt32();
            if (version >= 3) br.ReadInt32(); //frameSeek

            int[] indexList = new int[count];
            for (int i = 0; i < count; i++) indexList[i] = br.ReadInt32();

            int iconCount = count / 2;

            for (int n = 0; n < iconCount; n++)
            {
                int a = n * 2, b = n * 2 + 1;
                if (b >= count) break;

                using var normal = DecodeFrame(fs, indexList[a]);
                using var pressed = DecodeFrame(fs, indexList[b]);

                var cell = new Panel { Width = 88, Height = 92, Margin = new Padding(2) };
                var pic = new PictureBox
                {
                    Size = new Size(84, 64),
                    Location = new Point(2, 2),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(30, 30, 30),
                };
                var combined = new Bitmap(168, 64, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(combined))
                {
                    g.Clear(Color.FromArgb(30, 30, 30));
                    if (normal.Width <= 84 && normal.Height <= 64) g.DrawImageUnscaled(normal, 0, 0);
                    else g.DrawImage(normal, 0, 0, 84, 64);
                    if (pressed.Width <= 84 && pressed.Height <= 64) g.DrawImageUnscaled(pressed, 84, 0);
                    else g.DrawImage(pressed, 84, 0, 84, 64);
                }
                pic.Image = combined;

                var label = new Label
                {
                    Text = "图标 " + n,
                    AutoSize = false,
                    Size = new Size(84, 20),
                    Location = new Point(2, 68),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(Font, FontStyle.Bold),
                    BackColor = Color.Moccasin,
                };

                cell.Controls.Add(pic);
                cell.Controls.Add(label);
                if (_pickMode)
                {
                    pic.Cursor = Cursors.Hand;
                    label.Cursor = Cursors.Hand;
                    pic.DoubleClick += (s, e) => Pick(n);
                    label.DoubleClick += (s, e) => Pick(n);
                }
                _flow.Controls.Add(cell);
            }
        }

        private void Pick(int n)
        {
            SelectedIcon = n;
            IconSelected?.Invoke(n);
            Close();
        }

        /// <summary>按索引解码单帧 → Bitmap(GZip压缩的A8R8G8B8, 与客户端MImage同格式).
        /// 注意: 不关闭传入的fs(共享流, 关闭会导致后续帧读取失败)</summary>
        private static Bitmap DecodeFrame(FileStream fs, int offset)
        {
            fs.Position = offset;

            var br = new BinaryReader(fs);

            short w = br.ReadInt16();
            short h = br.ReadInt16();
            br.ReadInt16(); //X
            br.ReadInt16(); //Y
            br.ReadInt16(); //ShadowX
            br.ReadInt16(); //ShadowY
            byte shadow = br.ReadByte();
            int length = br.ReadInt32();

            if (w <= 0 || h <= 0 || length <= 0) return new Bitmap(1, 1, PixelFormat.Format32bppArgb);

            byte[] compressed = br.ReadBytes(length);
            bool hasMask = (shadow >> 7) == 1;
            if (hasMask)
            {
                //跳过第二层(遮罩)数据, 主帧解码不受影响
                br.ReadInt16(); //MaskW
                br.ReadInt16(); //MaskH
                br.ReadInt16(); //MaskX
                br.ReadInt16(); //MaskY
                int maskLength = br.ReadInt32();
                br.ReadBytes(maskLength);
            }

            byte[] pixels = new byte[w * h * 4];
            using (var ms = new MemoryStream(compressed))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < pixels.Length)
                {
                    int n = gz.Read(pixels, read, pixels.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
            }

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                    System.Runtime.InteropServices.Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(bd.Scan0, y * bd.Stride), rowBytes);
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }
    }
}
