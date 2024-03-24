using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using HtmlAgilityPack;
using System.Diagnostics;
using CefSharp;
using CefSharp.WinForms;
using static System.Net.Mime.MediaTypeNames;
using CefSharp.DevTools.DOM;
using System.Text.RegularExpressions;
using CefSharp.DevTools.CSS;
using System.Linq.Expressions;

namespace WebForge
{
    public partial class mainPage : Form
    {
        private string selectedHTMLfile;
        private ChromiumWebBrowser WebBrowser;
        public mainPage()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
            this.WindowState = FormWindowState.Maximized;
            comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            comboBox1.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox1.AutoCompleteSource = AutoCompleteSource.ListItems;
            panel10.Parent = this;
            panel11.Parent = this;
            panel12.Parent = this;
            panel8.Parent = this;
            panel13.Parent = this;
        }
        private void addBtn_MouseClick(object sender, MouseEventArgs e)
        {
            openManager();
        }

        //алгоритъм за избиране на папка със html файл
        private void openManager()
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select a folder";
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    FileView.Nodes.Clear();
                    string selectedFolder = folderBrowserDialog.SelectedPath;
                    PopulateFileView(selectedFolder, FileView.Nodes);
                    checkNodePaths(FileView.Nodes);
                    label3.Text = $"File manager - {FileView.Nodes[0].Text}";
                }
            }

            void PopulateFileView(string directory, TreeNodeCollection parentNode, string parentPath = "")
            {
                var folder = new DirectoryInfo(directory);
                TreeNode node = new TreeNode(folder.Name);

                if (!string.IsNullOrEmpty(parentPath))
                {
                    node.Tag = parentPath;
                }
                else
                {
                    node.Tag = directory;
                }

                parentNode.Add(node);

                foreach (var file in folder.GetFiles())
                {
                    string filePath = file.FullName;
                    node.Nodes.Add(new TreeNode(file.Name) { Tag = filePath });
                }

                foreach (var subfolder in folder.GetDirectories())
                {
                    PopulateFileView(subfolder.FullName, node.Nodes, subfolder.Name);
                }
            }

            void checkNodePaths(TreeNodeCollection nodes)
            {
                foreach (TreeNode node in nodes)
                {
                    string filePath = node.Tag.ToString();
                    string extension = Path.GetExtension(filePath).ToLower();

                    if (File.Exists(filePath))
                    {
                        if (extension == ".css")
                        {
                            CSSpaths.Items.Add(filePath);
                            fixLinkedPathsCSS();
                        }
                    }

                    checkNodePaths(node.Nodes);
                }
            }
        }
        //край на алгоритъма
        bool CEFsettings = false;
        private void OpenBrowser(string htmlContent, string filePath)
        {
            if (WebBrowser != null)
            {
                panel1.Controls.Remove(WebBrowser);
                WebBrowser.Dispose();
            }

            if (!CEFsettings)
            {
                CefSettings settings = new CefSettings();
                Cef.Initialize(settings);
                CEFsettings = true;
            }

            WebBrowser = new ChromiumWebBrowser();
            WebBrowser.Dock = DockStyle.Fill;

            panel1.Controls.Add(WebBrowser);

            WebBrowser.LoadHtml(htmlContent, new Uri(filePath).AbsoluteUri);
        }
        private void FileView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string filePath = e.Node.Tag as string;
            if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("HTML file not found.");
                    return;
                }
                selectedHTMLfile = e.Node.Tag as string;
                string htmlContent = File.ReadAllText(filePath);
                OpenBrowser(htmlContent, filePath);
                HtmlAgilityPack.HtmlDocument htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                HierarchyView.Nodes.Clear();
                AddNodesFromHtml(HierarchyView.Nodes, htmlDoc.DocumentNode.ChildNodes);
            }
        }
        void changeText()
        {
            text.ScrollBars = ScrollBars.Vertical;
            text.Text = HierarchyView.SelectedNode.Tag.ToString();
        }
        //алгоритъм за изкарване на структурата на сайта
        private void AddNodesFromHtml(TreeNodeCollection parent, HtmlNodeCollection nodes)
        {
            foreach (HtmlNode node in nodes)
            {
                if (node.NodeType == HtmlNodeType.Comment)
                {
                    continue;
                }
                else if (node.NodeType == HtmlNodeType.Text)
                {
                    if (!string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        TreeNode textNode = new TreeNode("-text-");
                        textNode.Tag = node.InnerText.Trim();
                        parent.Add(textNode);
                    }
                }
                else
                {
                    TreeNode newNode;
                    if (node.Name == "div" && node.Attributes.Contains("class"))
                    {
                        string className = node.Attributes["class"].Value;
                        newNode = new TreeNode($"div class=\"{className}\"");
                    }
                    else if (node.Attributes.Contains("src"))
                    {
                        string src = node.Attributes["src"].Value;
                        newNode = new TreeNode($"{node.Name} src=\"{src}\"");
                    }
                    else if (node.Attributes.Contains("href"))
                    {
                        string href = node.Attributes["href"].Value;
                        newNode = new TreeNode($"{node.Name} href=\"{href}\"");
                    }
                    else
                    {
                        newNode = new TreeNode(node.Name);
                    }

                    if (node.HasChildNodes)
                    {
                        AddNodesFromHtml(newNode.Nodes, node.ChildNodes);
                    }

                    parent.Add(newNode);
                }
            }
        }
        //край на алгоритъма
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Cef.Shutdown();
            base.OnFormClosing(e);
        }
        bool textChanged = false;
        TreeNode selectedNode = null;
        private void HierarchyView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (HierarchyView.SelectedNode.Text == "-text-" && textChanged == false)
            {
                changeText();
                textChanged = false;
                selectedNode = HierarchyView.SelectedNode;
            }
            else if (HierarchyView.SelectedNode.Text == "-text-")
            {
                if (text.Text != selectedNode.Tag.ToString())
                {
                    DialogResult result = MessageBox.Show("Save text changes?", "Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        selectedNode.Tag = text.Text;
                        selectedNode = HierarchyView.SelectedNode;
                        changeText();
                        textChanged = false;
                    }
                    else if (result == DialogResult.No)
                    {
                        selectedNode = HierarchyView.SelectedNode;
                        changeText();
                        textChanged = false;
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        HierarchyView.SelectedNode = selectedNode;
                    }
                }
                else { changeText(); }
            }
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            string text = comboBox1.Text;
            text = text.Replace(" ", "-");
            comboBox1.Text = text;
            comboBox1.SelectionStart = comboBox1.Text.Length;
        }
        private void button2_Click(object sender, EventArgs e)
        {
            bool exists = false;
            foreach (var item in comboBox1.Items)
            {
                if (comboBox1.Text == item.ToString())
                {
                    exists = true;
                    break;
                }
            }
            if (exists == true)
            {
                listBox1.Items.Add(comboBox1.Text + ": " + textBox1.Text);
                comboBox1.Text = "";
                textBox1.Text = "";
                cssTagPSaved = false;
            }
        }

        void fixLinkedPathsCSS()
        {
            TreeNode rootNode = FileView.Nodes[0];
            List<string> modifiedItems = new List<string>();

            getTagsForPath(rootNode, modifiedItems, ".css");

            CSSpaths.Items.Clear();

            foreach (string modifiedItem in modifiedItems)
            {
                CSSpaths.Items.Add(modifiedItem.Substring(1));
            }
        }

        void getTagsForPath(TreeNode node, List<string> modifiedItems, string fileExtension)
        {
            if (IsFileNode(node, fileExtension))
            {
                string fullPath = node.Tag.ToString();
                string rootPath = FileView.Nodes[0].Tag.ToString();
                string shortenedPath = fullPath.Replace(rootPath, "");
                modifiedItems.Add(shortenedPath);
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                getTagsForPath(childNode, modifiedItems, fileExtension);
            }
        }

        bool IsFileNode(TreeNode node, string fileExtension)
        {
            return node.Tag != null && node.Tag.ToString().EndsWith(fileExtension);
        }
        private void text_TextChanged(object sender, EventArgs e)
        {
            textChanged = true;
        }
        TreeNode draggedNode;
        private void HierarchyView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            draggedNode = (TreeNode)e.Item;
            HierarchyView.DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void HierarchyView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void HierarchyView_DragDrop(object sender, DragEventArgs e)
        {
            TreeNode targetNode = HierarchyView.GetNodeAt(HierarchyView.PointToClient(new System.Drawing.Point(e.X, e.Y)));

            if (targetNode != null && draggedNode != null && targetNode.Parent == draggedNode.Parent)
            {
                int targetIndex = targetNode.Index;
                targetNode.Parent.Nodes.Insert(targetIndex, (TreeNode)draggedNode.Clone());

                draggedNode.Parent.Nodes.Remove(draggedNode);
                draggedNode = null;
            }
        }
        private void HierarchyView_MouseDown(Object sender, MouseEventArgs e)
        {
            panel10.Visible = false;
            panel11.Visible = false;
            panel12.Visible = false;
            panel8.Visible = false;
            panel13.Visible = false;
            if (e.Button == MouseButtons.Right)
            {
                TreeNode selectedNode = HierarchyView.GetNodeAt(e.Location);

                if (selectedNode != null)
                {
                    HierarchyView.SelectedNode = selectedNode;
                    string[] mas = HierarchyView.SelectedNode.Text.Split(' ');
                    string[] types = { "a", "area", "base", "link", "audio", "embed", "iframe", "img", "input", "script", "source", "track", "video", "div" };
                    if (mas[0] == "-text-")
                    {
                        panel12.Visible = true;
                        panel12.Location = new Point(MousePosition.X, MousePosition.Y - panel12.Size.Height);
                        panel12.BringToFront();
                    }
                    else
                    {
                        bool isAbl = true;
                        foreach (string item in types)
                        {
                            if (mas[0] == item)
                            {
                                changeType(item);
                                panel11.Visible = true;
                                panel11.Location = new Point(MousePosition.X, MousePosition.Y - panel11.Size.Height);
                                panel11.BringToFront();
                                isAbl = false;
                            }
                        }
                        if (isAbl == true)
                        {
                            panel10.Visible = true;
                            panel10.Location = new Point(MousePosition.X, MousePosition.Y - panel10.Size.Height);
                            panel10.BringToFront();
                        }
                    }
                }
            }
        }
        void changeType(string tag)
        {
            if (tag == "a" || tag == "area" || tag == "base" || tag == "link")
            {
                label7.Text = "Change href";
            }
            else if (tag == "audio" || tag == "embed" || tag == "iframe" || tag == "img" || tag == "input" || tag == "script" || tag == "source" || tag == "track" || tag == "video")
            {
                label7.Text = "Change src";
            }
            else
            {
                label7.Text = "Change class";
            }
        }
        //алгоритъм за генериране на html код от HierarchyView
        private void GenerateHTML(TreeNode node, StreamWriter writer)
        {
            string[] mas = node.Text.Split(' ');
            if (node.Text == "-text-")
            {
                writer.Write(node.Tag.ToString());
            }
            else if (mas[0] == "link")
            {
                string hrefValue = GetAttributeValue(node.Text, "href");
                if (!string.IsNullOrEmpty(hrefValue))
                {
                    writer.Write($"<link rel=\"stylesheet\" type=\"text/css\" href=\"{hrefValue}\">");
                }
            }
            else
            {
                string hrefValue = GetAttributeValue(node.Text, "href");
                if (!string.IsNullOrEmpty(hrefValue))
                {
                    writer.Write($"<{node.Text.Substring(0, mas[0].Length)} href=\"{hrefValue}\">");
                }
                else
                {
                    writer.Write($"<{node.Text}>");
                }

                foreach (TreeNode childNode in node.Nodes)
                {
                    GenerateHTML(childNode, writer);
                }
                writer.Write($"</{mas[0]}>");
            }
        }

        private string GetAttributeValue(string nodeText, string attributeName)
        {
            string attributeValue = string.Empty;
            int startIndex = nodeText.IndexOf(attributeName);
            if (startIndex != -1)
            {
                int valueStartIndex = startIndex + attributeName.Length + 2;
                int endIndex = nodeText.IndexOf("\"", valueStartIndex);
                if (endIndex != -1)
                {
                    attributeValue = nodeText.Substring(valueStartIndex, endIndex - valueStartIndex);
                }
            }
            return attributeValue;
        }
        //край на алгоритъма
        private void button4_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(selectedHTMLfile))
            {
                WriteTextToFile(selectedHTMLfile, HierarchyView);
                OpenBrowser(File.ReadAllText(selectedHTMLfile), selectedHTMLfile);
            }
            else
            {
                MessageBox.Show("No HTML file detected.", "Notice!");
            }
        }
        void WriteTextToFile(string filePath, System.Windows.Forms.TreeView treeView)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (TreeNode rootNode in treeView.Nodes)
                {
                    writer.Write("<!DOCTYPE html>");
                    GenerateHTML(rootNode, writer);
                }
            }
        }

        private void FileView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            label4.Text = $"Hierarchy - {FileView.SelectedNode.Text}";
        }

        private void CSSpaths_TextUpdate(object sender, EventArgs e)
        {
            if (CSSpaths.Items.Contains(CSSpaths.Text))
            {
                string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
                string text = File.ReadAllText(path);
                comboBox2.Items.Clear();
                string[] tags = GetCssTags(text);
                foreach (string tag in tags)
                {
                    comboBox2.Items.Add(tag);
                }
            }
        }
        //алгоритъм за намиране на всички тагове във css файла
        private void CSSpaths_SelectedIndexChanged(object sender, EventArgs e)
        {
            string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
            string text = File.ReadAllText(path);
            comboBox2.Items.Clear();
            string[] tags = GetCssTags(text);
            foreach (string tag in tags)
            {
                comboBox2.Items.Add(tag);
            }
        }
        static string[] GetCssTags(string cssCode)
        {
            HashSet<string> tagsSet = new HashSet<string>();
            Regex regex = new Regex(@"\b\w+\s*?(?=\{)");

            foreach (Match match in regex.Matches(cssCode))
            {
                string[] selectors = match.Value.Split(',');
                foreach (string selector in selectors)
                {
                    string trimmedSelector = selector.Trim();
                    if (trimmedSelector.Contains('.'))
                    {
                        tagsSet.Add(trimmedSelector);
                    }
                    else
                    {
                        string tag = trimmedSelector.Split(' ')[0];
                        tagsSet.Add(tag);
                    }
                }
            }

            string[] tags = new string[tagsSet.Count];
            tagsSet.CopyTo(tags);
            return tags;
        }
        //намиране на атрибутите на тага който се преглежда
        static Dictionary<string, string> GetAttributesForTag(string cssCode, string tagName)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            string pattern = $@"{tagName}\s*{{[^}}]*}}";
            Match match = Regex.Match(cssCode, pattern);

            if (match.Success)
            {
                string attributesBlock = match.Value;
                string attributesPattern = @"(?<attribute>[a-zA-Z-]+)\s*:\s*(?<value>[^;]+);";
                MatchCollection attributeMatches = Regex.Matches(attributesBlock, attributesPattern);

                foreach (Match attributeMatch in attributeMatches)
                {
                    string attributeName = attributeMatch.Groups["attribute"].Value.Trim();
                    string attributeValue = attributeMatch.Groups["value"].Value.Trim();
                    attributes[attributeName] = attributeValue;
                }
            }

            return attributes;
        }
        //край на алгоритъма

        bool cssTagPSaved = true;
        private void button1_MouseClick(object sender, MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox2.Text) && !comboBox2.Items.Contains(comboBox2.Text))
            {
                comboBox2.Items.Add(comboBox2.Text);
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                listBox1.Items.Remove(listBox1.SelectedItem);
                cssTagPSaved = false;
            }
        }
        private void button4_MouseClick(object sender, MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox2.Text) && comboBox2.Items.Contains(comboBox2.Text))
            {
                comboBox2.Items.Remove(comboBox2.Text);
            }
        }
        private int lastSelectedIndex = -1;

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cssTagPSaved)
            {
                listBox1.Items.Clear();
                string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
                string text = File.ReadAllText(path);
                Dictionary<string, string> attributes = GetAttributesForTag(text, comboBox2.Text);
                foreach (var attribute in attributes)
                {
                    listBox1.Items.Add($"{attribute.Key}: {attribute.Value}");
                }

                lastSelectedIndex = comboBox2.SelectedIndex;
            }
            else
            {
                DialogResult result = MessageBox.Show("Save CSS changes?", "Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    cssTagPSaved = true;

                    if (lastSelectedIndex != -1)
                    {
                        string selectedItem = comboBox2.Items[lastSelectedIndex].ToString();
                        string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
                        string text = File.ReadAllText(path);

                        text = DeleteTagAndProperties(text, selectedItem);
                        text = TransformGroupedTags(text);

                        text += selectedItem + " {";
                        foreach (var item in listBox1.Items)
                        {
                            text += item.ToString() + "; ";
                        }
                        text += "} ";

                        File.WriteAllText(path, text);

                        listBox1.Items.Clear();
                        Dictionary<string, string> updatedAttributes = GetAttributesForTag(text, comboBox2.Text);
                        foreach (var attribute in updatedAttributes)
                        {
                            listBox1.Items.Add($"{attribute.Key}: {attribute.Value}");
                        }
                    }
                }
                else if (result == DialogResult.No)
                {
                    cssTagPSaved = true;
                }
            }
        }
        static string DeleteTagAndProperties(string cssContent, string tagToDelete)
        {
            string pattern = $@"{tagToDelete}\s*{{[^}}]*}}";

            cssContent = Regex.Replace(cssContent, pattern, "", RegexOptions.IgnoreCase);

            return cssContent;
        }

        static string TransformGroupedTags(string cssContent)
        {
            string pattern = @"\.(.*?)\s*({[^}]*})";

            cssContent = Regex.Replace(cssContent, pattern, "$1$2");

            return cssContent;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
            string text = File.ReadAllText(path);
            text += comboBox2.Text + "{}";
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
            string text = File.ReadAllText(path);
            string tagToDelete = comboBox2.Text;
            text = DeleteTagAndProperties(text, tagToDelete);
            text = TransformGroupedTags(text);
            File.WriteAllText(path, text);
        }

        private void mainPage_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cssTagPSaved == false)
            {
                DialogResult result = MessageBox.Show("Save CSS changes?", "Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    cssTagPSaved = true;

                    if (lastSelectedIndex != -1)
                    {
                        string selectedItem = comboBox2.Items[lastSelectedIndex].ToString();
                        string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
                        string text = File.ReadAllText(path);

                        text = DeleteTagAndProperties(text, selectedItem);
                        text = TransformGroupedTags(text);

                        text += selectedItem + " {";
                        foreach (var item in listBox1.Items)
                        {
                            text += item.ToString() + "; ";
                        }
                        text += "} ";

                        File.WriteAllText(path, text);

                        listBox1.Items.Clear();
                        Dictionary<string, string> updatedAttributes = GetAttributesForTag(text, comboBox2.Text);
                        foreach (var attribute in updatedAttributes)
                        {
                            listBox1.Items.Add($"{attribute.Key}: {attribute.Value}");
                        }
                    }
                }
                else if (result == DialogResult.No)
                {
                    cssTagPSaved = true;
                }
            }
            if (HierarchyView.SelectedNode != null && HierarchyView.SelectedNode.Text == "-text-" && textChanged == false)
            {
                changeText();
                textChanged = false;
                selectedNode = HierarchyView.SelectedNode;
            }
        }

        private void label6_MouseClick(object sender, MouseEventArgs e)
        {
            deleteNode();
        }


        private void label9_MouseClick(object sender, MouseEventArgs e)
        {
            deleteNode();
        }

        private void label10_MouseClick(object sender, MouseEventArgs e)
        {
            deleteNode();
        }
        void deleteNode()
        {
            HierarchyView.SelectedNode.Remove();
            panel10.Visible = false;
            panel11.Visible = false;
            panel12.Visible = false;
        }

        private void label5_Click(object sender, EventArgs e)
        {
            addChild(HierarchyView.SelectedNode, label5, panel10);
        }

        private void label8_Click(object sender, EventArgs e)
        {
            panel13.Visible = false;
            addChild(HierarchyView.SelectedNode, label8, panel11);
        }
        TreeNode node;
        void addChild(TreeNode selNode, Label clckLabel, Panel panelS)
        {
            panel8.BringToFront();
            panel8.Location = new Point(panelS.Location.X + clckLabel.Location.X + 95, panelS.Location.Y + clckLabel.Location.Y + 35 - 267);
            panel8.Visible = true;
            node = selNode;
        }

        private void label2_Click_1(object sender, EventArgs e)
        {
            if (listBox2.SelectedItem != null)
            {
                TreeNode newNode = new TreeNode(listBox2.SelectedItem.ToString());
                if (listBox2.SelectedItem.ToString() == "-text-") newNode.Tag = "";
                node.Nodes.Add(newNode);
                panel10.Visible = false;
                panel11.Visible = false;
                panel12.Visible = false;
                panel8.Visible = false;
            }
        }
        TreeNode node2;
        private void label11_Click(object sender, EventArgs e)
        {
            string[] mas = node2.Text.Split(' ');
            string[] mas2 = label7.Text.Split(' ');
            node2.Text = $"{mas[0]} {mas2[1]}=\"{comboBox3.Text}\"";
            panel13.Visible = false;
            panel10.Visible = false;
            panel11.Visible = false;
            panel12.Visible = false;
            panel8.Visible = false;
        }
        void constNode(TreeNode node1)
        {
            node2 = node1;
        }
        private void label7_Click(object sender, EventArgs e)
        {
            panel8.Visible = false;
            panel13.Visible = true;
            panel13.BringToFront();
            panel13.Location = new Point(panel11.Location.X + label7.Location.X + 95, panel11.Location.Y + label7.Location.Y + 35 - 63);
            constNode(HierarchyView.SelectedNode);
            if (label7.Text.Substring(7) == "class")
            {

            }
            else
            {
                comboBox3.Items.Clear();
                foreach (TreeNode rootNode in FileView.Nodes)
                {
                    TraverseTree(rootNode);
                }
            }
        }
        void TraverseTree(TreeNode node)
        {
            string path1 = FileView.Nodes[0].Tag.ToString();
            string path2 = node.Tag.ToString();
            comboBox3.Items.Add(path2.Replace(path1 + "\\", ""));
            foreach (TreeNode childNode in node.Nodes)
            {
                TraverseTree(childNode);
            }
        }

        private void panel14_Click(object sender, EventArgs e)
        {
            cssTagPSaved = true;

            if (lastSelectedIndex != -1)
            {
                string selectedItem = comboBox2.Items[lastSelectedIndex].ToString();
                string path = FileView.Nodes[0].Tag.ToString() + "\\" + CSSpaths.Text;
                string text = File.ReadAllText(path);

                text = DeleteTagAndProperties(text, selectedItem);
                text = TransformGroupedTags(text);

                text += selectedItem + " {";
                foreach (var item in listBox1.Items)
                {
                    text += item.ToString() + "; ";
                }
                text += "} ";

                File.WriteAllText(path, text);

                listBox1.Items.Clear();
                Dictionary<string, string> updatedAttributes = GetAttributesForTag(text, comboBox2.Text);
                foreach (var attribute in updatedAttributes)
                {
                    listBox1.Items.Add($"{attribute.Key}: {attribute.Value}");
                }
            }
        }

        private void panel15_Click(object sender, EventArgs e)
        {
            selectedNode.Tag = text.Text;
            selectedNode = HierarchyView.SelectedNode;
            changeText();
            textChanged = false;
        }
    }
}