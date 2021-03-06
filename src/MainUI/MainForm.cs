﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Pyramid
{
    public partial class MainForm : Form
    {
        private List<IBackend> m_Backends;
        private List<ILanguage> m_Languages;
        private ICompileOptionsPanel m_CompileOptionsPanel;
        private Options m_Options;
        private IWrapper m_Wrapper;
        private string m_FileName = "";
        private string m_LastBackend = "";

        public Options Options { get { return m_Options; } }
      
        private void CreateBackends( Options opts )
        {
            List<IBackend> backends = new List<IBackend>();

            try
            {
                ID3DCompiler fxc = m_Wrapper.CreateD3DCompiler(opts.D3DCompilerPath);
                backends.Add(new D3DCompilerBackend(fxc));
                try
                {
                    IAMDDriver driver = m_Wrapper.CreateAMDDriver(opts.DXXDriverPath);
                    backends.Add(new AMDDriverBackend(driver, fxc));
                }
                catch( System.Exception ex )
                {
                    MessageBox.Show(ex.Message);
                }
            }
            catch( System.Exception ex )
            {
                MessageBox.Show(ex.Message);
            }
            
            backends.Add(new CodeXLBackend(opts.CodeXLPath, opts.D3DCompilerPath, opts.TempPath));
            backends.Add(new GLSlangBackend(m_Wrapper));
            backends.Add(new GLSLOptimizerBackend(m_Wrapper));
            backends.Add(new PowerVRBackend(opts.PowerVRCompilerPath, opts.TempPath));
            backends.Add(new MaliSCBackend(opts.MaliSCRoot, opts.TempPath));

            if( File.Exists( opts.MysteryToolPath ) )
            {
                backends.Add(new MysteryToolBackend(opts));
            }

            m_Backends = backends;
        }

        public MainForm( Options options, IWrapper wrapper )
        {
            InitializeComponent();
            m_Options   = options;
            cmbBackend.DisplayMember  = "Name";
            cmbLanguage.DisplayMember = "Name";

            List<ILanguage> languages = new List<ILanguage>();
            languages.Add(new HLSLLanguage());
            languages.Add(new GLSLLanguage());
            m_Languages = languages;

            foreach (ILanguage l in m_Languages)
                cmbLanguage.Items.Add(l);

            cmbLanguage.SelectedIndex = 0;

            m_Wrapper = wrapper;
            CreateBackends(options);

            txtCode.AllowDrop = true;
            txtCode.DragEnter += 
                delegate( object sender, DragEventArgs args )
                {
                    if (args.Data.GetDataPresent(DataFormats.FileDrop))
                        args.Effect = DragDropEffects.Move;
                    else
                        args.Effect = DragDropEffects.None;
                };

            txtCode.DragDrop +=
                delegate(object sender, DragEventArgs args)
                {
                    if (args.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] paths = args.Data.GetData(DataFormats.FileDrop) as string[];
                        OpenFile(paths[0]);
                    }
                };
        }

        private void ClearResults()
        {
            cmbBackend.Items.Clear();
            BackendPanel.Controls.Clear();
            AnalysisPanel.Controls.Clear();
        }

        private void btnCompile_Click(object sender, EventArgs e)
        {
            if( m_CompileOptionsPanel == null )
                return;

            this.UseWaitCursor = true;

            ClearResults();

            IResultSet SelectedResultSet = null;

            ICompileOptions opts = m_CompileOptionsPanel.ReadOptions();
            
            IShader shader = null;
            switch (opts.Language)
            {
                case Languages.GLSL: 
                    shader = new GLSLShader(txtCode.Text, opts as IGLSLOptions); break;
                case Languages.HLSL: 
                    shader = new HLSLShader(txtCode.Text, opts as IHLSLOptions); break;
                default:
                    throw new System.Exception("Unsupported language");
            }

            foreach (IBackend backend in m_Backends)
            {
                if (m_Options.IsBackendDisabled(backend.Name))
                    continue;

                IBackendOptions options = null;

                if (backend is AMDDriverBackend)
                {
                    AMDDriverBackend amdBackend = backend as AMDDriverBackend;
                    List<string> requestedAsics = new List<string>();
                    foreach (string asic in amdBackend.Asics)
                    {
                        if (!m_Options.IsAMDAsicDisabled(asic))
                        {
                            requestedAsics.Add(asic);
                        }
                    }
                    AMDDriverBackendOptions backendOptions = new AMDDriverBackendOptions(requestedAsics);
                    options = backendOptions;
                }
                else if (backend is CodeXLBackend)
                {
                    CodeXLBackend codeXLBackend = backend as CodeXLBackend;
                    List<string> requestedAsics = new List<string>();
                    foreach (string asic in codeXLBackend.Asics)
                    {
                        if (!m_Options.IsCodeXLAsicDisabled(asic))
                        {
                            requestedAsics.Add(asic);
                        }
                    }
                    CodeXLBackendOptions backendOptions = new CodeXLBackendOptions(requestedAsics);
                    options = backendOptions;
                }

                IResultSet r = backend.Compile(shader, options);
                if (r != null)
                {
                    if (r.Name.Equals(m_LastBackend))
                        SelectedResultSet = r;
                    cmbBackend.Items.Add(r);
                }
            }

            if (cmbBackend.Items.Count > 0)
            {
                if (SelectedResultSet != null)
                    cmbBackend.SelectedIndex = cmbBackend.Items.IndexOf(SelectedResultSet);
                else
                    cmbBackend.SelectedIndex = 0;
            }
            else
            {
                m_LastBackend = "";
            }

            this.UseWaitCursor = false;
        }

        private void cmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            CompilePanel.Controls.Clear();

            ClearResults();

            m_CompileOptionsPanel = null;
            if (cmbLanguage.SelectedItem != null)
            {
                ILanguage l = (ILanguage)cmbLanguage.SelectedItem;
                CompilePanel.Controls.Add(l.OptionsPanel.Panel);
                m_CompileOptionsPanel = l.OptionsPanel;

                try
                {
                    txtCode.SetHighlighting(l.Name);
                }
                catch( System.Exception ex )
                {
                    MessageBox.Show(ex.Message, "Uh-oh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }

        private void cmbBackend_SelectedIndexChanged(object sender, EventArgs e)
        {
            BackendPanel.Controls.Clear();
            AnalysisPanel.Controls.Clear();
            if (cmbBackend.SelectedItem != null )
            {
                IResultSet rs = (IResultSet) cmbBackend.SelectedItem;

                if (rs.ResultsPanel != null)
                {
                    BackendPanel.Controls.Add(rs.ResultsPanel);
                    rs.ResultsPanel.Dock = DockStyle.Fill;
                }

                if (rs.AnalysisPanel != null)
                {
                    AnalysisPanel.Controls.Add(rs.AnalysisPanel);
                    rs.AnalysisPanel.Dock = DockStyle.Fill;
                }
                m_LastBackend = rs.Name;
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OptionsScreen opts = new OptionsScreen(m_Options, m_Backends);
            if (opts.ShowDialog() != DialogResult.Cancel)
            {
                m_Options = opts.SelectedOptions;
                ClearResults();
                CreateBackends(m_Options);
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Text = "Pyramid";
            txtCode.Text = "";
            txtCode.Refresh();
            m_FileName = "";
            ClearResults();
        }

        private void OpenFile( string path )
        {
            try
            {
                string code = File.ReadAllText(path);
                txtCode.Text = code;
                ClearResults();
                m_FileName = path;
                this.Text = "Pyramid : " + m_FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "uh-oh, couldn't Open", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            if( fd.ShowDialog() != DialogResult.Cancel)
            {
                OpenFile(fd.FileName);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(m_FileName))
                SaveAs();
            else
                Save(m_FileName);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAs();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SaveAs()
        {
            SaveFileDialog save = new SaveFileDialog();
            if (save.ShowDialog() != DialogResult.Cancel)
            {
                Save(save.FileName);
                m_FileName = save.FileName;
                this.Text = "Pyramid : " + m_FileName;
            }
        }

        private void Save( string As )
        {
            try
            {
                File.WriteAllText(As, txtCode.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "uh-oh, couldn't save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox a = new AboutBox();
            a.ShowDialog();
        }
      
    }
}
