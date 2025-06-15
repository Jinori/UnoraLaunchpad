using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public partial class PatchNotesWindow : Window
{
    public PatchNotesWindow()
    {
        InitializeComponent();
        LoadPatchNotesAsync();
    }

    private async void LoadPatchNotesAsync()
    {
        try
        {
            using var http = new HttpClient();
            var json = await http.GetStringAsync("http://unora.freeddns.org/unoralauncher/patch_notes.json");

            var patchNotes = JsonConvert.DeserializeObject<List<PatchNote>>(json);

            foreach (var note in patchNotes)
            {
                var header = new TextBlock
                {
                    Text = $"{note.Date} - {note.Title}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                PatchNotesPanel.Children.Add(header);

                foreach (var section in note.Sections)
                {
                    var sectionHeader = new TextBlock
                    {
                        Text = section.Tag,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 5, 0, 2)
                    };
                    PatchNotesPanel.Children.Add(sectionHeader);

                    foreach (var line in section.Lines)
                    {
                        PatchNotesPanel.Children.Add(new TextBlock
                        {
                            Text = "• " + line,
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PatchNotesPanel.Children.Add(new TextBlock
            {
                Text = "Failed to load patch notes: " + ex.Message,
                Foreground = System.Windows.Media.Brushes.Red
            });
        }
    }

    private class PatchNote
    {
        public string Date { get; set; }
        public string Title { get; set; }
        public List<Section> Sections { get; set; }
    }

    private class Section
    {
        public string Tag { get; set; }
        public List<string> Lines { get; set; }
    }
}
