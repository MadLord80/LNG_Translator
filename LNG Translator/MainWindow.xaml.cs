using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows.Controls;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;
using System.Collections;

namespace LNG_Translator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string windowTitle = "LNG Translator";
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private string FileName;
        private int addrLength = 4;
        private List<LNGRow> lngRows = new List<LNGRow>();
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 1200, "1200: UTF-16 LE" }, { 932, "932: Shift-JIS" }, {0,"" } };
        private int curEnc = 1200;

        private Dictionary<string, string> langs;
        private string curFromLang = "ja";
        private string curToLang = "en";

        public MainWindow()
        {
            InitializeComponent();

            this.FillLangs();

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                if (enc.CodePage != 1200 && enc.CodePage != 932)
                {
                    encodings.Add(enc.CodePage, enc.CodePage + ": " + enc.DisplayName);
                }
            }
            foreach (KeyValuePair<int, string> enc in encodings)
            {
                if (enc.Key == 0)
                {
                    Separator ms = new Separator();
                    resEncMenuItem.Items.Add(ms);
                    continue;
                }
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = enc.Value,
                    Tag = enc.Key,
                    IsCheckable = true
                };
                mi.Click += resEncMenuItem_Select;
                if (enc.Key == this.curEnc) { mi.IsChecked = true; }
                resEncMenuItem.Items.Add(mi);
            }

            foreach (KeyValuePair<string, string> lang in langs)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi.Click += langFrom_Select;
                if (lang.Value == curFromLang) { mi.IsChecked = true; }
                langFromMenuItem.Items.Add(mi);
                System.Windows.Controls.MenuItem mi2 = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi2.Click += langTo_Select;
                if (lang.Value == curToLang) { mi2.IsChecked = true; }
                langToMenuItem.Items.Add(mi2);
            }
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;

            OpenFileDialog.Filter = "LNG files (*.LNG)|*.LNG|All files (*.*)|*.*";

            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Google Translate" };
            exportItem.Click += TranslateContextButtonClick;
            context.Items.Add(exportItem);
            System.Windows.Controls.MenuItem copyItem = new System.Windows.Controls.MenuItem() { Header = "Copy" };
            copyItem.Click += CopyContextButtonClick;
            context.Items.Add(copyItem);
            //System.Windows.Controls.MenuItem offsetItem = new System.Windows.Controls.MenuItem() { Header = "Offset" };
            //offsetItem.Click += OffsetContextButtonClick;
            //context.Items.Add(offsetItem);
            stringsView.ContextMenu = context;

            searchTextBox.GotFocus += RemovePlaceholder;
            searchTextBox.LostFocus += AddSearchText;

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;
        }

        private void RemovePlaceholder(object sender, RoutedEventArgs e)
        {
            if (searchTextBox.Text == "Search...")
            {
                searchTextBox.Text = "";
            }
        }
        private void AddSearchText(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                searchTextBox.Text = "Search...";
            }                
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                //byte[] searchTextBytes = Encoding.Default.GetBytes(searchTextBox.Text);
                //string searchText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.Convert(Encoding.Default, Encoding.GetEncoding(this.curEnc), searchTextBytes));
                string searchText = searchTextBox.Text;
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText.Contains(searchText));
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void TranslateContextButtonClick(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            byte[] trans = Encoding.UTF8.GetBytes(this.GoogleTranslate(lrow.OrigText));
            //lrow.TransTextBytes = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(lrow.Encoding), trans);
            lrow.TransText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(this.curEnc), trans));
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void CopyContextButtonClick(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            System.Windows.Clipboard.SetText(lrow.OrigText.Replace(Convert.ToChar(0x00), Convert.ToChar(0x20)));

            //string allText = ""; int cnt = 0;
            //for (int i = 0; i < lngRows.Count; i++)
            //{
            //    if (lngRows[i].OrigText != "")
            //    {
            //        allText += lngRows[i].OrigText + "\n";
            //        cnt++;
            //    }
            //}
            //System.Windows.MessageBox.Show("Copied " + cnt + " strings");
            //System.Windows.Clipboard.SetText(allText);

            //string stringsFileName = "origs.txt";
            //byte[] endLine = new byte[] { 0x0d, 0x0a };
            //byte[] BOM = new byte[] { 0xef, 0xbb, 0xbf };
            //using (FileStream fs = new FileStream(stringsFileName, FileMode.Create, FileAccess.ReadWrite))
            //{
            //    fs.Write(BOM, 0, BOM.Length);
            //    for (int i = 0; i < lngRows.Count; i++)
            //    {
            //        if (lngRows[i].OrigText != "")
            //        {
            //            byte[] istr = Encoding.GetEncoding(curEnc).GetBytes(lngRows[i].OrigText);
            //            byte[] str = Encoding.Convert(Encoding.GetEncoding(curEnc), Encoding.UTF8, istr);
            //            fs.Write(str, 0, str.Length);
            //            fs.Write(endLine, 0, endLine.Length);
            //        }
            //    }
            //}
            //System.Windows.MessageBox.Show("Done!");
        }

        //private void OffsetContextButtonClick(object sender, RoutedEventArgs e)
        //{
        //    LNGRow lrow = (LNGRow)stringsView.SelectedItem;
        //    if (lrow == null) { return; }
        //    string offset = lrow.Offset.Select(b => b.ToString("X2")).Aggregate((s1, s2) => s1 + s2);
        //    System.Windows.MessageBox.Show(offset);
        //    Console.WriteLine(lrow.Offset);
        //}

        private void langFrom_Select(object sender, RoutedEventArgs e)
        {
            string lfrom = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curFromLang = lfrom;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
        }
        private void langTo_Select(object sender, RoutedEventArgs e)
        {
            string lto = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curToLang = lto;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
        }

        private void resEncMenuItem_Select(object sender, RoutedEventArgs e)
        {
            int enc = Convert.ToInt32(((System.Windows.Controls.MenuItem)sender).Tag.ToString());
            foreach (var item in resEncMenuItem.Items)
            {
                if (item.GetType() == typeof(Separator)) { continue; }
                ((System.Windows.Controls.MenuItem)item).IsChecked =
                    (((System.Windows.Controls.MenuItem)item).Tag == ((System.Windows.Controls.MenuItem)sender).Tag)
                    ? true : false;
            }
            this.curEnc = enc;
            //this.UpdateStringsEncoding();
            this.ReadFile();
        }

        private void OpenLNGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindowElement.Title = this.windowTitle + " - " + OpenFileDialog.FileName;
                this.FileName = OpenFileDialog.FileName;
                this.ReadFile();
            }
        }

        private void ReadFile()
        {
            byte[] knownSignature = new byte[] { 0xA5, 0x5A, 0x5A, 0xA5, 0x01, 0x00, 0x00, 0x01 };
            this.addrLength = 4;
            using (FileStream fs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
            {
                byte[] signature = new byte[8];
                fs.Read(signature, 0, signature.Length);
                if (!signature.SequenceEqual(knownSignature))
                {
                    System.Windows.MessageBox.Show("Unknown signature!");
                    return;
                }

                fs.Position = 0x40;
                byte[] addr = new byte[4] { 0xff, 0xff, 0x00, 0x00 };
                byte[] endaddr = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
                byte[] bChar = new byte[2] { 0xff, 0xff };
                byte[] bEndChar = new byte[2] { 0x00, 0x00 };
                List<byte> bText = new List<byte>();
                lngRows.Clear();

                int addr_id = 1;
                //int addrLength = 4;
                fs.Read(addr, 0, this.addrLength);
                // may be 4 bytes offset
                if (addr[2] != 0 || addr[3] != 0)
                {
                    this.addrLength = 2;
                    addr[2] = 0; addr[3] = 0;
                    fs.Position -= 2;
                }
                while (!addr.SequenceEqual(endaddr))
                {
                    bText.Clear();
                    long nextAddr = fs.Position;
                    fs.Position = 0x40 + BitConverter.ToUInt32(addr, 0) * 2;
                    fs.Read(bChar, 0, bChar.Length);
                    int maxTextLength = 1024;
                    while (maxTextLength > 0 && fs.Position <= fs.Length - 1)
                    {
                        if (!bChar.SequenceEqual(bEndChar))
                        {
                            bText.Add(bChar[0]); bText.Add(bChar[1]);
                            fs.Read(bChar, 0, bChar.Length);
                            maxTextLength--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    //string sText = Encoding.GetEncoding(this.curEnc).GetString(bText.ToArray());
                    //LNGRow lrow = new LNGRow(addr, sText)
                    //LNGRow lrow = new LNGRow(addr_id, addr, bText.ToArray())
                    //{
                    //    Encoding = curEnc,
                    //    //TransText = sText
                    //    //TransTextBytes = bText.ToArray()
                    //};
                    LNGRow lrow = new LNGRow(
                        addr_id, addr, Encoding.GetEncoding(this.curEnc).GetString(bText.ToArray()), this.curEnc);
                    lngRows.Add(lrow);
                    addr_id++;

                    fs.Position = nextAddr;
                    fs.Read(addr, 0, this.addrLength);
                    if (fs.Position >= fs.Length - 1)
                    {
                        addr = endaddr;
                    }
                }

                ((GridView)stringsView.View).Columns[1].Header = "Original: (found " + lngRows.Count + " strings)";
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText != "");
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FileInfo newFile = new FileInfo(this.FileName);
            string newFileName = newFile.Name.Substring(0, newFile.Name.Length - newFile.Extension.Length) + ".new" + newFile.Extension;

            //0x00 - 0x3f - copy header
            //0x18(4 bytes) - length of file
            //0x20(4 bytes) - last string(end of strings)
            //
            //end of strings(just copy from last string to EOF):
            //1.RG_VOICE_DATA
            //2. 0x31(606 bytes)
            //+ checksum ??? (2 bytes, if no change - still working)

            byte[] origHeader = new byte[64];
            byte[] eofData = new byte[] { };
            byte[] lastOffset = new byte[4];
            using (FileStream ifs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
            {
                ifs.Read(origHeader, 0, origHeader.Length);
                lastOffset = origHeader.Skip(32).Take(4).ToArray();
                uint eofDataOffset = BitConverter.ToUInt32(lastOffset, 0);
                ifs.Position = eofDataOffset;
                Array.Resize(ref eofData, Convert.ToInt32(ifs.Length) - (int)eofDataOffset);
                ifs.Read(eofData, 0, eofData.Length);
            }

            bool hasEofOffset = 
                (lastOffset.SequenceEqual(BitConverter.GetBytes(0x40 + BitConverter.ToUInt32(lngRows.Last().Offset, 0) * 2))) 
                ? true : false;

            byte[] endString = new byte[] { 0x00, 0x00 };
            using (FileStream ofs = new FileStream(newFileName, FileMode.Create, FileAccess.ReadWrite))
            {
                ofs.Write(origHeader, 0, origHeader.Length);
                byte[] offsetData = new byte[(lngRows.Count + 2) * this.addrLength];
                ofs.Write(offsetData, 0, offsetData.Length);

                foreach (LNGRow lrow in lngRows)
                {
                    long curAbsPos = ofs.Position;
                    ofs.Position = 0x40 + this.addrLength * (lrow.Id - 1);
                    byte[] curRelPos = BitConverter.GetBytes((curAbsPos - 0x40) / 2);
                    ofs.Write(curRelPos, 0, this.addrLength);                    

                    ofs.Position = curAbsPos;
                    if (hasEofOffset && lrow == lngRows.Last()) { break; }

                    byte[] saveString = (lrow.TransTextBytes.Length > 0) ? lrow.TransTextBytes : lrow.OrigTextBytes;
                    ofs.Write(saveString, 0, saveString.Length);
                    ofs.Write(endString, 0, endString.Length);
                }
                //0x20(4 bytes) - last string(end of strings)
                long lastAbsPos = ofs.Position;
                ofs.Position = 0x20;
                byte[] lastPos = BitConverter.GetBytes(lastAbsPos);
                ofs.Write(lastPos, 0, 4);
                ofs.Position = lastAbsPos;

                ofs.Write(eofData, 0, eofData.Length);

                //0x18(4 bytes) - length of file
                long lengthAbsPos = ofs.Position;
                ofs.Position = 0x18;
                byte[] fileLength = BitConverter.GetBytes(lengthAbsPos);
                ofs.Write(fileLength, 0, 4);
            }

            System.Windows.MessageBox.Show(newFileName + " saved ok");
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tbox = (System.Windows.Controls.TextBox)sender;
            ContentPresenter cp = (ContentPresenter)tbox.TemplatedParent;
            LNGRow lrow = (LNGRow)cp.Content;
            lrow.TransText = tbox.Text;
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox txtbox = (System.Windows.Controls.TextBox)sender;
            txtbox.IsReadOnly = false;
        }

        //private void UpdateStringsEncoding()
        //{
        //    lngRows.ForEach((lrow) => {
        //        lrow.OrigTextBytes = Encoding.Convert(Encoding.GetEncoding(lrow.Encoding), Encoding.GetEncoding(this.curEnc), lrow.OrigTextBytes);
        //        if (lrow.TransTextBytes != null && lrow.TransTextBytes.Length > 0)
        //        {
        //            lrow.TransTextBytes = Encoding.Convert(Encoding.GetEncoding(lrow.Encoding), Encoding.GetEncoding(this.curEnc), lrow.TransTextBytes);
        //        }
        //        lrow.Encoding = this.curEnc;
        //    });
        //    stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
        //        ? lngRows.Where(row => row.OrigText != "")
        //        : lngRows;
        //    stringsView.Items.Refresh();
        //    AutoSizeColumns(stringsView.View as GridView);
        //}

        private void UpdateStringsView()
        {
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;

            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private string GoogleTranslate(string text)
        {
            // code from https://www.codeproject.com/Tips/5247661/Google-Translate-API-Usage-in-Csharp
            // Set the language from/to in the url (or pass it into this function)
            string url = String.Format 
                ("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
                    curFromLang, curToLang, Uri.EscapeUriString(text));
            HttpClient httpClient = new HttpClient();
            string result = null;
            try
            {
                result = httpClient.GetStringAsync(url).Result;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.ToString());
                return text;
            }

            // Get all json data
            var jsonData = new JavaScriptSerializer().Deserialize<List<dynamic>>(result);

            // Extract just the first array element (This is the only data we are interested in)
            var translationItems = jsonData[0];

            // Translation Data
            string translation = "";

            // Loop through the collection extracting the translated objects
            foreach (object item in translationItems)
            {
                // Convert the item array to IEnumerable
                IEnumerable translationLineObject = item as IEnumerable;

                // Convert the IEnumerable translationLineObject to a IEnumerator
                IEnumerator translationLineString = translationLineObject.GetEnumerator();

                // Get first object in IEnumerator
                translationLineString.MoveNext();

                // Save its value (translated text)
                translation += string.Format(" {0}", Convert.ToString(translationLineString.Current));
            }

            // Remove first blank character
            if (translation.Length > 1) { translation = translation.Substring(1); };

            // Return translation
            return translation;
        }

        private string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
               
        private void SkipEmptyStringsButton_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateStringsView();
        }

        private void AutoSizeColumns(GridView gv)
        {
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the almost same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    else
                    {
                        continue;
                    }
                    c.Width = double.NaN;
                }
            }
        }

        private void FillLangs()
        {
            this.langs = new Dictionary<string, string> {
                {"Japanese","ja"},
                {"English","en"},
                {"Russian","ru"},
                { "Dutch","nl"},
                {"Ukrainian","uk"}
                //{"", "" },

                //{ "Afrikaans", "af" },
                //{"Albanian","sq"},
                //{"Amharic","am"},
                //{"Arabic","ar"},
                //{"Armenian","hy"},
                //{"Azerbaijani","az"},
                //{"Basque","eu"},
                //{"Belarusian","be"},
                //{"Bengali","bn"},
                //{"Bosnian","bs"},
                //{"Bulgarian","bg"},
                //{"Catalan","ca"},
                //{"Chinese(Traditional)","zh-TW"},
                //{"Corsican","co"},
                //{"Croatian","hr"},
                //{"Czech","cs"},
                //{"Danish","da"},
                //{"Esperanto","eo"},
                //{"Estonian","et"},
                //{"Finnish","fi"},
                //{"French","fr"},
                //{"Frisian","fy"},
                //{"Galician","gl"},
                //{"Georgian","ka"},
                //{"German","de"},
                //{"Greek","el"},
                //{"Gujarati","gu"},
                //{"HaitianCreole","ht"},
                //{"Hausa","ha"},
                //{"Hebrew","he"},
                //{"Hindi","hi"},
                //{"Hungarian","hu"},
                //{"Icelandic","is"},
                //{"Igbo","ig"},
                //{"Indonesian","id"},
                //{"Irish","ga"},
                //{"Italian","it"},
                //{"Javanese","jv"},
                //{"Kannada","kn"},
                //{"Kazakh","kk"},
                //{"Khmer","km"},
                //{"Korean","ko"},
                //{"Kurdish","ku"},
                //{"Kyrgyz","ky"},
                //{"Lao","lo"},
                //{"Latin","la"},
                //{"Latvian","lv"},
                //{"Lithuanian","lt"},
                //{"Luxembourgish","lb"},
                //{"Macedonian","mk"},
                //{"Malagasy","mg"},
                //{"Malay","ms"},
                //{"Malayalam","ml"},
                //{"Maltese","mt"},
                //{"Maori","mi"},
                //{"Marathi","mr"},
                //{"Mongolian","mn"},
                //{"Myanmar(Burmese)","my"},
                //{"Nepali","ne"},
                //{"Norwegian","no"},
                //{"Nyanja(Chichewa)","ny"},
                //{"Pashto","ps"},
                //{"Persian","fa"},
                //{"Polish","pl"},
                //{"Portuguese(Portugal,Brazil)","pt"},
                //{"Punjabi","pa"},
                //{"Romanian","ro"},
                //{"Samoan","sm"},
                //{"ScotsGaelic","gd"},
                //{"Serbian","sr"},
                //{"Sesotho","st"},
                //{"Shona","sn"},
                //{"Sindhi","sd"},
                //{"Sinhala(Sinhalese)","si"},
                //{"Slovak","sk"},
                //{"Slovenian","sl"},
                //{"Somali","so"},
                //{"Spanish","es"},
                //{"Sundanese","su"},
                //{"Swahili","sw"},
                //{"Swedish","sv"},
                //{"Tagalog(Filipino)","tl"},
                //{"Tajik","tg"},
                //{"Tamil","ta"},
                //{"Telugu","te"},
                //{"Thai","th"},
                //{"Turkish","tr"},
                //{"Urdu","ur"},
                //{"Uzbek","uz"},
                //{"Vietnamese","vi"},
                //{"Welsh","cy"},
                //{"Xhosa","xh"},
                //{"Yiddish","yi"},
                //{"Yoruba","yo"},
                //{"Zulu","zu"}
            };
        }
        
        private class LNGRow
        {
            private int id;
            private byte[] offset;
            //private byte[] origText;
            //private byte[] transText;
            private string origText = "";
            private string transText = "";
            private int encoding;

            //public LNGRow(int id, byte[] offset, byte[] origText)
            public LNGRow(int id, byte[] offset, string origText, int enc)
            {
                this.id = id;
                this.offset = new byte[offset.Length];
                offset.CopyTo(this.offset, 0);
                //this.origText = new byte[origText.Length];
                //origText.CopyTo(this.origText, 0);

                //this.transText = new byte[] { };

                this.origText = origText;
                this.encoding = enc;
            }

            public int Id
            {
                get { return this.id; }
            }

            //public int Encoding
            //{
            //    get { return this.encoding; }
            //    set { this.encoding = value; }
            //}

            public byte[] Offset
            {
                get { return this.offset; }
                //set { this.offset = value; }
            }

            public string OrigText
            {
                get {
                    //byte[] otext = this.origText.Where(b => b != 0x00).ToArray();
                    //return System.Text.Encoding.GetEncoding(this.encoding).GetString(otext);
                    return this.origText;
                }
                //set { this.origText = value; }
            }

            public byte[] OrigTextBytes
            {
                get {
                    //return this.origText;
                    return Encoding.GetEncoding(this.encoding).GetBytes(this.origText);
                }
                //set { this.origText = value; }

            }

            public byte[] TransTextBytes
            {
                get { 
                    return Encoding.GetEncoding(this.encoding).GetBytes(this.transText);
                }
                //get { return this.transText; }
                //set {
                //    this.transText = new byte[value.Length];
                //    value.CopyTo(this.transText, 0);
                //}
            }

            public string TransText
            {
                get {
                    //byte[] ttext = this.transText.Where(b => b != 0x00).ToArray();
                    //return System.Text.Encoding.GetEncoding(this.encoding).GetString(ttext);
                    return this.transText;
                }
                set {
                    //this.transText = System.Text.Encoding.GetEncoding(this.encoding).GetBytes(value);
                    this.transText = value;
                }
            }
        }
    }
}
