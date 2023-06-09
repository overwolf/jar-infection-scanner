using ICSharpCode.SharpZipLib.Zip;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JarInfectionScanner
{
  public partial class Form1 : Form
  {
    static readonly IReadOnlyList<byte[]> kSignatures = new List<byte[]>
    {
      new byte[]
      {
        0x38, 0x54, 0x59, 0x04, 0x10, 0x35, 0x54, 0x59, 0x05, 0x10, 0x2E, 0x54, 0x59, 0x06, 0x10, 0x32, 0x54, 0x59,
        0x07, 0x10, 0x31, 0x54, 0x59, 0x08, 0x10, 0x37, 0x54, 0x59, 0x10, 0x06, 0x10, 0x2E, 0x54, 0x59, 0x10, 0x07,
        0x10, 0x31, 0x54, 0x59, 0x10, 0x08, 0x10, 0x34, 0x54, 0x59, 0x10, 0x09, 0x10, 0x34, 0x54, 0x59, 0x10, 0x0A,
        0x10, 0x2E, 0x54, 0x59, 0x10, 0x0B, 0x10, 0x31, 0x54, 0x59, 0x10, 0x0C, 0x10, 0x33, 0x54, 0x59, 0x10, 0x0D,
        0x10, 0x30, 0x54, 0xB7
      },
      new byte[]
      {
        0x68, 0x54, 0x59, 0x04, 0x10, 0x74, 0x54, 0x59, 0x05, 0x10, 0x74, 0x54, 0x59, 0x06, 0x10, 0x70, 0x54, 0x59,
        0x07, 0x10, 0x3a, 0x54, 0x59, 0x08, 0x10, 0x2f, 0x54, 0x59, 0x10, 0x06, 0x10, 0x2f, 0x54, 0x59, 0x10, 0x07,
        0x10, 0x66, 0x54, 0x59, 0x10, 0x08, 0x10, 0x69, 0x54, 0x59, 0x10, 0x09, 0x10, 0x6c, 0x54, 0x59, 0x10, 0x0a,
        0x10, 0x65, 0x54, 0x59, 0x10, 0x0b, 0x10, 0x73, 0x54, 0x59, 0x10, 0x0c, 0x10, 0x2e, 0x54, 0x59, 0x10, 0x0a,
        0x10, 0x73, 0x54, 0x59, 0x10, 0x0e, 0x10, 0x6b, 0x54, 0x59, 0x10, 0x0f, 0x10, 0x79, 0x54, 0x59, 0x10, 0x10,
        0x10, 0x72, 0x54, 0x59, 0x10, 0x11, 0x10, 0x61, 0x54, 0x59, 0x10, 0x12, 0x10, 0x67, 0x54, 0x59, 0x10, 0x13,
        0x10, 0x65, 0x54, 0x59, 0x10, 0x14, 0x10, 0x2e, 0x54, 0x59, 0x10, 0x15, 0x10, 0x64
      },
      new byte[] {0x2d, 0x54, 0x59, 0x04, 0x10, 0x6a, 0x54, 0x59, 0x05, 0x10, 0x61, 0x54, 0x59, 0x06, 0x10, 0x72}
    };

    public Form1()
    {
      InitializeComponent();
    }

    private void textBoxFolderFile_DragDrop(object sender, DragEventArgs e)
    {
    }

    private async void buttonScan_Click(object sender, EventArgs e)
    {
      if (textBoxFolderFile.Text.Length == 0)
      {
        textBoxFolderFile.Focus();
        return;
      }

      string directory = textBoxFolderFile.Text;

      ClearOutputs();
      buttonScan.Enabled = false;
      buttonClearOutput.Enabled = false;
      progressBar.Value = 0;
      labelStatus.Text = "";

      try
      {
        // We want to maximize parallelized scanning, but we don't want to overload the system, so we try to use 75% of the available processors.
        int processorCount = (int)Math.Max(Math.Floor(Environment.ProcessorCount * 0.75), 1);
        SemaphoreSlim semaphore = new SemaphoreSlim(processorCount, processorCount);
        
        await Task.Run(async () =>
        {
          AddOutputLine("Searching for files (this may take a while) ...");

          string[] jarFiles = GetJarFiles(directory).ToArray();

          int detectionsFound = 0;

          List<Task> tasks = new List<Task>();
          long i = 1;
          foreach (string jarFile in jarFiles)
          {
              tasks.Add(Task.Run(async () =>
              {
                try
                {
                  await semaphore.WaitAsync();
                  AddOutputLine($"[{Interlocked.Increment(ref i)}/{jarFiles.Length}] Scanning {jarFile} ...");
                  
                  if (await CheckJarFileAsync(jarFile).ConfigureAwait(false))
                  {
                    Interlocked.Increment(ref detectionsFound);
                  }
                } finally
                {
                  semaphore.Release();
                }
                
                this.BeginInvoke(new Action(() => {
                  progressBar.Value = (int)Math.Floor((Interlocked.Read(ref i)-1)/(float)jarFiles.Length*100);
                }));
              }));
          }
          
          await Task.WhenAll(tasks);
          
          AddOutputLine("Scan Complete");

          this.BeginInvoke(new Action(() =>
          {
            if (detectionsFound > 0)
            {
              labelStatus.Text = $"Scan complete - found {detectionsFound} infected files";
            } else
            {
              labelStatus.Text = $"Scan complete - no infected files found";
            }
          }));
        });
      } catch (Exception ex)
      {
        AddErrorLine(ex.ToString());
      }
      finally
      {
        buttonScan.Enabled = true;
        buttonClearOutput.Enabled = true;
      }
    }

    private IEnumerable<string> GetJarFiles(string directory, HashSet<string> alreadySearched = null)
    {
      alreadySearched = alreadySearched ?? new HashSet<string>();
      
      try
      {
        // We don't want to search the same directory twice, so we resolve the final path name and check if we've already searched it
        // This is to prevent infinite loops when there are symbolic links
        string finalPathName = NativeMethods.GetFinalPathName(directory);
        if (!alreadySearched.Add(finalPathName))
        {
          yield break;
        }
      }catch
      {
        AddErrorLine($"Failed to resolve '{directory}' to a final path name - skipping directory");
      }
      
      string[] files;
      try
      {
        files = Directory.GetFiles(directory, "*.jar");
      } catch
      {
        files = Array.Empty<string>();
        AddErrorLine($"Failed to get files in directory '{directory}'");
      }
      
      foreach (string file in files)
      {
        yield return file;
      }
      
      string[] directories;
      try
      {
        directories = Directory.GetDirectories(directory);
      } catch
      {
        directories = Array.Empty<string>();
        AddErrorLine($"Failed to get directories in directory '{directory}'");
      }

      foreach (string subDirectory in directories)
      {
        foreach (string file in GetJarFiles(subDirectory, alreadySearched))
        {
          yield return file;
        }
      }
      
    }

    private void buttonBrowse_Click(object sender, EventArgs e)
    {
      CommonOpenFileDialog dialog = new CommonOpenFileDialog
      {
        IsFolderPicker = true,
        Title = "Select a Folder"
      };

      if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
      {
        textBoxFolderFile.Text = dialog.FileName;
      }
    }

    private void buttonClearOutput_Click(object sender, EventArgs e)
    {
      ClearOutputs();
    }

    private void ClearOutputs()
    {
      textBoxOutput.Clear();
      textBoxDetections.Clear();
    }

    private void AddOutputLine(string outputLine)
    {
      this.BeginInvoke(new Action(() =>
      {
        textBoxOutput.AppendText(outputLine + Environment.NewLine);
        textBoxOutput.SelectionStart = textBoxOutput.Text.Length;
        textBoxOutput.ScrollToCaret();
      }));
    }

    private void AddDetectionLine(string outputLine)
    {
      this.BeginInvoke(new Action(() =>
      {
        textBoxDetections.AppendText(outputLine + Environment.NewLine);
        textBoxDetections.SelectionStart = textBoxOutput.Text.Length;
        textBoxDetections.ScrollToCaret();
      }));
    }

    async Task<bool> CheckJarFileAsync(string jarFilePath)
    {
      try
      {
        using (ZipFile jarFile = new ZipFile(jarFilePath))
        {
          foreach (ZipEntry entry in jarFile)
          {
            if (!entry.IsFile || !entry.Name.EndsWith(".class"))
            {
              continue;
            }

            using (Stream zipStream = jarFile.GetInputStream(entry))
            {
              byte[] buffer = new byte[entry.Size];

              // TODO: Conversion might fail for large files - fix to read
              // in a while loop
              await zipStream.ReadAsync(buffer, 0, (int)entry.Size).ConfigureAwait(false);

              foreach (byte[] sequence in kSignatures)
              {
                // if (ContainsByteArray(buffer, sequence)) {
                if (QuickBinaryFind.FindByteSubstring(buffer, sequence) != -1)
                {
                  string line = $"!!!!{jarFilePath} is infected" + Environment.NewLine;
                  AddOutputLine(line);
                  AddDetectionLine(line);
                  return true;
                }
              }
            }
          }
        }
      } catch (Exception ex)
      {
        AddErrorLine($"Error while extracting {jarFilePath}: {ex.Message}");
      }

      return false;
    }

    private void AddErrorLine(string error)
    {
      this.BeginInvoke(new Action(() =>
      {
        errorTextBox.AppendText(error + Environment.NewLine);
        errorTextBox.SelectionStart = textBoxOutput.Text.Length;
        errorTextBox.ScrollToCaret();
      }));
    }
  }
}