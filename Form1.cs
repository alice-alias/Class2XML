using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

using System.Xml.Linq;

namespace Class2XML
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }


        private void button5_Click(object sender, EventArgs e)
        {
            UpdateObjectViewer();
        }

        void UpdateObjectViewer()
        {
            treeView1.Nodes.Clear();

            treeView1.Nodes.AddRange(Namespaces.Select(x =>
            {
                var node = new TreeNode(x.Name) { Tag = x };
                node.Nodes.Add(new TreeNode());
                return node;
            }).ToArray());
        }

        XMLDocCommentList DocComments
        {
            get
            {
                var docs = new XMLDocCommentList();
                docs.AddRange(listBox1.Items.Cast<XMLDocComment>());
                return docs;
            }
        }

        IEnumerable<NamespaceInfo> Namespaces
        {
            get
            {
                return NamespaceInfo.Create(checkedListBox1.Items.Cast<Assembly>().Where((x, i) => checkedListBox1.GetItemChecked(i)), DocComments);
            }
        }

        enum FileOpeningResult
        {
            Succeed, Fail, Skipped
        }

        FileOpeningResult OpenAssembly(string path)
        {
            try
            {
                if (checkedListBox1.Items.Cast<Assembly>().Any(x => x.Location == path))
                {
                    checkedListBox1.SetItemCheckState(checkedListBox1.Items.Cast<Assembly>()
                        .Select((x, i) => Tuple.Create(x, i))
                        .First(x => x.Item1.Location == path).Item2, CheckState.Checked);
                    return FileOpeningResult.Skipped;
                }

                checkedListBox1.Items.Add(Assembly.LoadFrom(path));
                checkedListBox1.SetItemCheckState(checkedListBox1.Items.Count - 1, CheckState.Checked);

                return FileOpeningResult.Succeed;
            }
            catch (Exception)
            {
                return FileOpeningResult.Fail;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (libOpenDialog.ShowDialog() != DialogResult.OK) return;

            int succeed = 0, fail = 0, skipped = 0;
            foreach (var f in libOpenDialog.FileNames)
            {
                var result = OpenAssembly(f);
                if (result == FileOpeningResult.Succeed) succeed++;
                if (result == FileOpeningResult.Fail) fail++;
                if (result == FileOpeningResult.Skipped) skipped++;
            }

            var statuses = new List<string>();
            if (succeed > 0) statuses.Add(string.Format("{0} 読み込み完了", succeed));
            if (skipped > 0) statuses.Add(string.Format("{0} スキップ", skipped));
            if (fail > 0) statuses.Add(string.Format("{0} 失敗", fail));

            statusLabel.Text = string.Join(" / ", statuses);

            UpdateObjectViewer();
        }

        FileOpeningResult OpenXMLDoc(string path)
        {
            try
            {
                if (listBox1.Items.Cast<XMLDocComment>().Any(x => x.FileName == path))
                    return FileOpeningResult.Skipped;

                listBox1.Items.Add(XMLDocComment.Load(path));

                return FileOpeningResult.Succeed;
            }
            catch (Exception)
            {
                return FileOpeningResult.Fail;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (xmldocOpenDialog.ShowDialog() != DialogResult.OK) return;

            int succeed = 0, fail = 0, skipped = 0;
            foreach (var f in xmldocOpenDialog.FileNames)
            {
                var result = OpenXMLDoc(f);
                if (result == FileOpeningResult.Succeed) succeed++;
                if (result == FileOpeningResult.Fail) fail++;
                if (result == FileOpeningResult.Skipped) skipped++;
            }

            var statuses = new List<string>();
            if (succeed > 0) statuses.Add(string.Format("{0} 読み込み完了", succeed));
            if (skipped > 0) statuses.Add(string.Format("{0} スキップ", skipped));
            if (fail > 0) statuses.Add(string.Format("{0} 失敗", fail));

            statusLabel.Text = string.Join(" / ", statuses);

            UpdateObjectViewer();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            checkedListBox1.DisplayMember = "FullName";
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (o, ex) =>
            {
                try
                {
                    return Assembly.ReflectionOnlyLoad(ex.Name);
                }
                catch (Exception exx)
                {
                    try
                    {
                        return Assembly.ReflectionOnlyLoadFrom(
                            System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(ex.RequestingAssembly.Location), ex.Name.Split(',')[0] + ".dll"));
                    }
                    catch
                    {
                        throw exx;
                    }
                }
            };

            AppDomain.CurrentDomain.AssemblyResolve += (o, ex) =>
            {
                try
                {
                    return Assembly.Load(ex.Name);
                }
                catch (Exception exx)
                {
                    try
                    {
                        return Assembly.LoadFrom(
                            System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(ex.RequestingAssembly.Location), ex.Name.Split(',')[0] + ".dll"));
                    }
                    catch
                    {
                        throw exx;
                    }
                }
            };
        }


        private void button2_Click(object sender, EventArgs e)
        {
            if (checkedListBox1.SelectedIndex >= 0)
                checkedListBox1.Items.RemoveAt(checkedListBox1.SelectedIndex);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            while (listBox1.SelectedIndices.Count > 0)
                listBox1.Items.RemoveAt(listBox1.SelectedIndices[0]);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treeView1.SelectedNode != null)
                propertyGrid1.SelectedObject = treeView1.SelectedNode.Tag;
            else
                propertyGrid1.SelectedObject = null;
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            e.Node.Nodes.Clear();
            if (((MemberItem)e.Node.Tag).Children != null)
                e.Node.Nodes.AddRange(((MemberItem)e.Node.Tag).Children.Select(x =>
                {
                    var node = new TreeNode(x.Name) { Tag = x };
                    if(x.Children != null && x.Children.Length > 0)
                        node.Nodes.Add(new TreeNode(x.Name));
                        return node;
                }).ToArray());
        }

        private void treeView1_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            e.Node.Nodes.Clear();
            if (((MemberItem)e.Node.Tag).Children != null && ((MemberItem)e.Node.Tag).Children.Length > 0)
                e.Node.Nodes.Add(new TreeNode());

        }

        private void button7_Click(object sender, EventArgs e)
        {
            exportDialog.FileName = textBox1.Text;
            if (exportDialog.ShowDialog() != DialogResult.OK) return;
            textBox1.Text = exportDialog.FileName;
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            var fileName = textBox1.Text;
            button6.Enabled = false;
            try
            {
                if (System.IO.File.Exists(fileName))
                    MessageBox.Show(string.Format("ファイル {0} は既に存在します。上書きしますか？", fileName), "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                var doc = new XDocument();
                doc.Add(new XElement("namespaces"));

                NamespaceInfo[] nss = new NamespaceInfo[0] ;
                if (checkBox1.Checked)
                    nss = NamespaceInfo.Create(checkedListBox1.Items.Cast<Assembly>().Where((x, i) => checkedListBox1.GetItemChecked(i)).SelectMany(x => x.GetTypes().Where(y => TypeInfo.CheckVisibleType(y) && !y.IsImport)), DocComments);
                else
                    nss = Namespaces.ToArray();

                progressBar1.Maximum = nss.Length;
                progressBar1.Value = 0;
                progressBar1.Step = 1;

                foreach (var ns in nss)
                {
                    doc.Root.Add(ns.CreateXML());

                    progressBar1.PerformStep();
                }
                    
                using (var stream = System.IO.File.Create(fileName))
                    await Task.Run(() => doc.Save(stream));
            }
            finally
            {
                button6.Enabled = true;
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }


        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                int succeed = 0, fail = 0, skipped = 0;
                foreach (var f in files)
                {
                    var resultX = OpenXMLDoc(f);
                    var resultA = OpenAssembly(f);
                    if (resultA == FileOpeningResult.Succeed || resultX == FileOpeningResult.Succeed)
                        succeed++;
                    else if (resultA == FileOpeningResult.Skipped || resultX == FileOpeningResult.Skipped)
                        skipped++;
                    else
                        fail++;
                }

                var statuses = new List<string>();
                if (succeed > 0) statuses.Add(string.Format("{0} 読み込み完了", succeed));
                if (skipped > 0) statuses.Add(string.Format("{0} スキップ", skipped));
                if (fail > 0) statuses.Add(string.Format("{0} 失敗", fail));

                statusLabel.Text = string.Join(" / ", statuses);

                UpdateObjectViewer();
            }
        }
    }
}
